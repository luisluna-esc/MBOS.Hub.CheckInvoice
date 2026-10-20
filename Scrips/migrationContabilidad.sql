-- =====================================================================================
-- Informes para contabilidad (formato de importación "Transaction_Entry").
-- Se puede correr más de una vez. Correr ANTES de levantar el backend nuevo.
--
-- Cada producto de una salida sale en una fila con:
--   AccountCode 4121565 | SubAccountCode = código del departamento del producto |
--   FundCode 10 | FunctionCode 0000 | RestrictionCode 0A | EntityValue = total (negativo) |
--   SendMemo False | Description "S-N° - X Uds. Producto"
-- =====================================================================================

-- 1. Departamento: código contable (SubAccountCode). Se carga después en
--    Configuración del Sistema / Departamentos; mientras esté vacío, el informe lo avisa.
ALTER TABLE department ADD COLUMN IF NOT EXISTS code VARCHAR(20);

-- 2. Producto: códigos contables fijos, guardados al crear el producto (no se muestran
--    en pantalla). Los productos que ya existen quedan con los mismos valores.
ALTER TABLE product ADD COLUMN IF NOT EXISTS fund_code VARCHAR(10) NOT NULL DEFAULT '10';
ALTER TABLE product ADD COLUMN IF NOT EXISTS function_code VARCHAR(10) NOT NULL DEFAULT '0000';
ALTER TABLE product ADD COLUMN IF NOT EXISTS restriction_code VARCHAR(10) NOT NULL DEFAULT '0A';

-- 3. Menú: Reportes / Salidas para Contabilidad (los mismos roles que Salidas de Almacén).
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Salidas para Contabilidad', 'nav.reportsIssuesAccounting', '/reports/issues-accounting', NULL, m.menu_id, 12, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu_role (menu_id, role_id)
SELECT t.menu_id, mr.role_id
FROM menu t
JOIN menu i ON i.name = 'Salidas de Almacén'
JOIN menu_role mr ON mr.menu_id = i.menu_id
WHERE t.name = 'Salidas para Contabilidad'
ON CONFLICT DO NOTHING;

-- 3b. Menú: Reportes / Ingresos para Contabilidad (los mismos roles que Ingresos de Almacén).
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Ingresos para Contabilidad', 'nav.reportsReceiptsAccounting', '/reports/receipts-accounting', NULL, m.menu_id, 13, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu_role (menu_id, role_id)
SELECT t.menu_id, mr.role_id
FROM menu t
JOIN menu i ON i.name = 'Ingresos de Almacén'
JOIN menu_role mr ON mr.menu_id = i.menu_id
WHERE t.name = 'Ingresos para Contabilidad'
ON CONFLICT DO NOTHING;

-- 4. Códigos contables de los departamentos ("Código de sub cuenta inventario", contabilidad).
--    Solo llena los que están vacíos, para no pisar un código corregido a mano en la pantalla.
--    Departamentos del sistema sin código en esa lista: Asesoría de Prensa y Comunicación,
--    Auditoría, Contabilidad, Departamento de Limpieza, Departamento Jurídico, Diáconos y
--    Diaconisas, Libro Misionero y Ministerio de Música (se cargan a mano cuando se definan).
UPDATE department d
SET code = c.code
FROM (VALUES
    ('MINISTERIO DEL MENOR', '100'),
    ('MINISTERIO DEL ADOLESCENTE', '100'),
    ('MINISTERIO DE LA MUJER', '101'),
    ('EVANGELISMO', '102'),
    ('MINISTERIO JOVEN', '103'),
    ('MAYORDOMIA', '104'),
    ('ASOCIACIÓN MINISTERIAL', '105'),
    ('AVENTUREROS', '106'),
    ('CONQUISTADORES', '106'),
    ('MINISTERIO PERSONAL', '107'),
    ('HOGAR Y FAMILIA', '108'),
    ('SALUD', '109'),
    ('EDUCACIÓN', '110'),
    ('PUBLICACIONES', '111'),
    ('ESCUELA SÁBATICA', '112'),
    ('CALEB', '113'),
    ('ASA', '114'),
    ('OFICINA EN GENERAL', '115'),
    ('GESTIÓN DEL TALENTO HUMANO', '116'),
    ('TESORERIA', '117'),
    ('SECRETARIA', '118'),
    ('PRESIDENCIA', '124')
) AS c(name, code)
WHERE upper(d.name) = c.name
  AND (d.code IS NULL OR d.code = '');

-- 5. Departamentos que no tienen código contable: se borran. Sus productos no se borran
--    (tienen entradas, salidas y stock): quedan sin departamento y conservan su historial.
UPDATE product p
SET department_id = NULL
FROM department d
WHERE p.department_id = d.department_id
  AND upper(d.name) IN ('AUDITORIA', 'CONTABILIDAD', 'DEPARTAMENTO DE LIMPIEZA', 'DEPARTAMENTO JURIDICO',
                        'DIÁCONOS Y DIACONISAS', 'ASESORIA DE PRENSA Y COMUNICACIÓN', 'LIBRO MISIONERO', 'MINISTERIO DE MUSICA')
  AND (d.code IS NULL OR d.code = '');

DELETE FROM department d
WHERE upper(d.name) IN ('AUDITORIA', 'CONTABILIDAD', 'DEPARTAMENTO DE LIMPIEZA', 'DEPARTAMENTO JURIDICO',
                        'DIÁCONOS Y DIACONISAS', 'ASESORIA DE PRENSA Y COMUNICACIÓN', 'LIBRO MISIONERO', 'MINISTERIO DE MUSICA')
  AND (d.code IS NULL OR d.code = '');

-- Comprobación
SELECT COUNT(*) AS productos, COUNT(*) FILTER (WHERE fund_code = '10' AND function_code = '0000' AND restriction_code = '0A') AS con_codigos
FROM product;
SELECT department_id, name, code FROM department ORDER BY name;
SELECT m.name, string_agg(r.name, ', ' ORDER BY r.name) AS roles
FROM menu m JOIN menu_role mr ON mr.menu_id = m.menu_id JOIN role r ON r.role_id = mr.role_id
WHERE m.name IN ('Salidas para Contabilidad', 'Ingresos para Contabilidad')
GROUP BY m.name;
