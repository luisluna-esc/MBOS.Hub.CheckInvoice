-- =====================================================================================
-- Despliegue de noviembre 2026: deja la base de producción igual que la local.
-- Corre en orden todos los scripts de noviembre. Cada uno se puede correr más de una vez.
--
-- Correr como postgres, desde la carpeta Scrips (después del git pull):
--   cd /opt/inventrack/backend/Scrips
--   sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f desplegarNoviembre.sql
-- =====================================================================================

\set ON_ERROR_STOP on

-- Antes de nada: deben existir las tablas de los scripts de octubre. Si falta alguna, se para
-- aquí sin cambiar nada y dice qué script correr primero.
DO $$
DECLARE
    v_missing TEXT;
BEGIN
    SELECT string_agg(t.name || ' (' || t.script || ')', ', ') INTO v_missing
    FROM (VALUES
        ('document_review', 'migrationEntradasCompras.sql'),
        ('inbox_seen', 'migrationEntradasCompras.sql'),
        ('issue_batch', 'migrationRevision1.sql')
    ) AS t(name, script)
    WHERE to_regclass('public.' || t.name) IS NULL;
    IF v_missing IS NOT NULL THEN
        RAISE EXCEPTION 'Faltan tablas de octubre: %. Corre primero esos scripts.', v_missing;
    END IF;
END $$;

\echo '1/9 Notas de ingreso sin factura (crea sus tablas si faltan) y orden del menú'
\ir migrationNotasPendientes.sql
\echo '2/9 Solo dos almacenes: Almacén Central y Almacén Galpón Uno'
\ir migrationAlmacenes.sql
\echo '3/9 Correcciones: las revisan Contador, Tesorero y M-BOS'
\ir migrationCorrecciones.sql
\echo '4/9 Buzón: el lote se firma una sola vez'
\ir migrationFirmaLote.sql
\echo '5/9 Orden del menú Reportes'
\ir migrationOrdenReportes.sql
\echo '6/9 Saldos de stock al costo promedio, sin negativos'
\ir migrationSaldosStock.sql
\echo '7/9 Salida por lote con costos'
\ir migrationLoteCostos.sql
\echo '8/9 Cartera de cuentas por cobrar con saldo arrastrado'
\ir migrationCarteraSaldo.sql
\echo '9/9 Saldos iniciales que faltaban (después de juntar los almacenes)'
\ir migrationSaldosIniciales.sql

\echo 'Listo. Almacenes que quedaron:'
SELECT warehouse_id, name, is_active FROM warehouse ORDER BY warehouse_id;
