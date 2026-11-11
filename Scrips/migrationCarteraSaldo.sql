-- =====================================================================================
-- Cartera de cuentas por cobrar con saldo arrastrado (2026-11-05)
-- Las fechas del reporte son un corte: salen las cuentas de salidas hasta la fecha fin, y las
-- de antes de la fecha inicio solo si todavía deben algo. Antes se filtraba por fecha límite y
-- lo pendiente de meses anteriores no salía.
-- Se puede correr más de una vez.
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationCarteraSaldo.sql
-- =====================================================================================

BEGIN;

-- Reporte "Cuentas por Cobrar" general: a diferencia de sp_get_account_receivables (grilla
-- paginada de la pantalla CRUD), este devuelve la cartera completa sin paginar, con el
-- nombre del cliente ya resuelto (join client -> party) porque el reporte es de todos los
-- clientes a la vez y no tendría sentido resolver cada nombre uno por uno en el backend
-- (como sí hace el Reporte Campo Pastor, que es de un solo cliente).
-- Fechas como corte (2026-11-05): el saldo se arrastra. Salen las cuentas de salidas hasta la
-- fecha fin; las de antes de la fecha inicio solo si todavía deben algo (lo pendiente de agosto
-- sigue saliendo en septiembre y después). Antes se filtraba por fecha límite y lo pendiente
-- de meses anteriores desaparecía del reporte.
DROP FUNCTION IF EXISTS sp_get_account_receivables_report(BIGINT, VARCHAR, DATE, DATE);
CREATE OR REPLACE FUNCTION sp_get_account_receivables_report(
    p_client_id BIGINT,
    p_status VARCHAR,
    p_date_from DATE,
    p_date_to DATE
)
RETURNS TABLE (
    account_receivable_id BIGINT,
    issue_id BIGINT,
    issue_date TIMESTAMP,
    client_id BIGINT,
    client_name VARCHAR,
    product_count INT,
    product_names VARCHAR,
    total_amount NUMERIC,
    outstanding_balance NUMERIC,
    paid_amount NUMERIC,
    due_date DATE,
    status VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        ar.account_receivable_id,
        ar.issue_id,
        i.issue_date,
        ar.client_id,
        p.name AS client_name,
        CAST(id_count.product_count AS INT) AS product_count,
        id_count.product_names,
        ar.total_amount,
        ar.outstanding_balance,
        ar.total_amount - ar.outstanding_balance AS paid_amount,
        ar.due_date,
        CASE
            WHEN ar.status = 'paid' THEN 'paid'
            WHEN ar.status = 'pending' AND ar.due_date IS NOT NULL AND ar.due_date < (NOW() AT TIME ZONE 'America/La_Paz')::date THEN 'late'
            ELSE ar.status
        END AS status
    FROM account_receivable ar
    LEFT JOIN issue i ON i.issue_id = ar.issue_id
    LEFT JOIN client c ON c.client_id = ar.client_id
    LEFT JOIN party p ON p.party_id = c.party_id
    LEFT JOIN LATERAL (
        SELECT
            COUNT(DISTINCT id2.product_id) AS product_count,
            STRING_AGG(DISTINCT pr.name, ', ') AS product_names
        FROM issue_detail id2
        JOIN product pr ON pr.product_id = id2.product_id
        WHERE id2.issue_id = ar.issue_id
    ) id_count ON TRUE
    WHERE (p_client_id IS NULL OR ar.client_id = p_client_id)
      AND (
            p_status IS NULL
            OR (
                CASE
                    WHEN ar.status = 'paid' THEN 'paid'
                    WHEN ar.status = 'pending' AND ar.due_date IS NOT NULL AND ar.due_date < (NOW() AT TIME ZONE 'America/La_Paz')::date THEN 'late'
                    ELSE ar.status
                END
              ) = p_status
          )
      AND (p_date_to IS NULL OR COALESCE(i.issue_date, ar.created_at) < p_date_to + 1)
      AND (p_date_from IS NULL OR ar.outstanding_balance > 0 OR COALESCE(i.issue_date, ar.created_at) >= p_date_from)
    ORDER BY (ar.status = 'paid'), ar.due_date NULLS LAST, ar.account_receivable_id;
$$;

COMMIT;
