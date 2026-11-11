-- =====================================================================================
-- Saldos iniciales faltantes (2026-11-05)
--
-- Hay stock que se cargó directo en la tabla stock, sin una Entrada que lo respalde.
-- Los reportes calculan Entradas − Salidas, así que esos productos salían en negativo.
-- Este script crea, por almacén, una Entrada "Inventario Inicial" (estado 'anterior')
-- con la diferencia de cada producto, valorizada para que el almacén cuadre con el stock
-- real (cantidad × costo promedio), y con fecha
-- un día antes del primer movimiento de ese producto en ese almacén. No cambia la tabla
-- stock: después del script los reportes dan lo mismo que el stock real.
--
-- Solo agrega lo que falta: si se corre otra vez, ya no encuentra diferencias.
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationSaldosIniciales.sql
-- =====================================================================================

BEGIN;

DO $$
DECLARE
    v_type_id BIGINT;
    v_user_id BIGINT;
    v_wh RECORD;
    v_receipt_id BIGINT;
    v_number INT;
BEGIN
    SELECT receipt_type_id INTO v_type_id FROM receipt_type WHERE name = 'Inventario Inicial' LIMIT 1;
    IF v_type_id IS NULL THEN
        INSERT INTO receipt_type (name, is_active) VALUES ('Inventario Inicial', true) RETURNING receipt_type_id INTO v_type_id;
    END IF;
    SELECT app_user_id INTO v_user_id FROM app_user WHERE username = 'luis.luna';

    CREATE TEMP TABLE missing ON COMMIT DROP AS
    SELECT s.warehouse_id, s.product_id,
           s.quantity - fn_review_quantity(s.product_id, s.warehouse_id, NULL, NULL) AS qty,
           -- Valor que falta para que el almacén quede valorizado como el stock real
           -- (cantidad × costo promedio), contando lo que ya entró y salió.
           GREATEST(s.quantity * s.average_cost
             - COALESCE((SELECT SUM(rd.total_cost) FROM receipt r JOIN receipt_detail rd ON rd.receipt_id = r.receipt_id
                         WHERE rd.product_id = s.product_id AND r.warehouse_id = s.warehouse_id AND NOT r.is_voided), 0)
             + COALESCE((SELECT SUM(idt.total_cost) FROM issue i JOIN issue_detail idt ON idt.issue_id = i.issue_id
                         WHERE idt.product_id = s.product_id AND i.warehouse_id = s.warehouse_id AND NOT i.is_voided), 0)
             - s.average_cost * (fn_review_quantity(s.product_id, s.warehouse_id, NULL, NULL)
                 - COALESCE((SELECT SUM(rd.quantity) FROM receipt r JOIN receipt_detail rd ON rd.receipt_id = r.receipt_id
                             WHERE rd.product_id = s.product_id AND r.warehouse_id = s.warehouse_id AND NOT r.is_voided), 0)
                 + COALESCE((SELECT SUM(idt.quantity) FROM issue i JOIN issue_detail idt ON idt.issue_id = i.issue_id
                             WHERE idt.product_id = s.product_id AND i.warehouse_id = s.warehouse_id AND NOT i.is_voided), 0)),
             0) AS total,
           COALESCE(LEAST(
               (SELECT MIN(r.issue_date) FROM receipt r JOIN receipt_detail rd ON rd.receipt_id = r.receipt_id
                 WHERE rd.product_id = s.product_id AND r.warehouse_id = s.warehouse_id AND NOT r.is_voided),
               (SELECT MIN(i.issue_date) FROM issue i JOIN issue_detail idt ON idt.issue_id = i.issue_id
                 WHERE idt.product_id = s.product_id AND i.warehouse_id = s.warehouse_id AND NOT i.is_voided),
               (SELECT MIN(t.transfer_date) FROM transfer t JOIN transfer_detail td ON td.transfer_id = t.transfer_id
                 WHERE td.product_id = s.product_id AND (t.source_warehouse_id = s.warehouse_id OR t.destination_warehouse_id = s.warehouse_id))
           ), now())::date - 1 AS receipt_date
    FROM stock s
    WHERE s.quantity - fn_review_quantity(s.product_id, s.warehouse_id, NULL, NULL) > 0;

    FOR v_wh IN SELECT warehouse_id, MIN(receipt_date) AS receipt_date FROM missing GROUP BY warehouse_id LOOP
        SELECT COALESCE(MAX(monthly_number), 0) + 1 INTO v_number
        FROM receipt WHERE date_trunc('month', issue_date) = date_trunc('month', v_wh.receipt_date::timestamp);

        INSERT INTO receipt (warehouse_id, receipt_type_id, description, issue_date, created_at, created_by,
                             invoice_total, monthly_number, review_status)
        SELECT v_wh.warehouse_id, v_type_id, 'Saldo inicial (stock cargado sin entrada)', v_wh.receipt_date, now(), v_user_id,
               ROUND(SUM(m.total), 2), v_number, 'anterior'
        FROM missing m WHERE m.warehouse_id = v_wh.warehouse_id
        RETURNING receipt_id INTO v_receipt_id;

        INSERT INTO receipt_detail (receipt_id, product_id, quantity, unit_cost, total_cost)
        SELECT v_receipt_id, m.product_id, m.qty, ROUND(m.total / m.qty, 2), ROUND(m.total, 2)
        FROM missing m WHERE m.warehouse_id = v_wh.warehouse_id;

        RAISE NOTICE 'Almacén %: entrada % con % productos', v_wh.warehouse_id, v_receipt_id,
            (SELECT COUNT(*) FROM missing m WHERE m.warehouse_id = v_wh.warehouse_id);
    END LOOP;
END $$;

-- Lo que no se puede arreglar con una entrada (salió más de lo que hay en stock) se muestra
-- para revisarlo a mano.
SELECT w.name AS almacen, p.name AS producto, s.quantity AS stock,
       fn_review_quantity(s.product_id, s.warehouse_id, NULL, NULL) AS segun_movimientos
FROM stock s
JOIN warehouse w ON w.warehouse_id = s.warehouse_id
JOIN product p ON p.product_id = s.product_id
WHERE s.quantity <> fn_review_quantity(s.product_id, s.warehouse_id, NULL, NULL);

COMMIT;
