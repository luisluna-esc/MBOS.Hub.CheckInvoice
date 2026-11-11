-- =====================================================================================
-- Orden del menú Reportes (2026-11-04): los reportes que van de la mano quedan juntos.
--   Saldos: Stock - Almacén, Stock - Departamento, Levantamiento de Inventario
--   Kardex: Kardex Físico Valorado, Kardex por Material
--   Historiales: Historial de Entradas, Historial de Salidas
--   Movimientos: Ingresos de Almacén, Salidas de Almacén, Transferencias de Almacén
--   Pastores y cobros: Campo Pastor, Cartera de Cuentas por Cobrar
--   Contabilidad: Ingresos para Contabilidad, Salidas para Contabilidad
-- Solo cambia el orden. Se puede correr más de una vez.
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationOrdenReportes.sql
-- =====================================================================================

BEGIN;

UPDATE menu c SET display_order = o.display_order
FROM (VALUES
    ('Stock - Almacén', 1),
    ('Stock - Departamento', 2),
    ('Levantamiento de Inventario', 3),
    ('Kardex Físico Valorado', 4),
    ('Kardex por Material', 5),
    ('Historial de Entradas', 6),
    ('Historial de Salidas', 7),
    ('Ingresos de Almacén', 8),
    ('Salidas de Almacén', 9),
    ('Transferencias de Almacén', 10),
    ('Campo Pastor', 11),
    ('Cartera de Cuentas por Cobrar', 12),
    ('Contabilidad', 13)
) AS o(name, display_order), menu p
WHERE c.name = o.name AND c.parent_menu_id = p.menu_id AND p.name = 'Reportes' AND p.parent_menu_id IS NULL;

UPDATE menu c SET display_order = o.display_order
FROM (VALUES
    ('Ingresos para Contabilidad', 1),
    ('Salidas para Contabilidad', 2)
) AS o(name, display_order), menu p
WHERE c.name = o.name AND c.parent_menu_id = p.menu_id AND p.name = 'Contabilidad';

COMMIT;

-- Comprobación
SELECT p.name AS grupo, c.name AS reporte, c.display_order
FROM menu c JOIN menu p ON p.menu_id = c.parent_menu_id
WHERE p.name IN ('Reportes', 'Contabilidad')
ORDER BY p.name DESC, c.display_order;
