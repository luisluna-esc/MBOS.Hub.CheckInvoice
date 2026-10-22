-- =====================================================================================
-- Carga de pastores (planilla "Misioneros 4T", 41 pastores con su C.I. y distrito).
-- Se puede correr más de una vez: no duplica pastores ni distritos.
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f dataPastores.sql
--
-- 1. Distritos con nombre viejo en el sistema: se renombran al nombre actual (conservan sus
--    iglesias). 2. Distritos que faltan: se crean. 3. Cada pastor: si ya existe (por C.I. o, si
--    no, por nombre) se actualiza su nombre, C.I. y distrito con los de la planilla y queda
--    marcado como pastor activo; si no existe, se crea. Los nombres van con los apellidos
--    primero ("Tarqui Huanca Isaac"), igual en todo el sistema.
-- =====================================================================================

-- El archivo está en UTF-8 (nombres con ñ y acentos), sin importar la consola que lo corra.
SET client_encoding = 'UTF8';

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
    (1, 'Tarqui Huanca Isaac', '6875340', '25 de Julio'),
    (2, 'Apaza Quiñones Hugo', '6915986', '27 de Mayo'),
    (3, 'Surco Yupanqui Edwin', '4514588', '27 de Septiembre'),
    (4, 'Sirpa Alcon Juan Milton', '12449941', 'Araca'),
    (5, 'Poma Perez Luis Alfredo', '7043523', 'Aroma'),
    (6, 'Bautista Calle Miguel Angel', '7020616', 'Atipiris'),
    (7, 'Coronel Laime Henry Wilson', '4800042', 'Bolivar'),
    (8, 'Callisaya Chinahuanca Roly', '7037306', 'Cajuata'),
    (9, 'Tarqui Chura Efrain', '9239792', 'Charapaqui'),
    (10, 'Valeriano Condori Absalon Eleazar', '13440376', 'Collana'),
    (11, 'Quiuchaca Apaza Nelo Franklin', '8439312', 'Cosapa'),
    (12, 'Larico Aruquipa Noel', '6188628', 'Cosmos'),
    (13, 'Chino Chipana John', '3424647', 'Cupilupaca'),
    (14, 'Condori Canaviri Alvaro Gabriel', '6853028', 'Curahuara'),
    (15, 'Pomacusi Cari Mario Alejandro', '8354769', 'El Porvenir'),
    (16, 'Pairo Mamani Ever', '10062204', 'El Salvador'),
    (17, 'Llanos Cohaila Omar', '8304655', 'El Valle'),
    (18, 'Pari Tipo Mequias', '6178853', 'Fabril'),
    (19, 'Mamani Mamani Sergio', '7005521', 'Inquisivi'),
    (20, 'Moya Condori Ronald', '6955431', 'La Hermosa'),
    (21, 'Quispe Espinoza Juan Eusebio', '7055964', 'Lahuachaca'),
    (22, 'Quispe Sánchez Josué', '8404262', 'Las Fronteras'),
    (23, 'Pinto Salvatierra Mario Carlos', '5033881', 'Maranatha'),
    (24, 'Quipildor Tito Juan Luis', '2621402', 'Mariscal'),
    (25, 'Aguilar Conde Emmanuel', '10387002', 'Muruamaya'),
    (26, 'Mamani Limachi Fidel John', '3478403', 'Pacajes'),
    (27, 'Lima Huanca Juan Carlos', '4834871', 'Patacamaya'),
    (28, 'Chuquimia Lucana Robie', '10805636', 'Pathipi'),
    (29, 'Sajama Sanchez David', '7278224', 'Rosario'),
    (30, 'Aliaga Quispe Fidel', '4843050', 'San Martín'),
    (31, 'Valdez Laura Franz Ciro', '3461744', 'Santa Rosa'),
    (32, 'Chipana Serrano Mauro', '6089115', 'Santiago de Machaca'),
    (33, 'Llusco Chávez Edson Plinio', '6890587', 'Senkata 79'),
    (34, 'Chambilla Esprella Rubén', '6768769', 'Tilata'),
    (35, 'Tarquiola Coria Percy', '7080473', 'Ventilla'),
    (36, 'Escarzo Nina Jose Edwin', '4046655', 'Viacha'),
    (37, 'Mena Laura Benjamin Josue', '6858984', 'Vichaya'),
    (38, 'Gutiérrez Limachi David', '4774964', 'Villa Adela'),
    (39, 'Llanos Cohaila Adhemar', '12955578', 'Villa Aroma'),
    (40, 'Morante Kondory Manuel Huascar', '3434083', 'Villa Juliana'),
    (41, 'Chambi Catunta Wilmer Elio', '9963954', 'Villa Remedios');

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
            UPDATE party SET tax_id = p.tax_id, name = trim(p.name) WHERE party_id = v_party;
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
