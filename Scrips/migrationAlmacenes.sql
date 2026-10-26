-- =====================================================================================
-- Almacenes (2026-10-26): solo quedan dos, "Almacen Central" y "Almacen Galpon Uno".
-- Se puede correr más de una vez. No se borra ningún movimiento.
--
--   * ALMACEN CENTRAL MBOS pasa a llamarse "Almacen Central", con todo lo suyo.
--   * ALMACEN CENTRAL MBON pasa a ser "Almacen Galpon Uno" (conserva sus transferencias y stock);
--     si no existe, el galpón se crea.
--   * Los demás almacenes (la demo de reportes y los que nunca se usaron) se juntan con el
--     Almacen Central: sus entradas, salidas, lotes, transferencias, conteos, notas pendientes y
--     stock pasan al central (el stock se suma, con costo promedio ponderado) y el almacén se elimina.
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationAlmacenes.sql
-- =====================================================================================

BEGIN;

DO $$
DECLARE
    v_central BIGINT;
    v_old BIGINT;
BEGIN
    SELECT warehouse_id INTO v_central FROM warehouse WHERE name = 'Almacen Central' ORDER BY warehouse_id LIMIT 1;
    IF v_central IS NULL THEN
        SELECT warehouse_id INTO v_central FROM warehouse WHERE upper(name) = 'ALMACEN CENTRAL MBOS' ORDER BY warehouse_id LIMIT 1;
    END IF;
    IF v_central IS NULL THEN
        RAISE EXCEPTION 'No se encontró el almacén "ALMACEN CENTRAL MBOS" (ni "Almacen Central"). Almacenes: %',
            (SELECT string_agg(warehouse_id || ' ' || name, ', ' ORDER BY warehouse_id) FROM warehouse);
    END IF;

    -- El galpón es el antiguo ALMACEN CENTRAL MBON: solo tiene transferencias desde el central y
    -- su stock; así no se pierde ese historial. Si no existe, el galpón se crea más abajo.
    IF NOT EXISTS (SELECT 1 FROM warehouse WHERE name = 'Almacen Galpon Uno') THEN
        UPDATE warehouse SET name = 'Almacen Galpon Uno'
        WHERE warehouse_id = (SELECT warehouse_id FROM warehouse WHERE upper(name) = 'ALMACEN CENTRAL MBON' ORDER BY warehouse_id LIMIT 1);
    END IF;

    -- Los demás: todo lo suyo pasa al central y el almacén se elimina.
    FOR v_old IN
        SELECT warehouse_id FROM warehouse
        WHERE warehouse_id <> v_central AND name <> 'Almacen Galpon Uno'
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

    UPDATE warehouse SET name = 'Almacen Central', is_active = TRUE WHERE warehouse_id = v_central;

    IF NOT EXISTS (SELECT 1 FROM warehouse WHERE name = 'Almacen Galpon Uno') THEN
        INSERT INTO warehouse (name, is_active) VALUES ('Almacen Galpon Uno', TRUE);
    END IF;
    UPDATE warehouse SET is_active = TRUE WHERE name = 'Almacen Galpon Uno';
END $$;

-- La demo ya no tiene almacén propio: su registro se quita para que demoDataCleanup.sql no se
-- corra por error sobre datos que ahora son del central.
DROP TABLE IF EXISTS demo_seed_registry;

COMMIT;

-- Comprobación: deben salir solo los dos, con su stock.
SELECT w.warehouse_id, w.name, w.is_active,
       (SELECT coalesce(sum(s.quantity), 0) FROM stock s WHERE s.warehouse_id = w.warehouse_id) AS stock_total
FROM warehouse w ORDER BY w.warehouse_id;
