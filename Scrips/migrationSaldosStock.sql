-- =====================================================================================
-- Saldos de stock (2026-11-05)
--
-- 1. Stock - Almacén y Stock - Departamento valorizan igual que el Kardex por Material:
--    valor = lo que entró (al costo de cada entrada) menos lo que salió (al costo promedio
--    con que salió cada salida). Antes el costo unitario era el promedio simple de todas las
--    entradas, sin descontar las salidas, y no cuadraba con el kardex.
--    Ejemplo HOJAS: 100 a 10, 50 a 15, salen 50, entran 200 a 20:
--      antes  (1000 + 750 + 4000) / 350 = 16,43
--      ahora  (1000 + 750 - 583,33 + 4000) / 300 = 17,22
-- 2. Los reportes son saldos a la Fecha fin. La Fecha inicio solo deja los productos que
--    se movieron en el periodo. Los productos sin saldo (cero o menos) no salen.
-- 3. Kardex Físico Valorado: sin saldo el valor es 0 y nunca sale un valor negativo.
--
-- Se puede correr más de una vez.
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationSaldosStock.sql
-- =====================================================================================

BEGIN;

-- Saldo (cantidad y valor) de cada producto en un almacén (o en todos) a una fecha.
-- Las transferencias no guardan costo: se valorizan al costo promedio actual del producto.
CREATE OR REPLACE FUNCTION fn_stock_balance(
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE,
    p_review_state VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    product_id BIGINT,
    quantity NUMERIC,
    total_value NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH movements AS (
        SELECT rd.product_id, r.issue_date AS moved_at, rd.quantity AS qty, rd.total_cost AS val, 0::numeric AS transfer_qty
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE NOT r.is_voided AND fn_review_matches(r.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
          AND (p_date_to IS NULL OR r.issue_date < p_date_to + 1)
        UNION ALL
        SELECT idt.product_id, i.issue_date, -idt.quantity, -idt.total_cost, 0
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE NOT i.is_voided AND fn_review_matches(i.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
          AND (p_date_to IS NULL OR i.issue_date < p_date_to + 1)
        UNION ALL
        -- Solo las transferencias internas (con almacén de origen) suben el stock del destino.
        SELECT td.product_id, t.transfer_date, td.quantity, 0, td.quantity
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE NOT t.is_voided AND fn_review_matches(t.review_status, p_review_state)
          AND t.source_warehouse_id IS NOT NULL
          AND (p_warehouse_id IS NULL OR t.destination_warehouse_id = p_warehouse_id)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        UNION ALL
        SELECT td.product_id, t.transfer_date, -td.quantity, 0, -td.quantity
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE NOT t.is_voided AND fn_review_matches(t.review_status, p_review_state)
          AND (p_warehouse_id IS NULL OR t.source_warehouse_id = p_warehouse_id)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
    ),
    totals AS (
        SELECT m.product_id,
               SUM(m.qty) AS qty,
               SUM(m.val) AS val,
               SUM(m.transfer_qty) AS transfer_qty,
               BOOL_OR(p_date_from IS NULL OR m.moved_at >= p_date_from) AS moved_in_period
        FROM movements m
        GROUP BY m.product_id
    ),
    current_cost AS (
        SELECT s.product_id,
               CASE WHEN SUM(s.quantity) > 0 THEN SUM(s.quantity * s.average_cost) / SUM(s.quantity)
                    ELSE MAX(s.average_cost) END AS average_cost
        FROM stock s
        WHERE p_warehouse_id IS NULL OR s.warehouse_id = p_warehouse_id
        GROUP BY s.product_id
    )
    -- Si el valor da cero o menos con cantidad positiva (salidas antiguas registradas a un
    -- costo distinto del promedio), se usa el costo promedio guardado: nunca se muestra negativo.
    SELECT t.product_id,
           t.qty,
           CASE WHEN t.val + t.transfer_qty * COALESCE(c.average_cost, 0) > 0
                THEN t.val + t.transfer_qty * COALESCE(c.average_cost, 0)
                ELSE t.qty * COALESCE(c.average_cost, 0) END
    FROM totals t
    LEFT JOIN current_cost c ON c.product_id = t.product_id
    WHERE t.moved_in_period AND t.qty > 0;
$$;

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
    SELECT
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        b.quantity AS net_quantity,
        ROUND(b.total_value / b.quantity, 4) AS unit_value,
        ROUND(b.total_value, 2) AS total_value
    FROM fn_stock_balance(p_warehouse_id, p_date_from, p_date_to, p_review_state) b
    JOIN product p ON p.product_id = b.product_id
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
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
    SELECT
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        b.quantity AS net_quantity,
        ROUND(b.total_value / b.quantity, 4) AS unit_value,
        ROUND(b.total_value, 2) AS total_value
    FROM fn_stock_balance(p_warehouse_id, p_date_from, p_date_to, p_review_state) b
    JOIN product p ON p.product_id = b.product_id
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    WHERE p_department_id IS NULL OR p.department_id = p_department_id
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
    , raw AS (
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
    ),
    current_cost AS (
        SELECT s.product_id,
               CASE WHEN SUM(s.quantity) > 0 THEN SUM(s.quantity * s.average_cost) / SUM(s.quantity)
                    ELSE MAX(s.average_cost) END AS average_cost
        FROM stock s
        WHERE p_warehouse_id IS NULL OR s.warehouse_id = p_warehouse_id
        GROUP BY s.product_id
    )
    -- Nunca valores negativos (2026-11-05): sin saldo el valor es 0; con saldo y valor negativo
    -- (salidas antiguas registradas a un costo distinto del promedio) se usa el costo promedio.
    SELECT
        r.product_id, r.code, r.name, r.sub_department_id, r.sub_department_name,
        r.opening_quantity,
        CASE WHEN r.opening_quantity <= 0 THEN 0
             WHEN r.opening_value < 0 THEN r.opening_quantity * COALESCE(c.average_cost, 0)
             ELSE r.opening_value END AS opening_value,
        r.receipts_quantity, r.receipts_value, r.issues_quantity, r.issues_value,
        r.closing_quantity,
        CASE WHEN r.closing_quantity <= 0 THEN 0
             WHEN r.closing_value < 0 THEN r.closing_quantity * COALESCE(c.average_cost, 0)
             ELSE r.closing_value END AS closing_value
    FROM raw r
    LEFT JOIN current_cost c ON c.product_id = r.product_id
    ORDER BY r.sub_department_name, r.name;
$$;

COMMIT;
