-- =====================================================================================
-- Notas de ingreso sin factura (2026-10-26). Se puede correr más de una vez.
-- El material que llega sin factura se anota en su propia tabla y no toca el stock ni los
-- reportes; sus entregas tampoco. Al unir la factura se vuelven entradas y salidas reales.
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationNotasPendientes.sql
-- =====================================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS pending_receipt (
    pending_receipt_id BIGSERIAL PRIMARY KEY,
    monthly_number INT NOT NULL,
    warehouse_id BIGINT NOT NULL,
    supplier_id BIGINT,
    received_date TIMESTAMP NOT NULL,
    description VARCHAR(255),
    status VARCHAR(20) NOT NULL DEFAULT 'pendiente', -- pendiente | validado | anulado
    receipt_id BIGINT, -- Entrada creada al unir la factura.
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by BIGINT,
    closed_at TIMESTAMP,
    closed_by BIGINT,
    CONSTRAINT fk_pending_receipt_warehouse FOREIGN KEY (warehouse_id) REFERENCES warehouse(warehouse_id),
    CONSTRAINT fk_pending_receipt_supplier FOREIGN KEY (supplier_id) REFERENCES supplier(supplier_id),
    CONSTRAINT fk_pending_receipt_receipt FOREIGN KEY (receipt_id) REFERENCES receipt(receipt_id),
    CONSTRAINT fk_pending_receipt_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_pending_receipt_closed_by FOREIGN KEY (closed_by) REFERENCES app_user(app_user_id)
);
CREATE INDEX IF NOT EXISTS idx_pending_receipt_status ON pending_receipt(status);

CREATE TABLE IF NOT EXISTS pending_receipt_detail (
    pending_receipt_detail_id BIGSERIAL PRIMARY KEY,
    pending_receipt_id BIGINT NOT NULL,
    product_id BIGINT NOT NULL,
    quantity NUMERIC(12,2) NOT NULL,
    CONSTRAINT fk_pending_receipt_detail_note FOREIGN KEY (pending_receipt_id) REFERENCES pending_receipt(pending_receipt_id) ON DELETE CASCADE,
    CONSTRAINT fk_pending_receipt_detail_product FOREIGN KEY (product_id) REFERENCES product(product_id)
);
CREATE INDEX IF NOT EXISTS idx_pending_receipt_detail_note ON pending_receipt_detail(pending_receipt_id);

CREATE TABLE IF NOT EXISTS pending_delivery (
    pending_delivery_id BIGSERIAL PRIMARY KEY,
    pending_receipt_id BIGINT NOT NULL,
    delivery_date TIMESTAMP NOT NULL,
    issue_type_id BIGINT,
    client_id BIGINT,
    description VARCHAR(255),
    send_to_accounts_receivable BOOLEAN NOT NULL DEFAULT FALSE,
    payment_type VARCHAR(20),
    payment_detail VARCHAR(255),
    due_date DATE,
    is_voided BOOLEAN NOT NULL DEFAULT FALSE,
    issue_id BIGINT, -- Salida creada al unir la factura de la nota.
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by BIGINT,
    CONSTRAINT fk_pending_delivery_note FOREIGN KEY (pending_receipt_id) REFERENCES pending_receipt(pending_receipt_id),
    CONSTRAINT fk_pending_delivery_issue_type FOREIGN KEY (issue_type_id) REFERENCES issue_type(issue_type_id),
    CONSTRAINT fk_pending_delivery_client FOREIGN KEY (client_id) REFERENCES client(client_id),
    CONSTRAINT fk_pending_delivery_issue FOREIGN KEY (issue_id) REFERENCES issue(issue_id),
    CONSTRAINT fk_pending_delivery_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id)
);
CREATE INDEX IF NOT EXISTS idx_pending_delivery_note ON pending_delivery(pending_receipt_id);

CREATE TABLE IF NOT EXISTS pending_delivery_detail (
    pending_delivery_detail_id BIGSERIAL PRIMARY KEY,
    pending_delivery_id BIGINT NOT NULL,
    product_id BIGINT NOT NULL,
    quantity NUMERIC(12,2) NOT NULL,
    CONSTRAINT fk_pending_delivery_detail_delivery FOREIGN KEY (pending_delivery_id) REFERENCES pending_delivery(pending_delivery_id) ON DELETE CASCADE,
    CONSTRAINT fk_pending_delivery_detail_product FOREIGN KEY (product_id) REFERENCES product(product_id)
);
CREATE INDEX IF NOT EXISTS idx_pending_delivery_detail_delivery ON pending_delivery_detail(pending_delivery_id);

-- El backend se conecta como inventrack: necesita permisos sobre las tablas y sus secuencias.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'inventrack') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON pending_receipt, pending_receipt_detail, pending_delivery, pending_delivery_detail TO inventrack;
        GRANT USAGE, SELECT ON SEQUENCE pending_receipt_pending_receipt_id_seq, pending_receipt_detail_pending_receipt_detail_id_seq,
            pending_delivery_pending_delivery_id_seq, pending_delivery_detail_pending_delivery_detail_id_seq TO inventrack;
    END IF;
END $$;

-- Menú principal, debajo de Entradas: ahí se entrega material y se une la factura. Lo ven los
-- mismos roles que Entradas. Los menús de abajo bajan un lugar (solo la primera vez).
UPDATE menu SET display_order = display_order + 1
WHERE parent_menu_id IS NULL AND display_order >= 3
  AND NOT EXISTS (SELECT 1 FROM menu WHERE name = 'Ingresos Pendientes de Factura' AND parent_menu_id IS NULL);

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
VALUES ('Ingresos Pendientes de Factura', 'nav.pendingReceipts', '/receipts/pending', 'pending', NULL, 3, TRUE, NULL)
ON CONFLICT DO NOTHING;

-- Si ya estaba dentro de Reportes (primera versión), sale de ahí.
UPDATE menu SET parent_menu_id = NULL, display_order = 3, icon = 'pending', translation_key = 'nav.pendingReceipts'
WHERE name = 'Ingresos Pendientes de Factura';

INSERT INTO menu_role (menu_id, role_id)
SELECT p.menu_id, mr.role_id
FROM menu p
JOIN menu e ON e.name = 'Entradas' AND e.parent_menu_id IS NULL
JOIN menu_role mr ON mr.menu_id = e.menu_id
WHERE p.name = 'Ingresos Pendientes de Factura'
ON CONFLICT DO NOTHING;

-- Reportes vuelve a su orden (sin este menú adentro).
UPDATE menu c SET display_order = o.display_order
FROM (VALUES
    ('Historial de Salidas', 8),
    ('Salidas de Almacén', 9),
    ('Transferencias de Almacén', 10),
    ('Campo Pastor', 11),
    ('Cartera de Cuentas por Cobrar', 12),
    ('Contabilidad', 13)
) AS o(name, display_order), menu p
WHERE c.name = o.name AND c.parent_menu_id = p.menu_id AND p.name = 'Reportes' AND p.parent_menu_id IS NULL;

COMMIT;

-- Comprobación
SELECT name AS menu, display_order FROM menu WHERE parent_menu_id IS NULL ORDER BY display_order;
