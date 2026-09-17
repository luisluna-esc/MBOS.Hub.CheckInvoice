-- =====================================================================
-- Permission seed. Simplified (2026-08-25) to just two grantable codes:
--   - "visita" : read-only, satisfies any [Authorize(Policy="x.view")].
--   - "trabajo": full access, satisfies every policy (see
--     CheckInvoice.Infrastructure/Authorization/PermissionAuthorizationHandler.cs).
-- Controllers still declare granular policy names (e.g. "receipt.view",
-- "client.manage") but only these two codes are ever assigned to roles.
-- =====================================================================

-- Solo el catálogo de códigos, sin ningún grant aquí: los grants a roles (incluido M-BOS)
-- viven en seedRoles.sql, junto con la creación de los roles mismos — así este archivo no
-- depende de que la tabla role ya tenga filas, y seedRoles.sql no depende de nada más que
-- de que este script haya corrido antes que él.
INSERT INTO permission (code, name, description, module) VALUES
('visita', 'Visita (solo ver)', 'Puede ver todo el sistema, pero no crear, editar ni eliminar nada.', 'General'),
('trabajo', 'Trabajo (acceso completo)', 'Acceso completo: puede ver, crear, editar y eliminar según su rol.', 'General')
ON CONFLICT (code) DO NOTHING;
