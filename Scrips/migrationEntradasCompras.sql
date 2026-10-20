-- =====================================================================
-- Entradas por motivo, formulario de compra, transferencias internas, N° de nota por mes,
-- donaciones, fotos de productos y revisión de documentos (2026-10-12).
--
-- Para bases que ya existen (local y servidor desplegado). Una base nueva ya trae las
-- columnas desde dataBase.sql y los menús desde seedMenus.sql + seedMenuRoles.sql.
-- Se puede correr varias veces: cada paso revisa antes de crear o cambiar.
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationEntradasCompras.sql
-- =====================================================================

BEGIN;

-- 1. Fecha de compra (de la factura o del recibo). issue_date pasa a ser la fecha de ingreso.
ALTER TABLE receipt ADD COLUMN IF NOT EXISTS purchase_date TIMESTAMP;

-- Hasta hoy la única fecha de una compra era la de emisión de la factura.
UPDATE receipt r
SET purchase_date = r.issue_date
FROM receipt_type rt
WHERE rt.receipt_type_id = r.receipt_type_id
  AND rt.name IN ('Compras con Factura', 'Compras con Retencion')
  AND r.purchase_date IS NULL;

-- 2. Precio de compra por línea. Con retención el costo unitario es precio x 0,92.
ALTER TABLE receipt_detail ADD COLUMN IF NOT EXISTS unit_price NUMERIC(12,2);

-- Las líneas anteriores se registraron con el costo igual al precio.
UPDATE receipt_detail SET unit_price = unit_cost WHERE unit_price IS NULL;

-- 3. Menús. Devoluciones y Ajuste de Inventario ahora se abren desde Entradas (junto a Compras y
--    Transferencias). Las transferencias son internas: ya no hay "Recibidas" de terceros.
UPDATE menu SET is_active = FALSE WHERE name IN ('Devoluciones', 'Ajuste de Inventario', 'Recibidas');

-- El historial de entradas (buscar y reimprimir una nota) pasa a Reportes.
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Historial de Entradas', 'nav.reportsReceiptHistory', '/reports/receipt-history', NULL, m.menu_id, 1, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

-- Lo ven los mismos roles que ya ven el reporte "Ingresos de Almacén".
INSERT INTO menu_role (menu_id, role_id)
SELECT h.menu_id, mr.role_id
FROM menu h
JOIN menu i ON i.name = 'Ingresos de Almacén'
JOIN menu_role mr ON mr.menu_id = i.menu_id
WHERE h.name = 'Historial de Entradas'
ON CONFLICT DO NOTHING;

-- 4. N° de nota por mes ("15-01" = nota 15 de enero), según la fecha de ingreso. Las notas que
--    ya existen se numeran en orden de fecha dentro de su mes.
ALTER TABLE receipt ADD COLUMN IF NOT EXISTS monthly_number INT;

UPDATE receipt r
SET monthly_number = n.rn
FROM (
    SELECT receipt_id,
           row_number() OVER (PARTITION BY date_trunc('month', issue_date) ORDER BY issue_date, receipt_id) AS rn
    FROM receipt
) n
WHERE n.receipt_id = r.receipt_id
  AND NOT EXISTS (SELECT 1 FROM receipt x WHERE x.monthly_number IS NOT NULL);

CREATE UNIQUE INDEX IF NOT EXISTS uq_receipt_monthly_number
    ON receipt (date_trunc('month', issue_date), monthly_number);

-- 5. Donaciones: nuevo motivo de entrada.
INSERT INTO receipt_type (name, is_active)
SELECT 'Donacion', TRUE
WHERE NOT EXISTS (SELECT 1 FROM receipt_type WHERE name = 'Donacion');

