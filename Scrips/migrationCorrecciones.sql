-- =====================================================================================
-- Correcciones de Entradas y de Salidas (2026-11-04): las revisan Contador, M-BOS y Tesorero
-- (nunca quien la pidió). El Auxiliar Contador las ve para seguir las que pidió, pero no revisa. El grupo
-- "Autorizaciones" aparece solo cuando el rol ve alguna pantalla de adentro.
-- Se puede correr más de una vez.
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationCorrecciones.sql
-- =====================================================================================

INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m, role r
WHERE m.name IN ('Correcciones de Entradas', 'Correcciones de Salidas')
  AND r.name IN ('Contador', 'M-BOS', 'Tesorero', 'Auxiliar Contador')
ON CONFLICT DO NOTHING;

-- Comprobación
SELECT m.name AS menu, string_agg(r.name, ', ' ORDER BY r.name) AS roles
FROM menu m
JOIN menu_role mr ON mr.menu_id = m.menu_id
JOIN role r ON r.role_id = mr.role_id
WHERE m.name IN ('Correcciones de Entradas', 'Correcciones de Salidas')
GROUP BY m.name ORDER BY m.name;
