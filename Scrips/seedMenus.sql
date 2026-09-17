-- =====================================================================
-- Seed data for the dynamic sidebar menu (menu table). Add new rows
-- here as new frontend sections get built; parent_menu_id nests items.
--
-- permission_id is NULL on every row below: the granular permission
-- catalog was removed (2026-08-25) and will be replaced with a small
-- set of simple permissions. Wire permission_id back up once those
-- exist.
-- =====================================================================

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Inicio', 'nav.home', '/', 'home', NULL, 1, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Entradas', 'nav.receipts', '/receipts', 'receipt', NULL, 2, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Salidas', 'nav.issues', '/issues', 'issue', NULL, 3, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Catálogos', 'nav.catalogs', NULL, 'catalog', NULL, 4, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Proveedores', 'nav.suppliers', '/catalogs/suppliers', 'company', m.menu_id, 1, TRUE, NULL
FROM menu m
WHERE m.name = 'Catálogos' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Productos', 'nav.products', '/catalogs/products', 'product', m.menu_id, 2, TRUE, NULL
FROM menu m
WHERE m.name = 'Catálogos' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Clientes', 'nav.clients', '/catalogs/clients', 'client', m.menu_id, 3, TRUE, NULL
FROM menu m
WHERE m.name = 'Catálogos' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Transferencias', 'nav.transfers', NULL, 'transfer', NULL, 5, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Nueva Transferencia', 'nav.transfersNew', '/transfers/new', NULL, m.menu_id, 1, TRUE, NULL
FROM menu m
WHERE m.name = 'Transferencias' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Realizadas', 'nav.transfersSent', '/transfers/sent', NULL, m.menu_id, 2, TRUE, NULL
FROM menu m
WHERE m.name = 'Transferencias' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Recibidas', 'nav.transfersReceived', '/transfers/received', NULL, m.menu_id, 3, TRUE, NULL
FROM menu m
WHERE m.name = 'Transferencias' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Geografía', 'nav.geography', NULL, 'district', NULL, 6, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Distritos', 'nav.districts', '/geography/districts', NULL, m.menu_id, 1, TRUE, NULL
FROM menu m
WHERE m.name = 'Geografía' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Iglesias', 'nav.churches', '/geography/churches', NULL, m.menu_id, 2, TRUE, NULL
FROM menu m
WHERE m.name = 'Geografía' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Administración', 'nav.admin', NULL, 'admin', NULL, 7, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Usuarios', 'nav.users', '/admin/users', NULL, m.menu_id, 1, TRUE, NULL
FROM menu m
WHERE m.name = 'Administración' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Roles', 'nav.roles', '/admin/roles', NULL, m.menu_id, 2, TRUE, NULL
FROM menu m
WHERE m.name = 'Administración' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Permisos', 'nav.permissions', '/admin/permissions', NULL, m.menu_id, 3, TRUE, NULL
FROM menu m
WHERE m.name = 'Administración' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Anulaciones', 'nav.issueVoidRequests', '/issues/void-requests', 'void', NULL, 8, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Anulaciones de Entradas', 'nav.receiptVoidRequests', '/receipts/void-requests', 'void', NULL, 12, TRUE, NULL)
ON CONFLICT DO NOTHING;

-- Ajuste de Inventario: corrección libre de stock (ej. conteo físico), sin referenciar
-- ninguna Entrada/Salida existente. Internamente crea una Entrada tipo "Ajuste" o una Salida
-- tipo "Salida por Merma" según el signo de la diferencia, así queda dentro del mismo Kardex.
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Ajuste de Inventario', 'nav.stockAdjustment', '/stock-adjustments', 'adjustment', NULL, 14, TRUE, NULL)
ON CONFLICT DO NOTHING;

