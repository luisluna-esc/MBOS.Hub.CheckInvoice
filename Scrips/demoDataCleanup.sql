-- =====================================================================
-- Borra los datos de demostración que cargó demoData.sql.
--
-- Solo borra lo anotado en demo_seed_registry (y lo que cuelga de eso: líneas, depósitos por
-- producto, stock del almacén de la demo). Los catálogos que ya existían no se tocan.
-- Si alguien registró movimientos reales sobre datos de la demo (ej. una salida desde el
-- almacén de la demo), el script se detiene sin borrar nada.
--
-- Correr:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f demoDataCleanup.sql
-- =====================================================================

BEGIN;

DO $$
DECLARE
    v_wh BIGINT[] := ARRAY(SELECT row_id FROM demo_seed_registry WHERE table_name = 'warehouse');
    v_products BIGINT[] := ARRAY(SELECT row_id FROM demo_seed_registry WHERE table_name = 'product');
    v_clients BIGINT[] := ARRAY(SELECT row_id FROM demo_seed_registry WHERE table_name = 'client');
    v_suppliers BIGINT[] := ARRAY(SELECT row_id FROM demo_seed_registry WHERE table_name = 'supplier');
    v_issues BIGINT[] := ARRAY(SELECT row_id FROM demo_seed_registry WHERE table_name = 'issue');
    v_receipts BIGINT[] := ARRAY(SELECT row_id FROM demo_seed_registry WHERE table_name = 'receipt');
    v_ars BIGINT[] := ARRAY(SELECT row_id FROM demo_seed_registry WHERE table_name = 'account_receivable');
    v_payments BIGINT[] := ARRAY(SELECT row_id FROM demo_seed_registry WHERE table_name = 'payment');
    v_extra INT;
BEGIN
    -- Movimientos que no cargó la demo pero usan sus datos: no se borran a ciegas.
    SELECT (SELECT count(*) FROM issue WHERE (warehouse_id = ANY (v_wh) OR client_id = ANY (v_clients)) AND NOT issue_id = ANY (v_issues))
         + (SELECT count(*) FROM receipt WHERE (warehouse_id = ANY (v_wh) OR supplier_id = ANY (v_suppliers)) AND NOT receipt_id = ANY (v_receipts))
         + (SELECT count(*) FROM payment WHERE account_receivable_id = ANY (v_ars) AND NOT payment_id = ANY (v_payments))
         + (SELECT count(*) FROM transfer WHERE source_warehouse_id = ANY (v_wh) OR destination_warehouse_id = ANY (v_wh))
         + (SELECT count(*) FROM issue_detail d WHERE d.product_id = ANY (v_products) AND NOT d.issue_id = ANY (v_issues))
         + (SELECT count(*) FROM receipt_detail d WHERE d.product_id = ANY (v_products) AND NOT d.receipt_id = ANY (v_receipts))
      INTO v_extra;
    IF v_extra > 0 THEN
        RAISE EXCEPTION 'Hay % movimiento(s) registrados a mano sobre datos de la demo. Revísalos antes de borrar.', v_extra;
    END IF;

    DELETE FROM issue_void_request WHERE issue_id = ANY (v_issues);
    DELETE FROM receipt_void_request WHERE receipt_id = ANY (v_receipts);
    DELETE FROM payment WHERE payment_id = ANY (v_payments);            -- payment_detail se borra en cascada
    DELETE FROM installment WHERE account_receivable_id = ANY (v_ars);
    DELETE FROM account_receivable WHERE account_receivable_id = ANY (v_ars);
    DELETE FROM inventory_count_detail WHERE inventory_count_id IN (SELECT row_id FROM demo_seed_registry WHERE table_name = 'inventory_count');
    DELETE FROM inventory_count WHERE inventory_count_id IN (SELECT row_id FROM demo_seed_registry WHERE table_name = 'inventory_count');
    DELETE FROM issue WHERE issue_id = ANY (v_issues);                  -- issue_detail se borra en cascada
    DELETE FROM receipt_detail WHERE receipt_id = ANY (v_receipts);
    DELETE FROM receipt WHERE receipt_id = ANY (v_receipts);
    DELETE FROM stock WHERE warehouse_id = ANY (v_wh) OR product_id = ANY (v_products);
    DELETE FROM product WHERE product_id = ANY (v_products);
    DELETE FROM client_district_history WHERE client_id = ANY (v_clients);
    DELETE FROM client WHERE client_id = ANY (v_clients);
    DELETE FROM supplier WHERE supplier_id = ANY (v_suppliers);
    DELETE FROM party WHERE party_id IN (SELECT row_id FROM demo_seed_registry WHERE table_name = 'party');
    DELETE FROM warehouse WHERE warehouse_id = ANY (v_wh);
    DELETE FROM sub_department WHERE sub_department_id IN (SELECT row_id FROM demo_seed_registry WHERE table_name = 'sub_department');
    DELETE FROM department WHERE department_id IN (SELECT row_id FROM demo_seed_registry WHERE table_name = 'department');
    DELETE FROM media_type WHERE media_type_id IN (SELECT row_id FROM demo_seed_registry WHERE table_name = 'media_type');
END $$;

DROP TABLE demo_seed_registry;

COMMIT;