-- 6. Una foto por producto, guardada en la base.
CREATE TABLE IF NOT EXISTS product_image (
    product_id BIGINT PRIMARY KEY,
    content BYTEA NOT NULL,
    content_type VARCHAR(50) NOT NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_product_image_product FOREIGN KEY (product_id) REFERENCES product(product_id) ON DELETE CASCADE
);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'inventrack') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON product_image TO inventrack;
    END IF;
END $$;

-- 7. El sistema ya no es entre misiones: solo almacenes propios. Los almacenes de las misiones
--    se desactivan si no tienen stock (los que tengan stock quedan activos para revisarlos).
UPDATE warehouse w
SET is_active = FALSE
WHERE w.name IN ('ALMACEN CENTRAL MBON', 'ALMACEN CENTRAL UB', 'ALMACEN CENTRAL MOB', 'ALMACEN CENTRAL MBC')
  AND w.is_active
  AND NOT EXISTS (SELECT 1 FROM stock s WHERE s.warehouse_id = w.warehouse_id AND s.quantity <> 0);

-- 8. Revisión y firma digital de documentos (como el "Historial de Actualizaciones" de los
--    asientos contables): digitado -> verificado -> aprobado o rechazado; reabrir pide motivo y
--    empieza una versión nueva. Cada firma guarda la huella del contenido firmado.
--    El stock se sigue moviendo al digitar.
ALTER TABLE receipt ADD COLUMN IF NOT EXISTS review_status VARCHAR(20) NOT NULL DEFAULT 'digitado';
ALTER TABLE receipt ADD COLUMN IF NOT EXISTS review_version INT NOT NULL DEFAULT 1;
ALTER TABLE issue ADD COLUMN IF NOT EXISTS review_status VARCHAR(20) NOT NULL DEFAULT 'digitado';
ALTER TABLE issue ADD COLUMN IF NOT EXISTS review_version INT NOT NULL DEFAULT 1;
ALTER TABLE transfer ADD COLUMN IF NOT EXISTS review_status VARCHAR(20) NOT NULL DEFAULT 'digitado';
ALTER TABLE transfer ADD COLUMN IF NOT EXISTS review_version INT NOT NULL DEFAULT 1;

CREATE TABLE IF NOT EXISTS document_review (
    document_review_id BIGSERIAL PRIMARY KEY,
    document_type VARCHAR(20) NOT NULL,
    document_id BIGINT NOT NULL,
    version INT NOT NULL DEFAULT 1,
    action VARCHAR(20) NOT NULL,
    user_id BIGINT,
    at TIMESTAMP NOT NULL DEFAULT NOW(),
    reason VARCHAR(255),
    content_hash VARCHAR(64),
    CONSTRAINT fk_document_review_user FOREIGN KEY (user_id) REFERENCES app_user(app_user_id),
    CONSTRAINT ck_document_review_type CHECK (document_type IN ('receipt', 'issue', 'transfer'))
);
CREATE INDEX IF NOT EXISTS idx_document_review_document ON document_review(document_type, document_id);
ALTER TABLE document_review ADD COLUMN IF NOT EXISTS content_hash VARCHAR(64);
ALTER TABLE document_review DROP CONSTRAINT IF EXISTS ck_document_review_action;
ALTER TABLE document_review ADD CONSTRAINT ck_document_review_action
    CHECK (action IN ('digitado', 'verificado', 'aprobado', 'rechazado', 'reabierto', 'corregido'));

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'inventrack') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON document_review TO inventrack;
        GRANT USAGE, SELECT ON SEQUENCE document_review_document_review_id_seq TO inventrack;
    END IF;
END $$;

-- Los documentos que ya existen quedan "digitados", con quién y cuándo los registró.
INSERT INTO document_review (document_type, document_id, version, action, user_id, at)
SELECT 'receipt', r.receipt_id, 1, 'digitado', r.created_by, r.created_at
FROM receipt r
WHERE NOT EXISTS (SELECT 1 FROM document_review d WHERE d.document_type = 'receipt' AND d.document_id = r.receipt_id);

