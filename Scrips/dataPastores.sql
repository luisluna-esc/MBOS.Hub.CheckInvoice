-- =====================================================================================
-- Carga de pastores (planilla "Misioneros 4T", 41 pastores con su C.I. y distrito).
-- Se puede correr más de una vez: no duplica pastores ni distritos.
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f dataPastores.sql
--
-- 1. Distritos con nombre viejo en el sistema: se renombran al nombre actual (conservan sus
--    iglesias). 2. Distritos que faltan: se crean. 3. Cada pastor: si ya existe (por C.I. o, si
--    no, por nombre) se actualiza su C.I. y su distrito con los de la planilla y queda marcado
--    como pastor activo; si no existe, se crea.
-- =====================================================================================

BEGIN;

-- 1. Nombre viejo -> nombre actual (el de la planilla de Misioneros y la de iglesias 2026).
UPDATE district d
SET district_name = r.new_name
FROM (VALUES
    ('El Valle - Araca', 'Araca'),
    ('Curahuara - Cosapa', 'Cosapa'),
    ('Patacamaya El Porvenir', 'El Porvenir'),
    ('Viacha - Fabril', 'Fabril'),
    ('Muroamaya', 'Muruamaya'),
    ('Patacamaya Central', 'Patacamaya'),
    ('Patihipi', 'Pathipi')
) AS r(old_name, new_name)
WHERE d.district_name = r.old_name
  AND NOT EXISTS (SELECT 1 FROM district x WHERE lower(x.district_name) = lower(r.new_name));

-- 2. Distritos que no existían.
INSERT INTO district (district_name)
SELECT n.name
FROM (VALUES ('27 de Septiembre'), ('Aroma')) AS n(name)
WHERE NOT EXISTS (SELECT 1 FROM district x WHERE lower(x.district_name) = lower(n.name));

-- 3. Pastores.
CREATE TEMP TABLE tmp_pastor (n INT, name VARCHAR(150), tax_id VARCHAR(20), district VARCHAR(100)) ON COMMIT DROP;
INSERT INTO tmp_pastor (n, name, tax_id, district) VALUES
    (1, 'Isaac Tarqui Huanca', '6875340', '25 de Julio'),
    (2, 'Hugo Apaza Quiñones', '6915986', '27 de Mayo'),
    (3, 'Edwin Surco Yupanqui', '4514588', '27 de Septiembre'),
    (4, 'Juan Milton Sirpa Alcon', '12449941', 'Araca'),
    (5, 'Luis Alfredo Poma Perez', '7043523', 'Aroma'),
    (6, 'Miguel Angel Bautista Calle', '7020616', 'Atipiris'),
    (7, 'Henry Wilson Coronel Laime', '4800042', 'Bolivar'),
    (8, 'Roly Callisaya Chinahuanca', '7037306', 'Cajuata'),
    (9, 'Efrain Tarqui Chura', '9239792', 'Charapaqui'),
    (10, 'Absalon Eleazar Valeriano Condori', '13440376', 'Collana'),
    (11, 'Nelo Franklin Quiuchaca Apaza', '8439312', 'Cosapa'),
    (12, 'Noel Larico Aruquipa', '6188628', 'Cosmos'),
    (13, 'John Chino Chipana', '3424647', 'Cupilupaca'),
    (14, 'Alvaro Gabriel Condori Canaviri', '6853028', 'Curahuara'),
    (15, 'Mario Alejandro Pomacusi Cari', '8354769', 'El Porvenir'),
    (16, 'Ever Pairo Mamani', '10062204', 'El Salvador'),
    (17, 'Omar Llanos Cohaila', '8304655', 'El Valle'),
    (18, 'Mequias Pari Tipo', '6178853', 'Fabril'),
    (19, 'Sergio Mamani Mamani', '7005521', 'Inquisivi'),
    (20, 'Ronald Moya Condori', '6955431', 'La Hermosa'),
    (21, 'Juan Eusebio Quispe Espinoza', '7055964', 'Lahuachaca'),
    (22, 'Josué Quispe Sánchez', '8404262', 'Las Fronteras'),
    (23, 'Mario Carlos Pinto Salvatierra', '5033881', 'Maranatha'),
    (24, 'Juan Luis Quipildor Tito', '2621402', 'Mariscal'),
    (25, 'Emmanuel Aguilar Conde', '10387002', 'Muruamaya'),
    (26, 'Fidel John Mamani Limachi', '3478403', 'Pacajes'),
    (27, 'Juan Carlos Lima Huanca', '4834871', 'Patacamaya'),
    (28, 'Robie Chuquimia Lucana', '10805636', 'Pathipi'),
    (29, 'David Sajama Sanchez', '7278224', 'Rosario'),
    (30, 'Fidel Aliaga Quispe', '4843050', 'San Martín'),
    (31, 'Franz Ciro Valdez Laura', '3461744', 'Santa Rosa'),
    (32, 'Mauro Chipana Serrano', '6089115', 'Santiago de Machaca'),
    (33, 'Edson Plinio Llusco Chávez', '6890587', 'Senkata 79'),
    (34, 'Rubén Chambilla Esprella', '6768769', 'Tilata'),
    (35, 'Percy Tarquiola Coria', '7080473', 'Ventilla'),
    (36, 'Jose Edwin Escarzo Nina', '4046655', 'Viacha'),
    (37, 'Benjamin Josue Mena Laura', '6858984', 'Vichaya'),
    (38, 'David Gutiérrez Limachi', '4774964', 'Villa Adela'),
    (39, 'Adhemar Llanos Cohaila', '12955578', 'Villa Aroma'),
    (40, 'Manuel Huascar Morante Kondory', '3434083', 'Villa Juliana'),
    (41, 'Wilmer Elio Chambi Catunta', '9963954', 'Villa Remedios');

