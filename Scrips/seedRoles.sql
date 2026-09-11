-- =====================================================================
-- Seed for M-BOS (superuser) plus the 4 real business roles and their
-- permission grants. M-BOS was historically created by hand and never
-- captured in any seed script — every environment set up before this fix
-- had it created ad hoc, so a genuinely fresh database was missing it
-- entirely (found deploying to a new Render Postgres with zero manual
-- setup). Requires seedPermissions.sql to have run first (needs the
-- "trabajo"/"visita" permission rows to exist for the grants below).
--
-- The permission catalog was simplified (2026-08-25) to just two grantable
-- codes: "visita" (read-only, satisfies any policy ending in ".view") and
-- "trabajo" (full access, satisfies every policy) — see seedPermissions.sql
-- and PermissionAuthorizationHandler.cs. Roles are granted one of these two
-- codes below; there is no more granular per-module permission grant.
-- What each role can actually reach beyond that is controlled by menu
-- visibility (menu_role) and, for a few specific actions like approving an
-- Issue void request, by role-based [Authorize(Roles = "...")] checks on
-- the endpoint itself — not by this permission catalog.
-- =====================================================================

INSERT INTO role (name, description, is_active) VALUES
('M-BOS', 'Superuser role', TRUE),
('Tesorero', 'Reportes y autorización de edición/eliminación de entradas y salidas', TRUE),
('Contador', 'Reportes, registro de entradas y salidas, y autorización de edición/eliminación', TRUE),
('Auxiliar Contador', 'Registro de entradas, salidas, transferencias, clientes, proveedores, productos y geografía; solicita ediciones/eliminaciones a Contador y Tesorero', TRUE),
('Pastor', 'Autoservicio en el portal, acceso limitado a sus propios registros', TRUE)
ON CONFLICT DO NOTHING;

-- M-BOS: acceso completo, es el superusuario.
INSERT INTO role_permission (role_id, permission_id, created_at)
SELECT r.role_id, p.permission_id, NOW()
FROM role r
CROSS JOIN permission p
WHERE r.name = 'M-BOS' AND p.code = 'trabajo'
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- Tesorero: solo lectura (reportes, ver entradas/salidas).
INSERT INTO role_permission (role_id, permission_id, created_at)
SELECT r.role_id, p.permission_id, NOW()
FROM role r
CROSS JOIN permission p
WHERE r.name = 'Tesorero' AND p.code = 'visita'
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- Contador y Auxiliar Contador: acceso completo (registran y crean entradas/salidas).
INSERT INTO role_permission (role_id, permission_id, created_at)
SELECT r.role_id, p.permission_id, NOW()
FROM role r
CROSS JOIN permission p
WHERE r.name IN ('Contador', 'Auxiliar Contador') AND p.code = 'trabajo'
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- Pastor: autoservicio de solo lectura en el portal (sus propias cuentas por
-- cobrar, cuotas, pagos, depósitos).
INSERT INTO role_permission (role_id, permission_id, created_at)
SELECT r.role_id, p.permission_id, NOW()
FROM role r
CROSS JOIN permission p
WHERE r.name = 'Pastor' AND p.code = 'visita'
ON CONFLICT (role_id, permission_id) DO NOTHING;