INSERT INTO document_review (document_type, document_id, version, action, user_id, at)
SELECT 'issue', i.issue_id, 1, 'digitado', i.created_by, i.created_at
FROM issue i
WHERE NOT EXISTS (SELECT 1 FROM document_review d WHERE d.document_type = 'issue' AND d.document_id = i.issue_id);

INSERT INTO document_review (document_type, document_id, version, action, user_id, at)
SELECT 'transfer', t.transfer_id, 1, 'digitado', t.sender_user_id, t.transfer_date
FROM transfer t
WHERE NOT EXISTS (SELECT 1 FROM document_review d WHERE d.document_type = 'transfer' AND d.document_id = t.transfer_id);

-- 9. Menú en el orden de la reunión: Entradas, Salidas, Transferencias, Reportes; después
--    Autorizaciones (las anulaciones juntas) y al final Terceros y Productos.
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Autorizaciones', 'nav.authorizations', NULL, 'void', NULL, 6, TRUE, NULL
WHERE NOT EXISTS (SELECT 1 FROM menu WHERE name = 'Autorizaciones');

UPDATE menu SET parent_menu_id = (SELECT menu_id FROM menu WHERE name = 'Autorizaciones'), display_order = 1, icon = NULL
WHERE name = 'Anulaciones';
UPDATE menu SET parent_menu_id = (SELECT menu_id FROM menu WHERE name = 'Autorizaciones'), display_order = 2, icon = NULL
WHERE name = 'Anulaciones de Entradas';

-- El grupo lo ven los roles que ven alguna de las dos anulaciones.
INSERT INTO menu_role (menu_id, role_id)
SELECT DISTINCT g.menu_id, mr.role_id
FROM menu g
JOIN menu c ON c.name IN ('Anulaciones', 'Anulaciones de Entradas')
JOIN menu_role mr ON mr.menu_id = c.menu_id
WHERE g.name = 'Autorizaciones'
ON CONFLICT DO NOTHING;

UPDATE menu SET display_order = o.display_order
FROM (VALUES
    ('Inicio', 1), ('Entradas', 2), ('Salidas', 3), ('Transferencias', 4), ('Reportes', 5),
    ('Autorizaciones', 6), ('Cuentas por Cobrar', 7), ('Mi Cuenta', 8), ('Catálogos', 9),
    ('Geografía', 10), ('Configuración del Sistema', 11), ('Administración', 12)
) AS o(name, display_order)
WHERE menu.name = o.name AND menu.parent_menu_id IS NULL;

-- 10. Reporte de Transferencias (los mismos roles que el de Ingresos).
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Transferencias de Almacén', 'nav.reportsTransfers', '/reports/transfers', NULL, m.menu_id, 11, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu_role (menu_id, role_id)
SELECT t.menu_id, mr.role_id
FROM menu t
JOIN menu i ON i.name = 'Ingresos de Almacén'
JOIN menu_role mr ON mr.menu_id = i.menu_id
WHERE t.name = 'Transferencias de Almacén'
ON CONFLICT DO NOTHING;

-- 11. Inventario inicial desde Excel: lo carga M-BOS, una sola vez por almacén.
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Inventario Inicial', 'nav.settingsInitialInventory', '/admin/initial-inventory', NULL, m.menu_id, 16, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.name = 'Inventario Inicial' AND r.name = 'M-BOS'
ON CONFLICT DO NOTHING;

-- 12. Proveedor adventista: distrito e iglesia en el propio proveedor ("¿Es adventista?"). Antes
--     solo existían si además se lo registraba como cliente; se copian de ese cliente.
ALTER TABLE supplier ADD COLUMN IF NOT EXISTS district_id BIGINT REFERENCES district(district_id);
ALTER TABLE supplier ADD COLUMN IF NOT EXISTS church_id BIGINT REFERENCES church(church_id);

