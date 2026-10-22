-- =====================================================================================
-- Cambios de la revisión (2026-10-20). Se puede correr más de una vez.
-- Correr DESPUÉS de migrationContabilidad.sql y ANTES de levantar el backend nuevo.
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationRevision1.sql
-- =====================================================================================

BEGIN;

-- 1. Menú: Reportes / Contabilidad, con Salidas e Ingresos para Contabilidad adentro.
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Contabilidad', 'nav.reportsAccounting', NULL, NULL, m.menu_id, 12, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

UPDATE menu SET parent_menu_id = (SELECT menu_id FROM menu WHERE name = 'Contabilidad'), display_order = 1
WHERE name = 'Salidas para Contabilidad';
UPDATE menu SET parent_menu_id = (SELECT menu_id FROM menu WHERE name = 'Contabilidad'), display_order = 2
WHERE name = 'Ingresos para Contabilidad';

-- El grupo lo ven los roles que ven alguno de sus dos reportes.
INSERT INTO menu_role (menu_id, role_id)
SELECT DISTINCT g.menu_id, mr.role_id
FROM menu g
JOIN menu c ON c.parent_menu_id = g.menu_id
JOIN menu_role mr ON mr.menu_id = c.menu_id
WHERE g.name = 'Contabilidad'
ON CONFLICT DO NOTHING;

-- 1b. Menú Reportes ordenado por grupos (los que se complementan, juntos): saldos (Stock -
--     Almacén, Stock - Departamento, Levantamiento), kardex, entradas (historial y reporte),
--     salidas (historial y reporte), traspasos, pastores y cobros, y al final Contabilidad.
UPDATE menu c SET display_order = o.display_order
FROM (VALUES
    ('Stock - Almacén', 1),
    ('Stock - Departamento', 2),
    ('Levantamiento de Inventario', 3),
    ('Kardex Físico Valorado', 4),
    ('Kardex por Material', 5),
    ('Historial de Entradas', 6),
    ('Ingresos de Almacén', 7),
    ('Historial de Salidas', 8),
    ('Salidas de Almacén', 9),
    ('Transferencias de Almacén', 10),
    ('Campo Pastor', 11),
    ('Cartera de Cuentas por Cobrar', 12),
    ('Contabilidad', 13)
) AS o(name, display_order), menu p
WHERE c.name = o.name AND c.parent_menu_id = p.menu_id AND p.name = 'Reportes' AND p.parent_menu_id IS NULL;

-- 2. N° de nota de ingreso: "I 15-10-26" en vez de "MB 15-10-26" (las salidas ya son "S ...").
--    Se rehacen las funciones que arman el número, cambiando solo el prefijo.
DO $$
DECLARE
    fn RECORD;
BEGIN
    FOR fn IN
        SELECT p.oid
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'public' AND p.prosrc LIKE '%''MB ''%'
    LOOP
        EXECUTE replace(pg_get_functiondef(fn.oid), '''MB ''', '''I ''');
    END LOOP;
END $$;

-- 3. Compras con retención: el costo es el precio DIVIDIDO entre 0,92 (antes se multiplicaba).
--    Se recalculan las líneas ya registradas y el costo promedio del stock se corrige con la
--    diferencia de valor (como al aprobar una corrección de entrada).
WITH changed AS (
    SELECT d.receipt_detail_id, r.warehouse_id, d.product_id, r.is_voided,
           round(d.unit_price / 0.92, 2) AS new_unit_cost,
           round(d.quantity * d.unit_price / 0.92, 2) AS new_total_cost,
           d.total_cost AS old_total_cost
    FROM receipt_detail d
    JOIN receipt r ON r.receipt_id = d.receipt_id
    JOIN receipt_type t ON t.receipt_type_id = r.receipt_type_id
    WHERE t.name = 'Compras con Retencion'
      AND d.unit_price IS NOT NULL
      AND (d.unit_cost <> round(d.unit_price / 0.92, 2) OR d.total_cost <> round(d.quantity * d.unit_price / 0.92, 2))
),
stock_fix AS (
    UPDATE stock s
    SET average_cost = greatest(s.quantity * s.average_cost + c.delta, 0) / s.quantity
    FROM (
        SELECT warehouse_id, product_id, sum(new_total_cost - old_total_cost) AS delta
        FROM changed
        WHERE NOT is_voided
        GROUP BY warehouse_id, product_id
    ) c
    WHERE s.warehouse_id = c.warehouse_id AND s.product_id = c.product_id AND s.quantity > 0
    RETURNING s.stock_id
)
UPDATE receipt_detail d
SET unit_cost = c.new_unit_cost, total_cost = c.new_total_cost
FROM changed c
WHERE d.receipt_detail_id = c.receipt_detail_id;

