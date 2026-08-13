-- =====================================================================
-- Permission seed matching the [Authorize(Policy = "resource.action")]
-- codes applied across every controller. Run once after deploying the
-- permission-based authorization system.
-- =====================================================================

INSERT INTO permission (code, name, module) VALUES
-- Security
('role.view', 'View Roles', 'Security'),
('role.manage', 'Manage Roles', 'Security'),
('permission.view', 'View Permissions', 'Security'),
('permission.manage', 'Manage Permissions', 'Security'),
('appuser.view', 'View Users', 'Security'),
('appuser.manage', 'Manage Users', 'Security'),
('menu.view', 'View Menus', 'Security'),
('menu.manage', 'Manage Menus', 'Security'),

-- Catalogs
('mission.view', 'View Missions', 'Catalogs'),
('mission.manage', 'Manage Missions', 'Catalogs'),
('configuration.view', 'View Configuration', 'Catalogs'),
('configuration.manage', 'Manage Configuration', 'Catalogs'),
('country.view', 'View Countries', 'Catalogs'),
('country.manage', 'Manage Countries', 'Catalogs'),
('document_type.view', 'View Document Types', 'Catalogs'),
('document_type.manage', 'Manage Document Types', 'Catalogs'),
('church_type.view', 'View Church Types', 'Catalogs'),
('church_type.manage', 'Manage Church Types', 'Catalogs'),
('issue_type.view', 'View Issue Types', 'Catalogs'),
('issue_type.manage', 'Manage Issue Types', 'Catalogs'),
('receipt_type.view', 'View Receipt Types', 'Catalogs'),
('receipt_type.manage', 'Manage Receipt Types', 'Catalogs'),
('print_type.view', 'View Print Types', 'Catalogs'),
('print_type.manage', 'Manage Print Types', 'Catalogs'),
('media_type.view', 'View Media Types', 'Catalogs'),
('media_type.manage', 'Manage Media Types', 'Catalogs'),
('department.view', 'View Departments', 'Catalogs'),
('department.manage', 'Manage Departments', 'Catalogs'),
('sub_department.view', 'View Sub-Departments', 'Catalogs'),
('sub_department.manage', 'Manage Sub-Departments', 'Catalogs'),
('province.view', 'View Provinces', 'Catalogs'),
('province.manage', 'Manage Provinces', 'Catalogs'),
('warehouse_period.view', 'View Warehouse Periods', 'Catalogs'),
('warehouse_period.manage', 'Manage Warehouse Periods', 'Catalogs'),
('warehouse.view', 'View Warehouses', 'Catalogs'),
('warehouse.manage', 'Manage Warehouses', 'Catalogs'),
('shipment.view', 'View Shipments', 'Catalogs'),
('shipment.manage', 'Manage Shipments', 'Catalogs'),

-- Organization
('district.view', 'View Districts', 'Organization'),
('district.manage', 'Manage Districts', 'Organization'),
('church.view', 'View Churches', 'Organization'),
('church.manage', 'Manage Churches', 'Organization'),

-- Parties
('client.view', 'View Clients', 'Parties'),
('client.manage', 'Manage Clients', 'Parties'),
('supplier.view', 'View Suppliers', 'Parties'),
('supplier.manage', 'Manage Suppliers', 'Parties'),
('client_district_history.view', 'View Client District History', 'Parties'),
('client_district_history.manage', 'Manage Client District History', 'Parties'),

-- Products
('product.view', 'View Products', 'Products'),
('product.manage', 'Manage Products', 'Products'),

-- Warehouses / Audit (read-only)
('stock.view', 'View Stock', 'Warehouses'),
('audit_log.view', 'View Audit Log', 'Audit'),

-- Movements (immutable: view + create)
('receipt.view', 'View Receipts', 'Movements'),
('receipt.create', 'Create Receipts', 'Movements'),
('issue.view', 'View Issues', 'Movements'),
('issue.create', 'Create Issues', 'Movements'),
('transfer.view', 'View Transfers', 'Movements'),
('transfer.create', 'Create Transfers', 'Movements'),
('inventory_count.view', 'View Inventory Counts', 'Movements'),
('inventory_count.create', 'Create Inventory Counts', 'Movements'),
('deposit.view', 'View Deposits', 'Movements'),
('deposit.create', 'Create Deposits', 'Movements'),
('discount.view', 'View Discounts', 'Movements'),
('discount.create', 'Create Discounts', 'Movements'),
('discount.manage', 'Approve/Reject Discounts', 'Movements'),

-- Finance
('account_receivable.view', 'View Accounts Receivable', 'Finance'),
('account_receivable.create', 'Create Accounts Receivable', 'Finance'),
('installment.view', 'View Installments', 'Finance'),
('installment.create', 'Create Installments', 'Finance'),
('payment.view', 'View Payments', 'Finance'),
('payment.create', 'Create Payments', 'Finance'),

-- Portal (pastor self-service, scoped to their own linked Client)
('portal.account_receivable.view', 'Portal: View My Accounts Receivable', 'Portal'),
('portal.installment.view', 'Portal: View My Installments', 'Portal'),
('portal.payment.view', 'Portal: View My Payments', 'Portal'),
('portal.deposit.view', 'Portal: View My Deposits', 'Portal')

ON CONFLICT (code) DO NOTHING;

-- Grant every permission above to the M-BOS role so the existing superuser keeps full access.
INSERT INTO role_permission (role_id, permission_id, created_at)
SELECT r.role_id, p.permission_id, NOW()
FROM role r
CROSS JOIN permission p
WHERE r.name = 'M-BOS'
ON CONFLICT (role_id, permission_id) DO NOTHING;