UPDATE supplier s
SET district_id = c.district_id, church_id = c.church_id
FROM client c
WHERE c.party_id = s.party_id
  AND c.district_id IS NOT NULL
  AND s.district_id IS NULL;

-- 13. Correcciones de Entradas: se piden con motivo y las aprueba el Contador (Autorizaciones).
ALTER TABLE change_request DROP CONSTRAINT IF EXISTS chk_change_request_table;
ALTER TABLE change_request ADD CONSTRAINT chk_change_request_table
    CHECK (table_name IN ('receipt', 'issue', 'transfer', 'receipt_edit'));

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Correcciones de Entradas', 'nav.receiptEditRequests', '/receipts/edit-requests', NULL, m.menu_id, 3, TRUE, NULL
FROM menu m
WHERE m.name = 'Autorizaciones' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu_role (menu_id, role_id)
SELECT c.menu_id, mr.role_id
FROM menu c
JOIN menu a ON a.name = 'Anulaciones de Entradas'
JOIN menu_role mr ON mr.menu_id = a.menu_id
WHERE c.name = 'Correcciones de Entradas'
ON CONFLICT DO NOTHING;

-- 15. Anulación de Transferencias (se pide con motivo y la aprueba el Contador).
ALTER TABLE transfer ADD COLUMN IF NOT EXISTS is_voided BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS transfer_void_request (
    transfer_void_request_id BIGSERIAL PRIMARY KEY,
    transfer_id BIGINT NOT NULL,
    void_reason_id BIGINT NOT NULL,
    detail VARCHAR(255),
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    requested_by BIGINT NOT NULL,
    requested_at TIMESTAMP NOT NULL DEFAULT NOW(),
    reviewed_by BIGINT,
    reviewed_at TIMESTAMP,
    review_notes VARCHAR(255),
    CONSTRAINT fk_transfer_void_request_transfer FOREIGN KEY (transfer_id) REFERENCES transfer(transfer_id),
    CONSTRAINT fk_transfer_void_request_reason FOREIGN KEY (void_reason_id) REFERENCES void_reason(void_reason_id),
    CONSTRAINT fk_transfer_void_request_requested_by FOREIGN KEY (requested_by) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_transfer_void_request_reviewed_by FOREIGN KEY (reviewed_by) REFERENCES app_user(app_user_id),
    CONSTRAINT chk_transfer_void_request_status CHECK (status IN ('pending', 'approved', 'rejected'))
);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'inventrack') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON transfer_void_request TO inventrack;
        GRANT USAGE, SELECT ON SEQUENCE transfer_void_request_transfer_void_request_id_seq TO inventrack;
    END IF;
END $$;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Anulaciones de Transferencias', 'nav.transferVoidRequests', '/transfers/void-requests', NULL, m.menu_id, 3, TRUE, NULL
FROM menu m
WHERE m.name = 'Autorizaciones' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;
UPDATE menu SET display_order = 4 WHERE name = 'Correcciones de Entradas';

INSERT INTO menu_role (menu_id, role_id)
SELECT t.menu_id, mr.role_id
FROM menu t
JOIN menu a ON a.name = 'Anulaciones de Entradas'
JOIN menu_role mr ON mr.menu_id = a.menu_id
WHERE t.name = 'Anulaciones de Transferencias'
ON CONFLICT DO NOTHING;

-- 16. Los documentos que ya existían antes de la firma digital quedan "anterior": no llenan
--     los buzones de pendientes, pero se pueden verificar si alguien quiere. Solo la primera vez.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM configuration WHERE key = 'review_baseline') THEN
        UPDATE receipt SET review_status = 'anterior' WHERE review_status = 'digitado';
        UPDATE issue SET review_status = 'anterior' WHERE review_status = 'digitado';
        UPDATE transfer SET review_status = 'anterior' WHERE review_status = 'digitado';
        INSERT INTO configuration (key, value, description)
        VALUES ('review_baseline', to_char(NOW(), 'YYYY-MM-DD HH24:MI'),
                'Desde esta fecha los documentos pasan por firma digital; los anteriores quedan como "anterior".');
    END IF;
