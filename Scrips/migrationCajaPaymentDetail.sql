-- =====================================================================
-- Rol Caja y depósitos por producto (2026-10-06).
--
-- Para bases que ya existen (local y servidor desplegado). Una base nueva ya trae todo esto
-- desde dataBase.sql + seedPermissions.sql + seedRoles.sql + seedMenuRoles.sql.
-- Se puede correr varias veces: cada paso revisa antes de crear o borrar.
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationCajaPaymentDetail.sql
-- =====================================================================

BEGIN;

-- 1. Reparto de cada depósito entre los productos de la salida.
CREATE TABLE IF NOT EXISTS payment_detail (
    payment_detail_id BIGSERIAL PRIMARY KEY,
    payment_id BIGINT NOT NULL,
    issue_detail_id BIGINT NOT NULL,
    product_id BIGINT NOT NULL,
    amount NUMERIC(12,2) NOT NULL CHECK (amount > 0),
    CONSTRAINT fk_payment_detail_payment FOREIGN KEY (payment_id) REFERENCES payment(payment_id) ON DELETE CASCADE,
    CONSTRAINT fk_payment_detail_issue_detail FOREIGN KEY (issue_detail_id) REFERENCES issue_detail(issue_detail_id),
    CONSTRAINT fk_payment_detail_product FOREIGN KEY (product_id) REFERENCES product(product_id)
);
CREATE INDEX IF NOT EXISTS ix_payment_detail_payment ON payment_detail(payment_id);
CREATE INDEX IF NOT EXISTS ix_payment_detail_issue_detail ON payment_detail(issue_detail_id);

-- La app se conecta con el usuario inventrack (en local, postgres): necesita poder usar la tabla nueva.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'inventrack') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON payment_detail TO inventrack;
        GRANT USAGE, SELECT ON SEQUENCE payment_detail_payment_detail_id_seq TO inventrack;
    END IF;
END $$;

-- 2. Rol Caja: permiso "trabajo" (registra depósitos) y en el menú solo Cuentas por Cobrar.
--    No se crean permisos nuevos: lo que ve cada rol lo decide su menú, que administra M-BOS.
-- role.name no tiene restricción única: ON CONFLICT no evitaría un duplicado al volver a correr.
INSERT INTO role (name, description, is_active)
SELECT 'Caja', 'Registra los depósitos de Cuentas por Cobrar, por producto', TRUE
WHERE NOT EXISTS (SELECT 1 FROM role WHERE name = 'Caja');

INSERT INTO role_permission (role_id, permission_id, created_at)
SELECT r.role_id, p.permission_id, NOW()
FROM role r
CROSS JOIN permission p
WHERE r.name = 'Caja' AND p.code = 'trabajo'
ON CONFLICT (role_id, permission_id) DO NOTHING;

INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.route = '/account-receivables'
  AND r.name = 'Caja'
ON CONFLICT DO NOTHING;

-- 3. Borrar los pagos generales (sin reparto por producto). Acordado con el usuario: el sistema
--    todavía no está en producción real. Los pagos nuevos siempre tienen payment_detail, así que
--    volver a correr este script no los toca.
DELETE FROM payment p
WHERE NOT EXISTS (SELECT 1 FROM payment_detail d WHERE d.payment_id = p.payment_id);

-- 4. Recalcular saldo y estado de cada cuenta y cuota con los pagos que quedan.
UPDATE account_receivable ar
SET outstanding_balance = GREATEST(ar.total_amount - COALESCE(paid.total, 0), 0),
    status = CASE WHEN ar.total_amount - COALESCE(paid.total, 0) <= 0 THEN 'paid' ELSE 'pending' END
FROM account_receivable ar2
LEFT JOIN (SELECT account_receivable_id, SUM(amount) AS total FROM payment GROUP BY account_receivable_id) paid
       ON paid.account_receivable_id = ar2.account_receivable_id
WHERE ar.account_receivable_id = ar2.account_receivable_id;

UPDATE installment i
SET status = CASE WHEN COALESCE(paid.total, 0) >= i.installment_amount THEN 'paid' ELSE 'pending' END
FROM installment i2
LEFT JOIN (SELECT installment_id, SUM(amount) AS total FROM payment WHERE installment_id IS NOT NULL GROUP BY installment_id) paid
       ON paid.installment_id = i2.installment_id
WHERE i.installment_id = i2.installment_id;

COMMIT;