CREATE TEMP TABLE tmp_result (n INT, name VARCHAR(150), action TEXT) ON COMMIT DROP;

DO $$
DECLARE
    p RECORD;
    v_party BIGINT;
    v_client BIGINT;
    v_district BIGINT;
    v_doc BIGINT := (SELECT document_type_id FROM document_type WHERE name = 'Cedula de Identidad' LIMIT 1);
    -- Sin acentos ni mayúsculas, para comparar nombres.
    v_from TEXT := 'áéíóúÁÉÍÓÚñÑüÜ';
    v_to   TEXT := 'aeiouAEIOUnNuU';
BEGIN
    FOR p IN SELECT * FROM tmp_pastor ORDER BY n LOOP
        SELECT district_id INTO v_district
        FROM district
        WHERE lower(translate(trim(district_name), v_from, v_to)) = lower(translate(trim(p.district), v_from, v_to))
        LIMIT 1;
        IF v_district IS NULL THEN
            RAISE EXCEPTION 'No existe el distrito "%" (pastor %)', p.district, p.name;
        END IF;

        -- Por C.I.; si no, por nombre.
        SELECT party_id INTO v_party FROM party WHERE tax_id = p.tax_id LIMIT 1;
        IF v_party IS NULL THEN
            SELECT party_id INTO v_party FROM party
            WHERE lower(translate(trim(name), v_from, v_to)) = lower(translate(trim(p.name), v_from, v_to))
            LIMIT 1;
        END IF;

        IF v_party IS NULL THEN
            INSERT INTO party (name, tax_id) VALUES (trim(p.name), p.tax_id) RETURNING party_id INTO v_party;
            INSERT INTO client (party_id, document_type_id, district_id, is_active, is_pastor)
            VALUES (v_party, v_doc, v_district, TRUE, TRUE);
            INSERT INTO tmp_result VALUES (p.n, p.name, 'creado');
        ELSE
            UPDATE party SET tax_id = p.tax_id WHERE party_id = v_party;
            SELECT client_id INTO v_client FROM client WHERE party_id = v_party;
            IF v_client IS NULL THEN
                INSERT INTO client (party_id, document_type_id, district_id, is_active, is_pastor)
                VALUES (v_party, v_doc, v_district, TRUE, TRUE);
                INSERT INTO tmp_result VALUES (p.n, p.name, 'ya existía, ahora también es pastor');
            ELSE
                UPDATE client
                SET district_id = v_district,
                    is_pastor = TRUE,
                    is_active = TRUE,
                    document_type_id = COALESCE(document_type_id, v_doc)
                WHERE client_id = v_client;
                INSERT INTO tmp_result VALUES (p.n, p.name, 'actualizado');
            END IF;
        END IF;
        v_party := NULL;
        v_client := NULL;
        v_district := NULL;
    END LOOP;
END $$;

SELECT action AS resultado, count(*) AS pastores FROM tmp_result GROUP BY action ORDER BY action;

COMMIT;

-- Comprobación: los 41 pastores con su distrito.
SELECT d.district_name AS distrito, p.tax_id AS ci, p.name AS pastor
FROM client c
JOIN party p ON p.party_id = c.party_id
LEFT JOIN district d ON d.district_id = c.district_id
WHERE c.is_pastor
ORDER BY d.district_name, p.name;