END $$;

-- 14. Buzón: notificaciones vistas por cada usuario y los procedimientos que arman el buzón.
CREATE TABLE IF NOT EXISTS inbox_seen (
    user_id BIGINT NOT NULL,
    notification_key VARCHAR(60) NOT NULL,
    seen_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_inbox_seen PRIMARY KEY (user_id, notification_key),
    CONSTRAINT fk_inbox_seen_user FOREIGN KEY (user_id) REFERENCES app_user(app_user_id) ON DELETE CASCADE
);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'inventrack') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON inbox_seen TO inventrack;
    END IF;
END $$;

-- =====================================================================
-- Buzón: pendientes por rol y notificaciones de cada usuario.
--   Pendientes (p_tab = 'pending'):
--     toVerify    documentos digitados por verificar y firmar (Contador, Tesorero, M-BOS)
--     toApprove   documentos verificados por aprobar (Contador, M-BOS)
--     voidRequest anulaciones de entradas, salidas y transferencias por autorizar (Contador, M-BOS)
--     receiptEdit correcciones de entradas por autorizar (Contador, M-BOS)
--   Notificaciones del usuario (p_tab = 'unread' o 'seen'):
--     cómo terminaron sus anulaciones y correcciones, y sus documentos rechazados.
--     Se marcan como vistas en inbox_seen (una fila por usuario y notificación).
-- =====================================================================
CREATE OR REPLACE FUNCTION sp_inbox_items(
    p_user_id BIGINT,
    p_can_verify BOOLEAN,
    p_can_approve BOOLEAN
)
RETURNS TABLE (
    tab VARCHAR,
    kind VARCHAR,
    document_type VARCHAR,
    document_id BIGINT,
    number TEXT,
    detail TEXT,
    status VARCHAR,
    at TIMESTAMP,
    notification_key TEXT
)
LANGUAGE sql
STABLE
AS $$
    WITH pending AS (
        SELECT 'toVerify'::varchar AS kind, 'receipt'::varchar AS document_type, r.receipt_id AS document_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') AS number,
               rt.name::text AS detail, NULL::varchar AS status, r.created_at AS at
        FROM receipt r LEFT JOIN receipt_type rt ON rt.receipt_type_id = r.receipt_type_id
        WHERE p_can_verify AND NOT r.is_voided AND r.review_status = 'digitado'
        UNION ALL
        SELECT 'toVerify', 'issue', i.issue_id, lpad(i.issue_id::text, 5, '0'), it.name::text, NULL, i.created_at
        FROM issue i LEFT JOIN issue_type it ON it.issue_type_id = i.issue_type_id
        WHERE p_can_verify AND NOT i.is_voided AND i.review_status = 'digitado'
        UNION ALL
        SELECT 'toVerify', 'transfer', t.transfer_id, lpad(t.transfer_id::text, 5, '0'),
               coalesce(ws.name, '—') || ' → ' || coalesce(wd.name, '—'), NULL, t.transfer_date
        FROM transfer t
        LEFT JOIN warehouse ws ON ws.warehouse_id = t.source_warehouse_id
        LEFT JOIN warehouse wd ON wd.warehouse_id = t.destination_warehouse_id
        WHERE p_can_verify AND NOT t.is_voided AND t.review_status = 'digitado'
        UNION ALL
        SELECT 'toApprove', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), rt.name::text, NULL, r.created_at
        FROM receipt r LEFT JOIN receipt_type rt ON rt.receipt_type_id = r.receipt_type_id
        WHERE p_can_approve AND NOT r.is_voided AND r.review_status = 'verificado'
        UNION ALL
        SELECT 'toApprove', 'issue', i.issue_id, lpad(i.issue_id::text, 5, '0'), it.name::text, NULL, i.created_at
        FROM issue i LEFT JOIN issue_type it ON it.issue_type_id = i.issue_type_id
        WHERE p_can_approve AND NOT i.is_voided AND i.review_status = 'verificado'
        UNION ALL
        SELECT 'toApprove', 'transfer', t.transfer_id, lpad(t.transfer_id::text, 5, '0'),
               coalesce(ws.name, '—') || ' → ' || coalesce(wd.name, '—'), NULL, t.transfer_date
        FROM transfer t
        LEFT JOIN warehouse ws ON ws.warehouse_id = t.source_warehouse_id
        LEFT JOIN warehouse wd ON wd.warehouse_id = t.destination_warehouse_id
        WHERE p_can_approve AND NOT t.is_voided AND t.review_status = 'verificado'
        UNION ALL
        SELECT 'voidRequest', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), v.detail::text, NULL, v.requested_at
        FROM receipt_void_request v JOIN receipt r ON r.receipt_id = v.receipt_id
        WHERE p_can_approve AND v.status = 'pending'
        UNION ALL
        SELECT 'voidRequest', 'issue', v.issue_id, lpad(v.issue_id::text, 5, '0'), v.detail::text, NULL, v.requested_at
        FROM issue_void_request v
        WHERE p_can_approve AND v.status = 'pending'
        UNION ALL
        SELECT 'voidRequest', 'transfer', v.transfer_id, lpad(v.transfer_id::text, 5, '0'), v.detail::text, NULL, v.requested_at
        FROM transfer_void_request v
        WHERE p_can_approve AND v.status = 'pending'
        UNION ALL
        SELECT 'receiptEdit', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), c.reason::text, NULL, c.requested_at
        FROM change_request c JOIN receipt r ON r.receipt_id = c.record_id
        WHERE p_can_approve AND c.table_name = 'receipt_edit' AND c.status = 'pending'
    ),
    -- Respuestas a lo que pidió o digitó el usuario (anulaciones, correcciones y la firma final
    -- de sus documentos, aprobados o rechazados).
    notifications AS (
        SELECT 'voidRequest'::varchar AS kind, 'receipt'::varchar AS document_type, r.receipt_id AS document_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') AS number,
               v.review_notes::text AS detail, v.status AS status, v.reviewed_at AS at,
               'receipt_void:' || v.receipt_void_request_id AS notification_key
        FROM receipt_void_request v JOIN receipt r ON r.receipt_id = v.receipt_id
        WHERE v.requested_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'voidRequest', 'issue', v.issue_id, lpad(v.issue_id::text, 5, '0'), v.review_notes::text, v.status, v.reviewed_at,
               'issue_void:' || v.issue_void_request_id
        FROM issue_void_request v
        WHERE v.requested_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'voidRequest', 'transfer', v.transfer_id, lpad(v.transfer_id::text, 5, '0'), v.review_notes::text, v.status, v.reviewed_at,
               'transfer_void:' || v.transfer_void_request_id
        FROM transfer_void_request v
        WHERE v.requested_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'receiptEdit', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), c.review_notes::text, c.status, c.reviewed_at,
               'receipt_edit:' || c.change_request_id
        FROM change_request c JOIN receipt r ON r.receipt_id = c.record_id
        WHERE c.table_name = 'receipt_edit' AND c.requested_by = p_user_id AND c.status <> 'pending'
        UNION ALL
        -- Documentos que digitó el usuario y el Contador aprobó o rechazó.
        SELECT CASE WHEN d.action = 'aprobado' THEN 'reviewApproved' ELSE 'reviewRejected' END::varchar,
               d.document_type, d.document_id,
               CASE WHEN d.document_type = 'receipt'
                    THEN (SELECT 'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') FROM receipt r WHERE r.receipt_id = d.document_id)
                    ELSE lpad(d.document_id::text, 5, '0') END,
               d.reason::text,
               CASE WHEN d.action = 'aprobado' THEN 'approved' ELSE 'rejected' END::varchar,
               d.at,
               'review_' || CASE WHEN d.action = 'aprobado' THEN 'approved' ELSE 'rejected' END || ':' || d.document_review_id
        FROM document_review d
        WHERE d.action IN ('aprobado', 'rechazado')
          AND d.user_id <> p_user_id
          AND EXISTS (
              SELECT 1 FROM document_review e
              WHERE e.document_type = d.document_type AND e.document_id = d.document_id
                AND e.action = 'digitado' AND e.user_id = p_user_id)
    ),
    -- Lo que firmó o autorizó el propio usuario.
    signed AS (
        SELECT 'reviewSigned'::varchar AS kind, d.document_type::varchar AS document_type, d.document_id AS document_id,
               CASE WHEN d.document_type = 'receipt'
                    THEN (SELECT 'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') FROM receipt r WHERE r.receipt_id = d.document_id)
                    ELSE lpad(d.document_id::text, 5, '0') END AS number,
               d.reason::text AS detail, d.action::varchar AS status, d.at AS at
        FROM document_review d
        WHERE d.user_id = p_user_id AND d.action IN ('verificado', 'aprobado', 'rechazado', 'reabierto')
        UNION ALL
        SELECT 'voidRequest', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), v.review_notes::text, v.status, v.reviewed_at
        FROM receipt_void_request v JOIN receipt r ON r.receipt_id = v.receipt_id
        WHERE v.reviewed_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'voidRequest', 'issue', v.issue_id, lpad(v.issue_id::text, 5, '0'), v.review_notes::text, v.status, v.reviewed_at
        FROM issue_void_request v
        WHERE v.reviewed_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'voidRequest', 'transfer', v.transfer_id, lpad(v.transfer_id::text, 5, '0'), v.review_notes::text, v.status, v.reviewed_at
        FROM transfer_void_request v
        WHERE v.reviewed_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'receiptEdit', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), c.review_notes::text, c.status, c.reviewed_at
        FROM change_request c JOIN receipt r ON r.receipt_id = c.record_id
        WHERE c.table_name = 'receipt_edit' AND c.reviewed_by = p_user_id AND c.status <> 'pending'
    )
    SELECT 'pending'::varchar, p.kind, p.document_type, p.document_id, p.number, p.detail, p.status, p.at, NULL::text
    FROM pending p
    UNION ALL
    SELECT CASE WHEN s.notification_key IS NULL THEN 'unread' ELSE 'seen' END::varchar,
           n.kind, n.document_type, n.document_id, n.number, n.detail, n.status, n.at, n.notification_key
    FROM notifications n
    LEFT JOIN inbox_seen s ON s.user_id = p_user_id AND s.notification_key = n.notification_key
    UNION ALL
    SELECT 'signed'::varchar, g.kind, g.document_type, g.document_id, g.number, g.detail, g.status, g.at, NULL::text
    FROM signed g;
