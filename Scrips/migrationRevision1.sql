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

-- 6. Historial: buscar el N° de una salida por lote trae a todos sus pastores.
CREATE OR REPLACE FUNCTION sp_get_issues(
    p_issue_id BIGINT,
    p_client_id BIGINT,
    p_warehouse_id BIGINT,
    p_issue_type_id BIGINT,
    p_page_number INT,
    p_page_size INT,
    p_client_name VARCHAR DEFAULT NULL
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
    ORDER BY i.issue_date DESC, i.issue_id DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
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
WHERE g.name = 'Contabilidad'
ORDER BY c.display_order;
SELECT count(*) AS funciones_con_mb
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'public' AND p.prosrc LIKE '%''MB ''%';
SELECT count(*) AS lineas_retencion,
       count(*) FILTER (WHERE d.unit_cost = round(d.unit_price / 0.92, 2)) AS con_costo_correcto
FROM receipt_detail d
JOIN receipt r ON r.receipt_id = d.receipt_id
JOIN receipt_type t ON t.receipt_type_id = r.receipt_type_id
WHERE t.name = 'Compras con Retencion' AND d.unit_price IS NOT NULL;
