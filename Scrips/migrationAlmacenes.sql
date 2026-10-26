-- =====================================================================================
-- Almacenes (2026-10-26): solo quedan dos, "Almacen Central" y "Almacen Galpon Uno".
-- Se puede correr más de una vez.
--
--   * El almacén central actual (ALMACEN CENTRAL MBOS) pasa a llamarse "Almacen Central",
--     con todos sus movimientos y su stock.
--   * "Almacen Galpon Uno" se crea si no existe.
--   * Los demás almacenes se eliminan, pero SOLO si no tienen nada registrado. Si alguno tiene
--     entradas, salidas, transferencias, conteos, notas pendientes o stock, el script se detiene
--     sin cambiar nada y dice cuál es (borrarlo se llevaría su historial).
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationAlmacenes.sql
-- =====================================================================================

BEGIN;

DO $$
DECLARE
    v_central BIGINT;
    v_with_data TEXT;
BEGIN
    SELECT warehouse_id INTO v_central FROM warehouse WHERE name = 'Almacen Central' ORDER BY warehouse_id LIMIT 1;
    IF v_central IS NULL THEN
        SELECT warehouse_id INTO v_central FROM warehouse WHERE upper(name) = 'ALMACEN CENTRAL MBOS' ORDER BY warehouse_id LIMIT 1;
    END IF;
    IF v_central IS NULL THEN
        RAISE EXCEPTION 'No se encontró el almacén "ALMACEN CENTRAL MBOS" (ni "Almacen Central"). Almacenes: %',
            (SELECT string_agg(warehouse_id || ' ' || name, ', ' ORDER BY warehouse_id) FROM warehouse);
    END IF;

    -- Los que sobran y tienen algo registrado: no se tocan, se avisa.
    SELECT string_agg(w.name, ', ' ORDER BY w.warehouse_id) INTO v_with_data
    FROM warehouse w
    WHERE w.warehouse_id <> v_central
      AND w.name <> 'Almacen Galpon Uno'
      AND (EXISTS (SELECT 1 FROM receipt x WHERE x.warehouse_id = w.warehouse_id)
        OR EXISTS (SELECT 1 FROM issue x WHERE x.warehouse_id = w.warehouse_id)
        OR EXISTS (SELECT 1 FROM issue_batch x WHERE x.warehouse_id = w.warehouse_id)
        OR EXISTS (SELECT 1 FROM transfer x WHERE w.warehouse_id IN (x.source_warehouse_id, x.destination_warehouse_id))
        OR EXISTS (SELECT 1 FROM inventory_count x WHERE x.warehouse_id = w.warehouse_id)
        OR EXISTS (SELECT 1 FROM pending_receipt x WHERE x.warehouse_id = w.warehouse_id)
        OR EXISTS (SELECT 1 FROM stock x WHERE x.warehouse_id = w.warehouse_id AND x.quantity <> 0));
    IF v_with_data IS NOT NULL THEN
        RAISE EXCEPTION 'No se cambió nada: estos almacenes tienen movimientos o stock y no se pueden eliminar: %', v_with_data;
    END IF;

    -- Sin nada registrado: fuera (con sus filas de stock en cero).
    DELETE FROM stock s USING warehouse w
    WHERE s.warehouse_id = w.warehouse_id AND w.warehouse_id <> v_central AND w.name <> 'Almacen Galpon Uno';
    DELETE FROM warehouse WHERE warehouse_id <> v_central AND name <> 'Almacen Galpon Uno';

    UPDATE warehouse SET name = 'Almacen Central', is_active = TRUE WHERE warehouse_id = v_central;

    IF NOT EXISTS (SELECT 1 FROM warehouse WHERE name = 'Almacen Galpon Uno') THEN
        INSERT INTO warehouse (name, is_active) VALUES ('Almacen Galpon Uno', TRUE);
    END IF;
    UPDATE warehouse SET is_active = TRUE WHERE name = 'Almacen Galpon Uno';
END $$;

COMMIT;

-- Comprobación: deben salir solo los dos.
SELECT warehouse_id, name, is_active FROM warehouse ORDER BY warehouse_id;