-- 4. Salida por lote: un lote agrupa una salida por pastor, todas con la misma fecha, el mismo
--    almacén y las mismas condiciones de cobro. N° de lote por mes ("L 3-10-26").
CREATE TABLE IF NOT EXISTS issue_batch (
    issue_batch_id BIGSERIAL PRIMARY KEY,
    monthly_number INT NOT NULL,
    batch_date TIMESTAMP NOT NULL,
    warehouse_id BIGINT NOT NULL,
    issue_type_id BIGINT,
    description VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by BIGINT,
    CONSTRAINT fk_issue_batch_warehouse FOREIGN KEY (warehouse_id) REFERENCES warehouse(warehouse_id),
    CONSTRAINT fk_issue_batch_issue_type FOREIGN KEY (issue_type_id) REFERENCES issue_type(issue_type_id),
    CONSTRAINT fk_issue_batch_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id)
);

ALTER TABLE issue ADD COLUMN IF NOT EXISTS issue_batch_id BIGINT;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_issue_issue_batch') THEN
        ALTER TABLE issue ADD CONSTRAINT fk_issue_issue_batch FOREIGN KEY (issue_batch_id) REFERENCES issue_batch(issue_batch_id);
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'inventrack') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON issue_batch TO inventrack;
        GRANT USAGE, SELECT ON SEQUENCE issue_batch_issue_batch_id_seq TO inventrack;
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS idx_issue_issue_batch ON issue(issue_batch_id);

-- 5. Una salida por lote lleva UN solo N° ("S 5-10-26"): todas sus filas (una por pastor) lo
--    comparten. El N° sigue sin repetirse entre salidas sueltas; las de un lote se controlan
--    por el lote.
DROP INDEX IF EXISTS uq_issue_monthly_number;
CREATE UNIQUE INDEX uq_issue_monthly_number
    ON issue (date_trunc('month', issue_date), monthly_number)
    WHERE issue_batch_id IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_issue_batch_monthly_number
    ON issue_batch (date_trunc('month', batch_date), monthly_number);

-- 5b. Tipo de salida "Salida por Lote": las tarjetas de Salidas salen de esta tabla (la misma
--     que usan los filtros), así que el lote también es un tipo. Las salidas por lote ya
--     guardadas quedan con ese tipo.
INSERT INTO issue_type (name, is_active)
SELECT 'Salida por Lote', TRUE
WHERE NOT EXISTS (SELECT 1 FROM issue_type WHERE name = 'Salida por Lote');

UPDATE issue_batch SET issue_type_id = (SELECT issue_type_id FROM issue_type WHERE name = 'Salida por Lote')
WHERE issue_type_id IS NULL;
UPDATE issue SET issue_type_id = (SELECT issue_type_id FROM issue_type WHERE name = 'Salida por Lote')
WHERE issue_batch_id IS NOT NULL AND issue_type_id IS NULL;

-- Regla de "Actualizado" (select Estado de los reportes): actualizado = la versión vigente ya
-- tiene firma (verificado o aprobado) o es anterior a la firma digital (ya era oficial antes de
-- las firmas); desactualizado = digitado o reabierto. Con "Actualizado" los reportes mandan
-- 'updated' y cuentan solo lo actualizado; con "Desactualizado" mandan NULL y cuentan todo.
-- Quién puede firmar se controla al firmar, así que aquí basta con el estado.
DROP FUNCTION IF EXISTS fn_review_matches(VARCHAR, BIGINT, INT, VARCHAR, VARCHAR);
CREATE OR REPLACE FUNCTION fn_review_matches(p_status VARCHAR, p_state VARCHAR)
RETURNS BOOLEAN
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT p_state IS NULL
        OR (p_state = 'updated' AND p_status IN ('verificado', 'aprobado', 'anterior'))
        OR (p_state = 'outdated' AND p_status NOT IN ('verificado', 'aprobado', 'anterior'));
$$;

-- Cantidad de un producto en un almacén hasta una fecha, contando solo los movimientos de ese
-- estado de firma (lo usa el Levantamiento de Inventario cuando se filtra por firma).
CREATE OR REPLACE FUNCTION fn_review_quantity(p_product_id BIGINT, p_warehouse_id BIGINT, p_date_to DATE, p_state VARCHAR)
RETURNS NUMERIC
LANGUAGE sql
STABLE
AS $$
    SELECT
        COALESCE((SELECT SUM(rd.quantity) FROM receipt_detail rd JOIN receipt r ON r.receipt_id = rd.receipt_id
                  WHERE rd.product_id = p_product_id AND NOT r.is_voided AND fn_review_matches(r.review_status, p_state)
                    AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
                    AND (p_date_to IS NULL OR r.issue_date < p_date_to + 1)), 0)
      - COALESCE((SELECT SUM(idt.quantity) FROM issue_detail idt JOIN issue i ON i.issue_id = idt.issue_id
                  WHERE idt.product_id = p_product_id AND NOT i.is_voided AND fn_review_matches(i.review_status, p_state)
                    AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
                    AND (p_date_to IS NULL OR i.issue_date < p_date_to + 1)), 0)
      + COALESCE((SELECT SUM(td.quantity) FROM transfer_detail td JOIN transfer t ON t.transfer_id = td.transfer_id
                  WHERE td.product_id = p_product_id AND t.source_warehouse_id IS NOT NULL
                    AND fn_review_matches(t.review_status, p_state)
                    AND (p_warehouse_id IS NULL OR t.destination_warehouse_id = p_warehouse_id)
                    AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)), 0)
      - COALESCE((SELECT SUM(td.quantity) FROM transfer_detail td JOIN transfer t ON t.transfer_id = td.transfer_id
                  WHERE td.product_id = p_product_id AND fn_review_matches(t.review_status, p_state)
                    AND (p_warehouse_id IS NULL OR t.source_warehouse_id = p_warehouse_id)
                    AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)), 0);
