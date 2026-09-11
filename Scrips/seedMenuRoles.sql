-- =====================================================================
-- Seed data for menu_role: a sidebar menu item is visible to a role
-- only if there is a row here for that (menu, role) pair. A menu with
-- no rows is not visible to anyone; a new menu needs an explicit row
-- per role that should see it.
-- The parent group ("Administración") auto-hides itself when none of
-- its children are visible, so only the routed children need rows.
--
-- Every explicitly-restricted route below must also be excluded from
-- the general "visible to all active roles" catch-all at the bottom,
-- or the catch-all re-grants it to every role and the restriction
-- becomes a no-op on a fresh install.
-- =====================================================================

-- Administración: solo M-BOS.
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.route IN ('/admin/users', '/admin/roles', '/admin/permissions')
  AND r.name = 'M-BOS'
ON CONFLICT DO NOTHING;

-- Anulaciones: solo a quien le importa (quien pide, y quienes aprueban).
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.route = '/issues/void-requests'
  AND r.name IN ('Auxiliar Contador', 'Contador', 'M-BOS')
ON CONFLICT DO NOTHING;

-- Anulaciones de Entradas: mismo criterio que Anulaciones (de Salidas).
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.route = '/receipts/void-requests'
  AND r.name IN ('Auxiliar Contador', 'Contador', 'M-BOS')
ON CONFLICT DO NOTHING;

-- Ajuste de Inventario: mismo criterio que Anulaciones (roles con permiso "trabajo").
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.route = '/stock-adjustments'
  AND r.name IN ('Auxiliar Contador', 'Contador', 'M-BOS')
ON CONFLICT DO NOTHING;

-- Devoluciones: mismo criterio que Ajuste de Inventario (roles con permiso "trabajo").
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.route = '/issue-returns'
  AND r.name IN ('Auxiliar Contador', 'Contador', 'M-BOS')
ON CONFLICT DO NOTHING;

-- Cuentas por Cobrar: gestión de cobranza, no es para Pastor (eso es "Mi Cuenta").
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.route = '/account-receivables'
  AND r.name IN ('Auxiliar Contador', 'Contador', 'Tesorero', 'M-BOS')
ON CONFLICT DO NOTHING;

-- Mi Cuenta: autoservicio, solo Pastor.
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.route = '/portal/my-account'
  AND r.name = 'Pastor'
ON CONFLICT DO NOTHING;

-- Reportes (grupo + sus hijos): informes de gestión de toda la organización, no es
-- para Pastor. "Reportes" es un grupo sin ruta propia (route IS NULL) desde que tiene
-- submenús, así que se identifica por nombre, no por ruta — igual que sus hijos.
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.name IN ('Reportes', 'Reporte Financiero', 'Stock - Almacén', 'Stock - Departamento', 'Kardex Físico Valorado', 'Levantamiento de Inventario', 'Salidas de Almacén', 'Ingresos de Almacén', 'Kardex por Material', 'Campo Pastor', 'Cartera de Cuentas por Cobrar')
  AND r.name IN ('Auxiliar Contador', 'Contador', 'Tesorero', 'M-BOS')
ON CONFLICT DO NOTHING;

-- Configuración del Sistema (grupo + sus 14 hijos): catálogos pequeños de referencia
-- (tipos, países, motivos de anulación, almacenes...) — control de administrador, igual
-- que "Administración", solo para M-BOS. Grupo sin ruta propia, igual que "Reportes",
-- así que se identifica por nombre.
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.name IN (
    'Configuración del Sistema', 'Países', 'Tipos de Documento', 'Tipos de Iglesia', 'Tipos de Impresión',
    'Tipos de Salida', 'Tipos de Entrada', 'Tipos de Medio', 'Departamentos', 'Subdepartamentos',
    'Misiones', 'Provincias', 'Casos Especiales', 'Motivos de Anulación', 'Almacenes'
  )
  AND r.name = 'M-BOS'
ON CONFLICT DO NOTHING;

-- Todo lo demás: visible para todos los roles activos.
-- Nota: la exclusión es por NOMBRE, no por ruta — un grupo restringido sin ruta propia
-- (route IS NULL, como "Reportes") pasaría la condición "m.route IS NULL" de un filtro
-- basado en ruta y quedaría mal otorgado a todos los roles pese a la regla explícita de
-- arriba. Por nombre no depende de si el menú tiene ruta o no.
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m
CROSS JOIN role r
WHERE r.is_active
  AND m.name NOT IN (
    'Usuarios', 'Roles', 'Permisos',
    'Anulaciones',
    'Anulaciones de Entradas',
    'Ajuste de Inventario',
    'Devoluciones',
    'Cuentas por Cobrar',
    'Mi Cuenta',
    'Reportes', 'Reporte Financiero', 'Stock - Almacén', 'Stock - Departamento', 'Kardex Físico Valorado', 'Levantamiento de Inventario', 'Salidas de Almacén', 'Ingresos de Almacén', 'Kardex por Material', 'Campo Pastor', 'Cartera de Cuentas por Cobrar',
    'Configuración del Sistema', 'Países', 'Tipos de Documento', 'Tipos de Iglesia', 'Tipos de Impresión',
    'Tipos de Salida', 'Tipos de Entrada', 'Tipos de Medio', 'Departamentos', 'Subdepartamentos',
    'Misiones', 'Provincias', 'Casos Especiales', 'Motivos de Anulación', 'Almacenes'
  )
ON CONFLICT DO NOTHING;
