-- =====================================================================================
-- Salida por lote con costos (2026-11-05): la hoja de firmas trae el costo con que salió cada
-- material, para el resumen de costos al pie (cantidad, costo unitario y total por material).
-- Se puede correr más de una vez.
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationLoteCostos.sql
-- =====================================================================================

BEGIN;

-- Hoja de firmas de una salida por lote: una fila por pastor y material, con el costo con que
-- salió cada material (para el resumen de costos al pie de la hoja).
DROP FUNCTION IF EXISTS sp_get_issue_batch_sheet(BIGINT);
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
    quantity NUMERIC,
    unit_cost NUMERIC,
    total_cost NUMERIC
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
        d.quantity,
        d.unit_cost,
        d.total_cost
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