$$;

-- 6. Historiales de Salidas y Entradas:
--    a) Buscar el N° de una salida por lote trae a todos sus pastores.
--    b) Filtro "Actualizado / Desactualizado" (con firma: verificado o aprobado): los
--       procedimientos reciben un parámetro más. Se borra la versión anterior para que no
--       queden dos con el mismo nombre.
DROP FUNCTION IF EXISTS sp_get_issues(BIGINT, BIGINT, BIGINT, BIGINT, INT, INT, VARCHAR);
DROP FUNCTION IF EXISTS sp_get_receipts(BIGINT, BIGINT, BIGINT, BIGINT, VARCHAR, INT, INT, VARCHAR);

CREATE OR REPLACE FUNCTION sp_get_issues(
    p_issue_id BIGINT,
    p_client_id BIGINT,
    p_warehouse_id BIGINT,
    p_issue_type_id BIGINT,
    p_page_number INT,
    p_page_size INT,
    p_client_name VARCHAR DEFAULT NULL,
    p_review_state VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    issue_id BIGINT,
    issue_type_id BIGINT,
    warehouse_id BIGINT,
    warehouse_period_id BIGINT,
    client_id BIGINT,
    complement VARCHAR,
    issue_date TIMESTAMP,
    print_type_id BIGINT,
    description VARCHAR,
    created_at TIMESTAMP,
    created_by_id BIGINT,
    created_by_full_name TEXT,
    has_pending_change_request BOOLEAN,
    total NUMERIC,
    is_voided BOOLEAN,
    has_pending_void_request BOOLEAN,
    void_reason_name TEXT,
    void_detail VARCHAR,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        i.issue_id,
        i.issue_type_id,
        i.warehouse_id,
        i.warehouse_period_id,
        i.client_id,
        i.complement,
        i.issue_date,
        i.print_type_id,
        i.description,
        i.created_at,
        i.created_by AS created_by_id,
        (au.first_name || ' ' || au.last_name) AS created_by_full_name,
        EXISTS (
            SELECT 1 FROM change_request cr
            WHERE cr.table_name = 'issue' AND cr.status = 'pending' AND cr.record_id = i.issue_id
        ) AS has_pending_change_request,
        COALESCE(det.total, 0) AS total,
        i.is_voided,
        EXISTS (
            SELECT 1 FROM issue_void_request vr
            WHERE vr.status = 'pending' AND vr.issue_id = i.issue_id
        ) AS has_pending_void_request,
        vctx.void_reason_name,
        vctx.detail AS void_detail,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM issue i
    LEFT JOIN app_user au ON au.app_user_id = i.created_by
    LEFT JOIN client c ON c.client_id = i.client_id
    LEFT JOIN party cp ON cp.party_id = c.party_id
    LEFT JOIN LATERAL (
        SELECT SUM(d.total_cost) AS total
        FROM issue_detail d
        WHERE d.issue_id = i.issue_id
    ) det ON true
    LEFT JOIN LATERAL (
        -- Más reciente de las anulaciones pendientes/aprobadas de esta salida.
        -- (El código original en C# asumía que solo existe una relevante a la vez.)
        SELECT vr2.detail, vru.name AS void_reason_name
        FROM issue_void_request vr2
        JOIN void_reason vru ON vru.void_reason_id = vr2.void_reason_id
        WHERE vr2.issue_id = i.issue_id AND vr2.status IN ('pending', 'approved')
        ORDER BY vr2.issue_void_request_id DESC
        LIMIT 1
    ) vctx ON true
    WHERE (p_issue_id IS NULL OR i.issue_id = p_issue_id
           -- Salida por lote: el mismo N° es de todos sus pastores.
           OR i.issue_batch_id = (SELECT x.issue_batch_id FROM issue x WHERE x.issue_id = p_issue_id))
      AND (p_client_id IS NULL OR i.client_id = p_client_id)
      AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
      AND (p_issue_type_id IS NULL OR i.issue_type_id = p_issue_type_id)
      AND (p_client_name IS NULL OR cp.name ILIKE '%' || p_client_name || '%')
      AND fn_review_matches(i.review_status, p_review_state)
    ORDER BY i.issue_date DESC, i.issue_id DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

CREATE OR REPLACE FUNCTION sp_get_receipts(
    p_receipt_id BIGINT,
    p_supplier_id BIGINT,
    p_warehouse_id BIGINT,
    p_receipt_type_id BIGINT,
    p_invoice_number VARCHAR,
    p_page_number INT,
    p_page_size INT,
    p_supplier_name VARCHAR DEFAULT NULL,
    p_review_state VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    receipt_id BIGINT,
    supplier_id BIGINT,
    tax_id VARCHAR,
    warehouse_id BIGINT,
    warehouse_period_id BIGINT,
    receipt_type_id BIGINT,
    invoice_number VARCHAR,
    description VARCHAR,
    issue_date TIMESTAMP,
    invoice_total NUMERIC,
    created_at TIMESTAMP,
    created_by_id BIGINT,
    created_by_full_name TEXT,
    has_pending_change_request BOOLEAN,
    is_voided BOOLEAN,
    has_pending_void_request BOOLEAN,
    void_reason_name TEXT,
    void_detail VARCHAR,
    related_issue_id BIGINT,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        r.receipt_id,
        r.supplier_id,
        r.tax_id,
        r.warehouse_id,
        r.warehouse_period_id,
        r.receipt_type_id,
        r.invoice_number,
        r.description,
        r.issue_date,
        r.invoice_total,
        r.created_at,
        r.created_by AS created_by_id,
        (au.first_name || ' ' || au.last_name) AS created_by_full_name,
        EXISTS (
            SELECT 1 FROM change_request cr
            WHERE cr.table_name = 'receipt' AND cr.status = 'pending' AND cr.record_id = r.receipt_id
        ) AS has_pending_change_request,
        r.is_voided,
        EXISTS (
            SELECT 1 FROM receipt_void_request vr
            WHERE vr.status = 'pending' AND vr.receipt_id = r.receipt_id
        ) AS has_pending_void_request,
        vctx.void_reason_name,
        vctx.detail AS void_detail,
        r.related_issue_id,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM receipt r
    LEFT JOIN app_user au ON au.app_user_id = r.created_by
    LEFT JOIN supplier s ON s.supplier_id = r.supplier_id
    LEFT JOIN party sp ON sp.party_id = s.party_id
    LEFT JOIN LATERAL (
        -- Más reciente de las anulaciones pendientes/aprobadas de esta entrada.
        SELECT vr2.detail, vru.name AS void_reason_name
        FROM receipt_void_request vr2
        JOIN void_reason vru ON vru.void_reason_id = vr2.void_reason_id
        WHERE vr2.receipt_id = r.receipt_id AND vr2.status IN ('pending', 'approved')
        ORDER BY vr2.receipt_void_request_id DESC
        LIMIT 1
    ) vctx ON true
    WHERE (p_receipt_id IS NULL OR r.receipt_id = p_receipt_id)
      AND (p_supplier_id IS NULL OR r.supplier_id = p_supplier_id)
      AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
      AND (p_receipt_type_id IS NULL OR r.receipt_type_id = p_receipt_type_id)
      AND (p_invoice_number IS NULL OR r.invoice_number = p_invoice_number)
      AND (p_supplier_name IS NULL OR sp.name ILIKE '%' || p_supplier_name || '%')
      AND fn_review_matches(r.review_status, p_review_state)
    ORDER BY r.issue_date DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- 8. Filtro "Actualizado / Desactualizado" en todos los reportes de inventario (stock, kardex,
--    levantamiento, ingresos y salidas): reciben un parámetro más. Se borra la versión
--    anterior de cada uno para que no queden dos con el mismo nombre.
DROP FUNCTION IF EXISTS sp_get_stock_report(BIGINT, DATE, DATE);
DROP FUNCTION IF EXISTS sp_get_stock_report_by_department(BIGINT, BIGINT, DATE, DATE);
DROP FUNCTION IF EXISTS sp_get_kardex_report(BIGINT, DATE, DATE);
DROP FUNCTION IF EXISTS sp_get_inventory_count_report(BIGINT, DATE, DATE);
DROP FUNCTION IF EXISTS sp_get_issues_report(BIGINT, DATE, DATE);
DROP FUNCTION IF EXISTS sp_get_receipts_report(BIGINT, DATE, DATE);
DROP FUNCTION IF EXISTS sp_get_kardex_by_product_report(BIGINT, BIGINT, DATE, DATE);
DROP FUNCTION IF EXISTS sp_get_kardex_by_product_opening_balance(BIGINT, BIGINT, DATE);

CREATE OR REPLACE FUNCTION sp_get_stock_report(
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE,
    p_review_state VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_id BIGINT,
    department_name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    net_quantity NUMERIC,
    unit_value NUMERIC,
    total_value NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH receipts AS (
        SELECT rd.product_id,
               SUM(rd.quantity) AS qty,
               SUM(rd.quantity * rd.unit_cost) AS cost_total
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE NOT r.is_voided AND fn_review_matches(r.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR r.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR r.issue_date < p_date_to + 1)
        GROUP BY rd.product_id
    ),
    issues AS (
        SELECT idt.product_id,
               SUM(idt.quantity) AS qty
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE NOT i.is_voided AND fn_review_matches(i.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR i.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR i.issue_date < p_date_to + 1)
        GROUP BY idt.product_id
    ),
    transfers_in AS (
        -- Solo transferencias internas (con almacén de origen) llegan a tocar
        -- stock.quantity de verdad (ver TransferService.InsertTransfer) — una
        -- transferencia externa (source_warehouse_id IS NULL) queda solo como
        -- registro pendiente de revisión y nunca incrementa el stock destino.
        SELECT td.product_id,
               SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE fn_review_matches(t.review_status, p_review_state)
          AND t.source_warehouse_id IS NOT NULL
          AND (p_warehouse_id IS NULL OR t.destination_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    ),
    transfers_out AS (
        SELECT td.product_id,
               SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE fn_review_matches(t.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR t.source_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    )
    SELECT
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        COALESCE(r.qty, 0) - COALESCE(iss.qty, 0) + COALESCE(ti.qty, 0) - COALESCE(tou.qty, 0) AS net_quantity,
        CASE WHEN COALESCE(r.qty, 0) = 0 THEN 0 ELSE r.cost_total / r.qty END AS unit_value,
        (COALESCE(r.qty, 0) - COALESCE(iss.qty, 0) + COALESCE(ti.qty, 0) - COALESCE(tou.qty, 0))
            * (CASE WHEN COALESCE(r.qty, 0) = 0 THEN 0 ELSE r.cost_total / r.qty END) AS total_value
    FROM product p
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    LEFT JOIN receipts r ON r.product_id = p.product_id
    LEFT JOIN issues iss ON iss.product_id = p.product_id
    LEFT JOIN transfers_in ti ON ti.product_id = p.product_id
    LEFT JOIN transfers_out tou ON tou.product_id = p.product_id
    WHERE r.product_id IS NOT NULL OR iss.product_id IS NOT NULL
       OR ti.product_id IS NOT NULL OR tou.product_id IS NOT NULL
    ORDER BY d.name, sd.name, p.name;
$$;

CREATE OR REPLACE FUNCTION sp_get_stock_report_by_department(
    p_warehouse_id BIGINT,
    p_department_id BIGINT,
    p_date_from DATE,
    p_date_to DATE,
    p_review_state VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_id BIGINT,
    department_name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    net_quantity NUMERIC,
    unit_value NUMERIC,
    total_value NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH receipts AS (
        SELECT rd.product_id,
               SUM(rd.quantity) AS qty,
               SUM(rd.quantity * rd.unit_cost) AS cost_total
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE NOT r.is_voided AND fn_review_matches(r.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR r.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR r.issue_date < p_date_to + 1)
        GROUP BY rd.product_id
    ),
    issues AS (
        SELECT idt.product_id,
               SUM(idt.quantity) AS qty
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE NOT i.is_voided AND fn_review_matches(i.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR i.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR i.issue_date < p_date_to + 1)
        GROUP BY idt.product_id
    ),
    transfers_in AS (
        -- Solo transferencias internas (con almacén de origen) tocan stock.quantity de
        -- verdad — ver el mismo comentario en sp_get_stock_report.
        SELECT td.product_id,
               SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE fn_review_matches(t.review_status, p_review_state)
          AND t.source_warehouse_id IS NOT NULL
          AND (p_warehouse_id IS NULL OR t.destination_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    ),
    transfers_out AS (
        SELECT td.product_id,
               SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE fn_review_matches(t.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR t.source_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    )
    SELECT
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        COALESCE(r.qty, 0) - COALESCE(iss.qty, 0) + COALESCE(ti.qty, 0) - COALESCE(tou.qty, 0) AS net_quantity,
        CASE WHEN COALESCE(r.qty, 0) = 0 THEN 0 ELSE r.cost_total / r.qty END AS unit_value,
        (COALESCE(r.qty, 0) - COALESCE(iss.qty, 0) + COALESCE(ti.qty, 0) - COALESCE(tou.qty, 0))
            * (CASE WHEN COALESCE(r.qty, 0) = 0 THEN 0 ELSE r.cost_total / r.qty END) AS total_value
    FROM product p
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    LEFT JOIN receipts r ON r.product_id = p.product_id
    LEFT JOIN issues iss ON iss.product_id = p.product_id
    LEFT JOIN transfers_in ti ON ti.product_id = p.product_id
    LEFT JOIN transfers_out tou ON tou.product_id = p.product_id
    WHERE (p_department_id IS NULL OR p.department_id = p_department_id)
      AND (r.product_id IS NOT NULL OR iss.product_id IS NOT NULL
       OR ti.product_id IS NOT NULL OR tou.product_id IS NOT NULL)
    ORDER BY sd.name, p.name;
$$;

CREATE OR REPLACE FUNCTION sp_get_kardex_report(
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE,
    p_review_state VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    opening_quantity NUMERIC,
    opening_value NUMERIC,
    receipts_quantity NUMERIC,
    receipts_value NUMERIC,
    issues_quantity NUMERIC,
    issues_value NUMERIC,
    closing_quantity NUMERIC,
    closing_value NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH receipts_before AS (
        SELECT rd.product_id, SUM(rd.quantity) AS qty, SUM(rd.total_cost) AS cost_total
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE NOT r.is_voided AND fn_review_matches(r.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND r.issue_date < p_date_from
        GROUP BY rd.product_id
    ),
    issues_before AS (
        SELECT idt.product_id, SUM(idt.quantity) AS qty, SUM(idt.total_cost) AS cost_total
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE NOT i.is_voided AND fn_review_matches(i.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND i.issue_date < p_date_from
        GROUP BY idt.product_id
    ),
    transfers_in_before AS (
        -- Solo transferencias internas (con almacén de origen) tocan stock.quantity de
        -- verdad — ver el mismo comentario en sp_get_stock_report.
        SELECT td.product_id, SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE fn_review_matches(t.review_status, p_review_state)
          AND t.source_warehouse_id IS NOT NULL
          AND (p_warehouse_id IS NULL OR t.destination_warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND t.transfer_date < p_date_from
        GROUP BY td.product_id
    ),
    transfers_out_before AS (
        SELECT td.product_id, SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE fn_review_matches(t.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR t.source_warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND t.transfer_date < p_date_from
        GROUP BY td.product_id
    ),
    receipts_period AS (
        SELECT rd.product_id, SUM(rd.quantity) AS qty, SUM(rd.total_cost) AS cost_total
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE NOT r.is_voided AND fn_review_matches(r.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR r.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR r.issue_date < p_date_to + 1)
        GROUP BY rd.product_id
    ),
    issues_period AS (
        SELECT idt.product_id, SUM(idt.quantity) AS qty, SUM(idt.total_cost) AS cost_total
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE NOT i.is_voided AND fn_review_matches(i.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR i.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR i.issue_date < p_date_to + 1)
        GROUP BY idt.product_id
    ),
    transfers_in_period AS (
        -- Solo transferencias internas (con almacén de origen) tocan stock.quantity de
        -- verdad — ver el mismo comentario en sp_get_stock_report.
        SELECT td.product_id, SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE fn_review_matches(t.review_status, p_review_state)
          AND t.source_warehouse_id IS NOT NULL
          AND (p_warehouse_id IS NULL OR t.destination_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    ),
    transfers_out_period AS (
        SELECT td.product_id, SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE fn_review_matches(t.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR t.source_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    )
    SELECT
        p.product_id,
        p.code,
        p.name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        (COALESCE(rb.qty, 0) - COALESCE(ib.qty, 0) + COALESCE(tib.qty, 0) - COALESCE(tob.qty, 0)) AS opening_quantity,
        (COALESCE(rb.cost_total, 0) - COALESCE(ib.cost_total, 0)
            + COALESCE(tib.qty, 0) * (CASE WHEN COALESCE(rb.qty, 0) = 0 THEN 0 ELSE rb.cost_total / rb.qty END)
            - COALESCE(tob.qty, 0) * (CASE WHEN COALESCE(rb.qty, 0) = 0 THEN 0 ELSE rb.cost_total / rb.qty END)
        ) AS opening_value,
        (COALESCE(rp.qty, 0) + COALESCE(tip.qty, 0)) AS receipts_quantity,
        (COALESCE(rp.cost_total, 0)
            + COALESCE(tip.qty, 0) * (CASE WHEN COALESCE(rp.qty, 0) = 0 THEN 0 ELSE rp.cost_total / rp.qty END)
        ) AS receipts_value,
        (COALESCE(ip.qty, 0) + COALESCE(tup.qty, 0)) AS issues_quantity,
        (COALESCE(ip.cost_total, 0)
            + COALESCE(tup.qty, 0) * (CASE WHEN COALESCE(rp.qty, 0) = 0 THEN 0 ELSE rp.cost_total / rp.qty END)
        ) AS issues_value,
        (
            (COALESCE(rb.qty, 0) - COALESCE(ib.qty, 0) + COALESCE(tib.qty, 0) - COALESCE(tob.qty, 0))
            + (COALESCE(rp.qty, 0) + COALESCE(tip.qty, 0))
            - (COALESCE(ip.qty, 0) + COALESCE(tup.qty, 0))
        ) AS closing_quantity,
        (
            (COALESCE(rb.cost_total, 0) - COALESCE(ib.cost_total, 0)
                + COALESCE(tib.qty, 0) * (CASE WHEN COALESCE(rb.qty, 0) = 0 THEN 0 ELSE rb.cost_total / rb.qty END)
                - COALESCE(tob.qty, 0) * (CASE WHEN COALESCE(rb.qty, 0) = 0 THEN 0 ELSE rb.cost_total / rb.qty END))
            + (COALESCE(rp.cost_total, 0)
                + COALESCE(tip.qty, 0) * (CASE WHEN COALESCE(rp.qty, 0) = 0 THEN 0 ELSE rp.cost_total / rp.qty END))
            - (COALESCE(ip.cost_total, 0)
                + COALESCE(tup.qty, 0) * (CASE WHEN COALESCE(rp.qty, 0) = 0 THEN 0 ELSE rp.cost_total / rp.qty END))
        ) AS closing_value
    FROM product p
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN receipts_before rb ON rb.product_id = p.product_id
    LEFT JOIN issues_before ib ON ib.product_id = p.product_id
    LEFT JOIN transfers_in_before tib ON tib.product_id = p.product_id
    LEFT JOIN transfers_out_before tob ON tob.product_id = p.product_id
    LEFT JOIN receipts_period rp ON rp.product_id = p.product_id
    LEFT JOIN issues_period ip ON ip.product_id = p.product_id
    LEFT JOIN transfers_in_period tip ON tip.product_id = p.product_id
    LEFT JOIN transfers_out_period tup ON tup.product_id = p.product_id
    WHERE rb.product_id IS NOT NULL OR ib.product_id IS NOT NULL
       OR tib.product_id IS NOT NULL OR tob.product_id IS NOT NULL
       OR rp.product_id IS NOT NULL OR ip.product_id IS NOT NULL
       OR tip.product_id IS NOT NULL OR tup.product_id IS NOT NULL
    ORDER BY sd.name, p.name;
$$;

CREATE OR REPLACE FUNCTION sp_get_inventory_count_report(
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE,
    p_review_state VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_id BIGINT,
    department_name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    quantity NUMERIC,
    unit_value NUMERIC,
    total_value NUMERIC,
    physical_quantity NUMERIC,
    difference NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH filtered_counts AS (
        SELECT ic.inventory_count_id, ic.source_count_id
        FROM inventory_count ic
        WHERE (p_warehouse_id IS NULL OR ic.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR ic.start_date >= p_date_from)
          AND (p_date_to IS NULL OR ic.end_date <= p_date_to)
    ),
    visible_counts AS (
        SELECT fc.inventory_count_id
        FROM filtered_counts fc
        WHERE fc.inventory_count_id NOT IN (
            SELECT fc2.source_count_id FROM filtered_counts fc2 WHERE fc2.source_count_id IS NOT NULL
        )
    )
    SELECT
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        q.quantity,
        icd.unit_value,
        (q.quantity * COALESCE(icd.unit_value, 0)) AS total_value,
        icd.physical_quantity,
        CASE WHEN p_review_state IS NULL THEN icd.difference
             ELSE COALESCE(icd.physical_quantity, 0) - q.quantity END AS difference
    FROM inventory_count_detail icd
    JOIN visible_counts vc ON vc.inventory_count_id = icd.inventory_count_id
    JOIN inventory_count ic ON ic.inventory_count_id = icd.inventory_count_id
    CROSS JOIN LATERAL (
        SELECT CASE WHEN p_review_state IS NULL THEN icd.system_quantity
                    ELSE fn_review_quantity(icd.product_id, ic.warehouse_id, ic.end_date::date, p_review_state) END AS quantity
    ) q
    JOIN product p ON p.product_id = icd.product_id
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    ORDER BY d.name, sd.name, p.name;
$$;

CREATE OR REPLACE FUNCTION sp_get_issues_report(
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE,
    p_review_state VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    issue_id BIGINT,
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_id BIGINT,
    department_name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    quantity NUMERIC,
    unit_cost NUMERIC,
    total_cost NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        i.issue_id,
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        idt.quantity,
        idt.unit_cost,
        idt.total_cost
    FROM issue_detail idt
    JOIN issue i ON i.issue_id = idt.issue_id
    JOIN product p ON p.product_id = idt.product_id
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    WHERE NOT i.is_voided AND fn_review_matches(i.review_status, p_review_state)
      AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
      AND (p_date_from IS NULL OR i.issue_date >= p_date_from)
      AND (p_date_to IS NULL OR i.issue_date < p_date_to + 1)
    ORDER BY d.name, sd.name, p.name, i.issue_id;
$$;

CREATE OR REPLACE FUNCTION sp_get_receipts_report(
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE,
    p_review_state VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    receipt_id BIGINT,
    receipt_date TIMESTAMP,
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_id BIGINT,
    department_name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    quantity NUMERIC,
    unit_cost NUMERIC,
    total_cost NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        r.receipt_id,
        r.issue_date AS receipt_date,
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        rd.quantity,
        rd.unit_cost,
        rd.total_cost
    FROM receipt_detail rd
    JOIN receipt r ON r.receipt_id = rd.receipt_id
    JOIN product p ON p.product_id = rd.product_id
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    WHERE NOT r.is_voided AND fn_review_matches(r.review_status, p_review_state)
      AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
      AND (p_date_from IS NULL OR r.issue_date >= p_date_from)
      AND (p_date_to IS NULL OR r.issue_date < p_date_to + 1)
    ORDER BY d.name, sd.name, p.name, r.receipt_id;
$$;

CREATE OR REPLACE FUNCTION sp_get_kardex_by_product_report(
    p_product_id BIGINT,
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE,
    p_review_state VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    movement_type VARCHAR,
    voucher_id BIGINT,
    voucher_date TIMESTAMP,
    quantity NUMERIC,
    unit_cost NUMERIC,
    total_cost NUMERIC,
    balance_quantity NUMERIC,
    balance_value NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH movements AS (
        SELECT 'receipt' AS movement_type, r.receipt_id AS voucher_id, r.issue_date AS voucher_date,
               r.created_at AS entry_order, rd.quantity, rd.unit_cost, rd.total_cost
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE rd.product_id = p_product_id
          AND NOT r.is_voided AND fn_review_matches(r.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
        UNION ALL
        SELECT 'issue' AS movement_type, i.issue_id AS voucher_id, i.issue_date AS voucher_date,
               i.created_at AS entry_order, idt.quantity, idt.unit_cost, idt.total_cost
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE idt.product_id = p_product_id
          AND NOT i.is_voided AND fn_review_matches(i.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
    ),
    running AS (
        SELECT
            movement_type, voucher_id, voucher_date, entry_order, quantity, unit_cost, total_cost,
            -- Empate de fecha entre una Entrada y una Salida: se ordena por el momento real
            -- en que se registró cada una (created_at), no por el ID crudo — receipt_id e
            -- issue_id son secuencias independientes sin relación de orden entre sí.
            SUM(CASE WHEN movement_type = 'receipt' THEN quantity ELSE -quantity END)
                OVER (ORDER BY voucher_date, entry_order, voucher_id ROWS UNBOUNDED PRECEDING) AS balance_quantity,
            SUM(CASE WHEN movement_type = 'receipt' THEN total_cost ELSE -total_cost END)
                OVER (ORDER BY voucher_date, entry_order, voucher_id ROWS UNBOUNDED PRECEDING) AS balance_value
        FROM movements
    )
    SELECT movement_type, voucher_id, voucher_date, quantity, unit_cost, total_cost, balance_quantity, balance_value
    FROM running
    WHERE (p_date_from IS NULL OR voucher_date >= p_date_from)
      AND (p_date_to IS NULL OR voucher_date < p_date_to + 1)
    ORDER BY voucher_date, entry_order, voucher_id;
$$;

CREATE OR REPLACE FUNCTION sp_get_kardex_by_product_opening_balance(
    p_product_id BIGINT,
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_review_state VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    opening_quantity NUMERIC,
    opening_value NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH receipts_before AS (
        SELECT COALESCE(SUM(rd.quantity), 0) AS qty, COALESCE(SUM(rd.total_cost), 0) AS cost_total
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE rd.product_id = p_product_id
          AND NOT r.is_voided AND fn_review_matches(r.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND r.issue_date < p_date_from
    ),
    issues_before AS (
        SELECT COALESCE(SUM(idt.quantity), 0) AS qty, COALESCE(SUM(idt.total_cost), 0) AS cost_total
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE idt.product_id = p_product_id
          AND NOT i.is_voided AND fn_review_matches(i.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND i.issue_date < p_date_from
    )
    SELECT
        (SELECT qty FROM receipts_before) - (SELECT qty FROM issues_before) AS opening_quantity,
        (SELECT cost_total FROM receipts_before) - (SELECT cost_total FROM issues_before) AS opening_value;
$$;

-- 7. Hoja de firmas de una salida por lote: una fila por pastor y material.
CREATE OR REPLACE FUNCTION sp_get_issue_batch_sheet(p_issue_batch_id BIGINT)
RETURNS TABLE (
    issue_batch_id BIGINT,
    monthly_number INT,
    batch_date TIMESTAMP,
    warehouse_name VARCHAR,
    description VARCHAR,
    created_by_name TEXT,
    issue_id BIGINT,
    is_voided BOOLEAN,
    client_name VARCHAR,
    tax_id VARCHAR,
    district_name VARCHAR,
    product_id BIGINT,
    product_code VARCHAR,
    product_name VARCHAR,
    department_name VARCHAR,
    quantity NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        b.issue_batch_id,
        b.monthly_number,
        b.batch_date,
        w.name AS warehouse_name,
        b.description,
        (au.first_name || ' ' || au.last_name) AS created_by_name,
        i.issue_id,
        i.is_voided,
        cp.name AS client_name,
        cp.tax_id,
        dt.district_name,
        p.product_id,
        p.code AS product_code,
        p.name AS product_name,
        dep.name AS department_name,
        d.quantity
    FROM issue_batch b
    JOIN issue i ON i.issue_batch_id = b.issue_batch_id
    JOIN issue_detail d ON d.issue_id = i.issue_id
    JOIN product p ON p.product_id = d.product_id
    LEFT JOIN department dep ON dep.department_id = p.department_id
    LEFT JOIN warehouse w ON w.warehouse_id = b.warehouse_id
    LEFT JOIN app_user au ON au.app_user_id = b.created_by
    LEFT JOIN client c ON c.client_id = i.client_id
    LEFT JOIN party cp ON cp.party_id = c.party_id
    LEFT JOIN district dt ON dt.district_id = c.district_id
    WHERE b.issue_batch_id = p_issue_batch_id
    ORDER BY dt.district_name, cp.name, p.name;
$$;

COMMIT;

-- Comprobación
SELECT g.name AS grupo, c.name AS reporte, c.display_order
FROM menu g JOIN menu c ON c.parent_menu_id = g.menu_id
WHERE g.name IN ('Reportes', 'Contabilidad')
ORDER BY g.name DESC, c.display_order;
SELECT count(*) AS funciones_con_mb
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'public' AND p.prosrc LIKE '%''MB ''%';
SELECT count(*) AS lineas_retencion,
       count(*) FILTER (WHERE d.unit_cost = round(d.unit_price / 0.92, 2)) AS con_costo_correcto
FROM receipt_detail d
JOIN receipt r ON r.receipt_id = d.receipt_id
JOIN receipt_type t ON t.receipt_type_id = r.receipt_type_id
WHERE t.name = 'Compras con Retencion' AND d.unit_price IS NOT NULL;
