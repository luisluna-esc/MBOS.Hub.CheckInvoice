-- =====================================================================
-- Permission seed. Simplified (2026-08-25) to just two grantable codes:
--   - "visita" : read-only, satisfies any [Authorize(Policy="x.view")].
--   - "trabajo": full access, satisfies every policy (see
--     CheckInvoice.Infrastructure/Authorization/PermissionAuthorizationHandler.cs).
-- Controllers still declare granular policy names (e.g. "receipt.view",
-- "client.manage") but only these two codes are ever assigned to roles.
-- =====================================================================

INSERT INTO permission (code, name, description, module) VALUES
('visita', 'Visita (solo ver)', 'Puede ver todo el sistema, pero no crear, editar ni eliminar nada.', 'General'),
('trabajo', 'Trabajo (acceso completo)', 'Acceso completo: puede ver, crear, editar y eliminar según su rol.', 'General')
ON CONFLICT (code) DO NOTHING;

-- Grant full access to the M-BOS role so the existing superuser keeps working.
INSERT INTO role_permission (role_id, permission_id, created_at)
SELECT r.role_id, p.permission_id, NOW()
FROM role r
CROSS JOIN permission p
WHERE r.name = 'M-BOS' AND p.code = 'trabajo'
ON CONFLICT (role_id, permission_id) DO NOTHING;
