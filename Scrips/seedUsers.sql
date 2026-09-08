-- =====================================================================
-- Seed de 3 usuarios hardcodeados de prueba, uno por cada rol de negocio
-- (ver seedRoles.sql). El username es igual al rol que tienen, sin más.
-- Password en texto plano (solo para referencia, no se guarda así):
--   tesorero          -> tesorero123
--   contador          -> contador123
--   auxiliarcontador  -> auxiliarcontador123
-- =====================================================================

INSERT INTO app_user (first_name, last_name, email, username, password_hash, is_active) VALUES
('Tesorero', 'Demo', 'tesorero@mbos.local', 'tesorero', '$2a$11$jyDnLlTkmlXrO2OyA10vX.wPBRyhwo/32S2.RJVtn1kR1rXo0uqwC', TRUE),
('Contador', 'Demo', 'contador@mbos.local', 'contador', '$2a$11$WojHBeYEnaaCzjAoIehjbO0j4Xr.7hzVGqt/1d15aZ8SUjjdL5zTS', TRUE),
('Auxiliar Contador', 'Demo', 'auxiliarcontador@mbos.local', 'auxiliarcontador', '$2a$11$JYxxyoSo03BgNHcjkj9Zyu3VwhwPHpuKYR1tx2JzXky430SRMX3Cm', TRUE)
ON CONFLICT (username) DO NOTHING;

-- Asignar a cada usuario su rol correspondiente
INSERT INTO app_user_role (app_user_id, role_id)
SELECT u.app_user_id, r.role_id
FROM app_user u
JOIN role r ON r.name = 'Tesorero'
WHERE u.username = 'tesorero'
ON CONFLICT (app_user_id, role_id) DO NOTHING;

INSERT INTO app_user_role (app_user_id, role_id)
SELECT u.app_user_id, r.role_id
FROM app_user u
JOIN role r ON r.name = 'Contador'
WHERE u.username = 'contador'
ON CONFLICT (app_user_id, role_id) DO NOTHING;

INSERT INTO app_user_role (app_user_id, role_id)
SELECT u.app_user_id, r.role_id
FROM app_user u
JOIN role r ON r.name = 'Auxiliar Contador'
WHERE u.username = 'auxiliarcontador'
ON CONFLICT (app_user_id, role_id) DO NOTHING;
