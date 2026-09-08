-- =====================================================================
-- Generic seed data for catalog tables. Adjust/replace with real values
-- whenever they're available.
-- =====================================================================

INSERT INTO country (name) VALUES
('Bolivia'), ('Argentina'), ('Brasil'), ('Chile'), ('Colombia'),
('Ecuador'), ('Paraguay'), ('Peru'), ('Uruguay'), ('Venezuela'),
('Espana'), ('Estados Unidos')
ON CONFLICT DO NOTHING;

INSERT INTO document_type (name) VALUES
('Cedula de Identidad'), ('Pasaporte'), ('NIT'), ('RUC'), ('Carnet de Extranjeria')
ON CONFLICT DO NOTHING;

INSERT INTO church_type (name) VALUES
('Iglesia'), ('Congregacion'), ('Grupo'), ('Nucleo'), ('Filial')
ON CONFLICT DO NOTHING;

-- Clasificación vigente para iglesias nuevas (ver CURRENT_CHURCH_TYPE_NAMES en
-- church-form-dialog.ts). Los tipos de arriba se mantienen solo por las iglesias ya
-- importadas del Excel; el formulario de creación ya no los ofrece.
INSERT INTO church_type (name) VALUES
('Organizada'), ('Corporacion')
ON CONFLICT DO NOTHING;

INSERT INTO mission (name) VALUES
('MISIÓN BOLIVIANA OCCIDENTAL DEL NORTE'), ('UNIÓN BOLIVIANA'), ('MISIÓN DEL ORIENTE BOLIVIANO'),
('MISIÓN BOLIVIANA CENTRAL'), ('MISIÓN BOLIVIANA OCCIDENTAL DEL SUR')
ON CONFLICT DO NOTHING;

INSERT INTO province (name) VALUES
('COCHABAMBA'), ('LA PAZ'), ('EL ALTO'), ('SANTA CRUZ'), ('ORURO'), ('VINTO'), ('TRINIDAD'),
('MONTERO'), ('WARNES'), ('GUAYARAMERIN'), ('YACUIBA'), ('QUILLACOLLO'), ('ENTRE RIOS'),
('PUERTO QUIJARRO'), ('YAPACANI'), ('TARIJA')
ON CONFLICT DO NOTHING;

INSERT INTO issue_type (name, is_active) VALUES
('Salida Normal', TRUE), ('Salida por Regalo', TRUE), ('Salida por Prestamo', TRUE), ('Salida por Merma', TRUE)
ON CONFLICT DO NOTHING;

INSERT INTO receipt_type (name, is_active) VALUES
('Compra', TRUE), ('Donacion', TRUE), ('Devolucion', TRUE), ('Ajuste', TRUE)
ON CONFLICT DO NOTHING;

INSERT INTO print_type (name) VALUES
('Factura'), ('Recibo'), ('Nota de Entrega'), ('Sin Impresion')
ON CONFLICT DO NOTHING;

INSERT INTO media_type (name, is_active) VALUES
('Libro', TRUE), ('Revista', TRUE), ('Folleto', TRUE), ('DVD', TRUE), ('Multimedia', TRUE)
ON CONFLICT DO NOTHING;