$$;

-- Una página del buzón con filtros: pestaña, clase, documento, N° y fechas (día de Bolivia).
CREATE OR REPLACE FUNCTION sp_get_inbox(
    p_user_id BIGINT,
    p_can_verify BOOLEAN,
    p_can_approve BOOLEAN,
    p_tab VARCHAR,
    p_kind VARCHAR,
    p_document_type VARCHAR,
    p_search VARCHAR,
    p_date_from DATE,
    p_date_to DATE,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    kind VARCHAR,
    document_type VARCHAR,
    document_id BIGINT,
    number TEXT,
    detail TEXT,
    status VARCHAR,
    at TIMESTAMP,
    notification_key TEXT,
    seen BOOLEAN,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    -- p_tab: 'pending', 'answers' (respuestas: sin leer primero, luego las vistas) o 'signed'.
    SELECT i.kind, i.document_type, i.document_id, i.number, i.detail, i.status, i.at, i.notification_key,
           i.tab = 'seen', count(*) OVER ()::int
    FROM sp_inbox_items(p_user_id, p_can_verify, p_can_approve) i
    WHERE (CASE WHEN p_tab = 'answers' THEN i.tab IN ('unread', 'seen') ELSE i.tab = p_tab END)
      AND (p_kind IS NULL OR i.kind = p_kind)
      AND (p_document_type IS NULL OR i.document_type = p_document_type)
      AND (p_search IS NULL OR i.number ILIKE '%' || p_search || '%' OR i.detail ILIKE '%' || p_search || '%')
      AND (p_date_from IS NULL OR (i.at AT TIME ZONE 'UTC' AT TIME ZONE 'America/La_Paz')::date >= p_date_from)
      AND (p_date_to IS NULL OR (i.at AT TIME ZONE 'UTC' AT TIME ZONE 'America/La_Paz')::date <= p_date_to)
    ORDER BY (i.tab = 'unread') DESC, i.at DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- Cuántos hay en cada pestaña y por clase (para el contador del menú y las tarjetas).
CREATE OR REPLACE FUNCTION sp_get_inbox_counts(
    p_user_id BIGINT,
    p_can_verify BOOLEAN,
    p_can_approve BOOLEAN
)
RETURNS TABLE (
    tab VARCHAR,
    kind VARCHAR,
    total INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT i.tab, i.kind, count(*)::int
    FROM sp_inbox_items(p_user_id, p_can_verify, p_can_approve) i
    GROUP BY i.tab, i.kind;
$$;

-- Marca notificaciones como vistas (o todas las sin leer si p_keys es NULL). Devuelve cuántas.
CREATE OR REPLACE FUNCTION sp_mark_inbox_seen(
    p_user_id BIGINT,
    p_keys TEXT[]
)
RETURNS TABLE (marked INT)
LANGUAGE sql
VOLATILE
AS $$
    WITH inserted AS (
        INSERT INTO inbox_seen (user_id, notification_key, seen_at)
        SELECT p_user_id, i.notification_key, NOW()
        FROM sp_inbox_items(p_user_id, FALSE, FALSE) i
        WHERE i.tab = 'unread'
          AND (p_keys IS NULL OR i.notification_key = ANY (p_keys))
        ON CONFLICT DO NOTHING
        RETURNING 1
    )
    SELECT count(*)::int FROM inserted;
$$;

COMMIT;

-- Comprobación: debe mostrar las dos columnas nuevas y el menú del historial.
SELECT
    (SELECT count(*) FROM information_schema.columns WHERE table_name = 'receipt' AND column_name = 'purchase_date') AS receipt_purchase_date,
    (SELECT count(*) FROM information_schema.columns WHERE table_name = 'receipt_detail' AND column_name = 'unit_price') AS receipt_detail_unit_price,
    (SELECT count(*) FROM menu WHERE name = 'Historial de Entradas') AS menu_historial,
    (SELECT count(*) FROM menu_role mr JOIN menu m ON m.menu_id = mr.menu_id WHERE m.name = 'Historial de Entradas') AS roles_historial,
    (SELECT count(*) FROM receipt WHERE monthly_number IS NULL) AS notas_sin_numero,
    (SELECT count(*) FROM receipt_type WHERE name = 'Donacion') AS tipo_donacion,
    (SELECT count(*) FROM information_schema.tables WHERE table_name = 'product_image') AS tabla_fotos,
    (SELECT count(*) FROM document_review) AS historial_revision;

-- Menú principal en su nuevo orden.
SELECT display_order, name FROM menu WHERE parent_menu_id IS NULL AND is_active ORDER BY display_order;

-- Almacenes que quedan activos (si aparece uno de misión es porque todavía tiene stock).
SELECT warehouse_id, name, is_active FROM warehouse ORDER BY is_active DESC, name;
