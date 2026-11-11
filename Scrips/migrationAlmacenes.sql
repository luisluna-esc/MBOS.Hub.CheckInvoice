-- =====================================================================================
-- Almacenes: solo quedan dos, "Almacén Central" y "Almacén Galpón Uno" (con tilde).
-- Se puede correr más de una vez. No se borra ningún movimiento.
--
--   * ALMACEN CENTRAL MBOS (o "Almacen Central") pasa a llamarse "Almacén Central", con todo lo suyo.
--   * ALMACEN CENTRAL MBON (o "Almacen Galpon Uno") pasa a ser "Almacén Galpón Uno" (conserva sus
--     transferencias y stock); si no existe, el galpón se crea.
--   * Los demás almacenes (la demo de reportes y los que nunca se usaron) se juntan con el
--     Almacén Central: sus entradas, salidas, lotes, transferencias, conteos, notas pendientes y
--     stock pasan al central (el stock se suma, con costo promedio ponderado) y el almacén se elimina.
--   * Si ya se corrió antes (nombres sin tilde), solo les pone la tilde.
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationAlmacenes.sql
-- =====================================================================================

BEGIN;

DO $$
DECLARE
    v_central BIGINT;
    v_galpon BIGINT;
    v_old BIGINT;
BEGIN
    SELECT warehouse_id INTO v_central FROM warehouse
    WHERE name IN ('Almacén Central', 'Almacen Central') OR upper(name) = 'ALMACEN CENTRAL MBOS'
    ORDER BY (name = 'Almacén Central') DESC, (name = 'Almacen Central') DESC, warehouse_id
    LIMIT 1;
    IF v_central IS NULL THEN
        RAISE EXCEPTION 'No se encontró el almacén "ALMACEN CENTRAL MBOS" (ni "Almacén Central"). Almacenes: %',
            (SELECT string_agg(warehouse_id || ' ' || name, ', ' ORDER BY warehouse_id) FROM warehouse);
    END IF;

    -- El galpón es el antiguo ALMACEN CENTRAL MBON: solo tiene transferencias desde el central y
    -- su stock; así no se pierde ese historial. Si no existe, el galpón se crea más abajo.
    SELECT warehouse_id INTO v_galpon FROM warehouse
    WHERE warehouse_id <> v_central
      AND (name IN ('Almacén Galpón Uno', 'Almacen Galpon Uno') OR upper(name) = 'ALMACEN CENTRAL MBON')
    ORDER BY (name = 'Almacén Galpón Uno') DESC, (name = 'Almacen Galpon Uno') DESC, warehouse_id
    LIMIT 1;

    -- Los demás: todo lo suyo pasa al central y el almacén se elimina.
    FOR v_old IN
        SELECT warehouse_id FROM warehouse
        WHERE warehouse_id <> v_central AND warehouse_id IS DISTINCT FROM v_galpon
        ORDER BY warehouse_id
    LOOP
        UPDATE receipt SET warehouse_id = v_central WHERE warehouse_id = v_old;
        UPDATE issue SET warehouse_id = v_central WHERE warehouse_id = v_old;
        UPDATE issue_batch SET warehouse_id = v_central WHERE warehouse_id = v_old;
        UPDATE inventory_count SET warehouse_id = v_central WHERE warehouse_id = v_old;
        UPDATE pending_receipt SET warehouse_id = v_central WHERE warehouse_id = v_old;
        UPDATE transfer SET source_warehouse_id = v_central WHERE source_warehouse_id = v_old;
        UPDATE transfer SET destination_warehouse_id = v_central WHERE destination_warehouse_id = v_old;

        -- Stock: se suma al del central; el costo queda promedio ponderado.
        UPDATE stock c
        SET average_cost = CASE WHEN c.quantity + o.quantity > 0
                                THEN (c.quantity * c.average_cost + o.quantity * o.average_cost) / (c.quantity + o.quantity)
                                ELSE c.average_cost END,
            quantity = c.quantity + o.quantity
        FROM stock o
        WHERE o.warehouse_id = v_old AND c.warehouse_id = v_central AND c.product_id = o.product_id;
        DELETE FROM stock o
        WHERE o.warehouse_id = v_old
          AND EXISTS (SELECT 1 FROM stock c WHERE c.warehouse_id = v_central AND c.product_id = o.product_id);
        UPDATE stock SET warehouse_id = v_central WHERE warehouse_id = v_old;

        DELETE FROM warehouse WHERE warehouse_id = v_old;
    END LOOP;

    UPDATE warehouse SET name = 'Almacén Central', is_active = TRUE WHERE warehouse_id = v_central;

    IF v_galpon IS NULL THEN
        INSERT INTO warehouse (name, is_active) VALUES ('Almacén Galpón Uno', TRUE);
    ELSE
        UPDATE warehouse SET name = 'Almacén Galpón Uno', is_active = TRUE WHERE warehouse_id = v_galpon;
    END IF;
END $$;

-- La demo ya no tiene almacén propio: su registro se quita para que demoDataCleanup.sql no se
-- corra por error sobre datos que ahora son del central.
DROP TABLE IF EXISTS demo_seed_registry;

COMMIT;

-- Comprobación: deben salir solo los dos, con su stock.
SELECT w.warehouse_id, w.name, w.is_active,
       (SELECT coalesce(sum(s.quantity), 0) FROM stock s WHERE s.warehouse_id = w.warehouse_id) AS stock_total
FROM warehouse w ORDER BY w.warehouse_id;