-- Devoluciones: apartado propio (no una acción dentro de Salidas). Se elige la Salida original
-- y se registra el retorno como Entrada tipo "Devolucion" referenciando esa Salida.
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Devoluciones', 'nav.issueReturns', '/issue-returns', 'return', NULL, 15, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Cuentas por Cobrar', 'nav.accountReceivables', '/account-receivables', 'receivable', NULL, 9, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Mi Cuenta', 'nav.myAccount', '/portal/my-account', 'portal', NULL, 10, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Reportes', 'nav.reports', NULL, 'report', NULL, 11, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Stock - Almacén', 'nav.reportsStock', '/reports/stock', NULL, m.menu_id, 2, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Stock - Departamento', 'nav.reportsStockByDepartment', '/reports/stock-by-department', NULL, m.menu_id, 3, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Kardex Físico Valorado', 'nav.reportsKardex', '/reports/kardex', NULL, m.menu_id, 4, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Levantamiento de Inventario', 'nav.reportsInventoryCount', '/reports/inventory-count', NULL, m.menu_id, 5, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Salidas de Almacén', 'nav.reportsIssues', '/reports/issues', NULL, m.menu_id, 6, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Ingresos de Almacén', 'nav.reportsReceipts', '/reports/receipts', NULL, m.menu_id, 7, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Kardex por Material', 'nav.reportsKardexByProduct', '/reports/kardex-by-product', NULL, m.menu_id, 8, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Campo Pastor', 'nav.reportsPastorField', '/reports/pastor-field', NULL, m.menu_id, 9, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

-- Nombre distinto al de la pantalla de gestión "Cuentas por Cobrar" (movimientos) para que
-- el filtro por nombre de menu_role no confunda ambos menús.
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Cartera de Cuentas por Cobrar', 'nav.reportsAccountReceivables', '/reports/account-receivables', NULL, m.menu_id, 10, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

-- Catálogos pequeños que hasta ahora solo se consumían como dropdown (via CatalogService)
-- sin ninguna pantalla de administración — el backend ya tenía CRUD completo para las 14,
-- solo faltaba el control desde el frontend. Grupo aparte de "Catálogos" (que es para
-- Proveedores/Productos/Clientes) para no mezclar catálogos de negocio con catálogos de
-- referencia/configuración del sistema.
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id) VALUES
('Configuración del Sistema', 'nav.settings', NULL, 'settings', NULL, 13, TRUE, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Países', 'nav.settingsCountries', '/admin/lookup/countries', NULL, m.menu_id, 1, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Tipos de Documento', 'nav.settingsDocumentTypes', '/admin/lookup/document-types', NULL, m.menu_id, 2, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Tipos de Iglesia', 'nav.settingsChurchTypes', '/admin/lookup/church-types', NULL, m.menu_id, 3, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Tipos de Impresión', 'nav.settingsPrintTypes', '/admin/lookup/print-types', NULL, m.menu_id, 4, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Tipos de Salida', 'nav.settingsIssueTypes', '/admin/lookup/issue-types', NULL, m.menu_id, 5, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Tipos de Entrada', 'nav.settingsReceiptTypes', '/admin/lookup/receipt-types', NULL, m.menu_id, 6, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Tipos de Medio', 'nav.settingsMediaTypes', '/admin/lookup/media-types', NULL, m.menu_id, 7, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Departamentos', 'nav.settingsDepartments', '/admin/lookup/departments', NULL, m.menu_id, 8, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Subdepartamentos', 'nav.settingsSubDepartments', '/admin/lookup/sub-departments', NULL, m.menu_id, 9, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Misiones', 'nav.settingsMissions', '/admin/lookup/missions', NULL, m.menu_id, 10, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Provincias', 'nav.settingsProvinces', '/admin/lookup/provinces', NULL, m.menu_id, 11, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Casos Especiales', 'nav.settingsSpecialCases', '/admin/lookup/special-cases', NULL, m.menu_id, 12, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Motivos de Anulación', 'nav.settingsVoidReasons', '/admin/lookup/void-reasons', NULL, m.menu_id, 13, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Almacenes', 'nav.settingsWarehouses', '/admin/lookup/warehouses', NULL, m.menu_id, 14, TRUE, NULL
FROM menu m
WHERE m.name = 'Configuración del Sistema' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;
