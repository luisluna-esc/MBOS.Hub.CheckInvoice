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
('Iglesia'), ('Congregacion'), ('Grupo'), ('Nucleo')
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