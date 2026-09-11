-- =====================================================================
-- INVENTORY SYSTEM - CONSOLIDATED POSTGRESQL SCRIPT
-- Creation order respects Foreign Key dependencies
-- =====================================================================

-- =====================================================================
-- LEVEL 1: TABLES WITHOUT DEPENDENCIES (base catalogs)
-- =====================================================================

CREATE TABLE configuration (
    configuration_id BIGSERIAL PRIMARY KEY,
    key VARCHAR(100) NOT NULL,
    value VARCHAR(255) NOT NULL,
    description VARCHAR(255),
    CONSTRAINT uq_configuration_key UNIQUE (key)
);

CREATE TABLE country (
    country_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    CONSTRAINT uq_country_name UNIQUE (name)
);

CREATE TABLE document_type (
    document_type_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    CONSTRAINT uq_document_type_name UNIQUE (name)
);

CREATE TABLE special_case (
    special_case_id BIGSERIAL PRIMARY KEY,
    code VARCHAR(10) NOT NULL,
    name VARCHAR(100) NOT NULL,
    CONSTRAINT uq_special_case_code UNIQUE (code)
);

CREATE TABLE void_reason (
    void_reason_id BIGSERIAL PRIMARY KEY,
    code VARCHAR(20) NOT NULL,
    name VARCHAR(100) NOT NULL,
    CONSTRAINT uq_void_reason_code UNIQUE (code)
);

CREATE TABLE church_type (
    church_type_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    CONSTRAINT uq_church_type_name UNIQUE (name)
);

CREATE TABLE issue_type (
    issue_type_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_issue_type_name UNIQUE (name)
);

CREATE TABLE receipt_type (
    receipt_type_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_receipt_type_name UNIQUE (name)
);

CREATE TABLE print_type (
    print_type_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    CONSTRAINT uq_print_type_name UNIQUE (name)
);

CREATE TABLE media_type (
    media_type_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE department (
    department_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE sub_department (
    sub_department_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE mission (
    mission_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_mission_name UNIQUE (name)
);

CREATE TABLE province (
    province_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_province_name UNIQUE (name)
);

CREATE TABLE warehouse (
    warehouse_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    address VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE permission (
    permission_id BIGSERIAL PRIMARY KEY,
    code VARCHAR(100) NOT NULL,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(255),
    module VARCHAR(50) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_permission_code UNIQUE (code)
);

CREATE TABLE shipment (
    shipment_id BIGSERIAL PRIMARY KEY,
    shipment_number INT NOT NULL,
    shipment_date DATE NOT NULL,
    description VARCHAR(255)
);

-- =====================================================================
-- LEVEL 2: APP_USER (self-referencing)
-- =====================================================================

CREATE TABLE app_user (
    app_user_id BIGSERIAL PRIMARY KEY,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    email VARCHAR(150) NOT NULL,
    username VARCHAR(50) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    last_login TIMESTAMP,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by BIGINT,
    updated_at TIMESTAMP,
    updated_by BIGINT,
    CONSTRAINT uq_app_user_email UNIQUE (email),
    CONSTRAINT uq_app_user_username UNIQUE (username),
    CONSTRAINT fk_app_user_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_app_user_updated_by FOREIGN KEY (updated_by) REFERENCES app_user(app_user_id)
);

-- =====================================================================
-- LEVEL 3: depend on app_user
-- =====================================================================

CREATE TABLE warehouse_period (
    warehouse_period_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(20) NOT NULL,
    is_closed BOOLEAN NOT NULL DEFAULT FALSE,
    closed_at TIMESTAMP,
    closed_by BIGINT REFERENCES app_user(app_user_id)
);

CREATE TABLE role (
    role_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    description VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by BIGINT,
    updated_at TIMESTAMP,
    updated_by BIGINT,
    CONSTRAINT uq_role_name UNIQUE (name),
    CONSTRAINT fk_role_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_role_updated_by FOREIGN KEY (updated_by) REFERENCES app_user(app_user_id)
);

CREATE TABLE refresh_token (
    refresh_token_id BIGSERIAL PRIMARY KEY,
    app_user_id BIGINT NOT NULL,
    token VARCHAR(500) NOT NULL,
    expires_at TIMESTAMP NOT NULL,
    is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_ip VARCHAR(50),
    CONSTRAINT fk_refresh_token_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id) ON DELETE CASCADE,
    CONSTRAINT uq_refresh_token_token UNIQUE (token)
);

CREATE TABLE district (
    district_id BIGSERIAL PRIMARY KEY,
    code VARCHAR(20),
    district_name VARCHAR(100) NOT NULL,
    mission_id BIGINT,
    province_id BIGINT,
    app_user_id BIGINT,
    registration_date TIMESTAMP NOT NULL DEFAULT NOW(),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_district_mission FOREIGN KEY (mission_id) REFERENCES mission(mission_id),
    CONSTRAINT fk_district_province FOREIGN KEY (province_id) REFERENCES province(province_id),
    CONSTRAINT fk_district_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id)
);

CREATE TABLE audit_log (
    audit_log_id BIGSERIAL PRIMARY KEY,
    app_user_id BIGINT,
    table_name VARCHAR(100) NOT NULL,
    record_id BIGINT,
    log_date TIMESTAMP NOT NULL DEFAULT NOW(),
    action VARCHAR(20) NOT NULL,
    CONSTRAINT fk_audit_log_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_audit_log_app_user ON audit_log(app_user_id);
CREATE INDEX idx_audit_log_table_name ON audit_log(table_name);

-- =====================================================================
-- LEVEL 4: app_user-role-permission relations, menu, church
-- =====================================================================

CREATE TABLE app_user_role (
    app_user_role_id BIGSERIAL PRIMARY KEY,
    app_user_id BIGINT NOT NULL,
    role_id BIGINT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by BIGINT,
    CONSTRAINT fk_app_user_role_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id) ON DELETE CASCADE,
    CONSTRAINT fk_app_user_role_role FOREIGN KEY (role_id) REFERENCES role(role_id) ON DELETE CASCADE,
    CONSTRAINT fk_app_user_role_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id),
    CONSTRAINT uq_app_user_role UNIQUE (app_user_id, role_id)
);

CREATE INDEX idx_app_user_role_app_user ON app_user_role(app_user_id);
CREATE INDEX idx_app_user_role_role ON app_user_role(role_id);

CREATE TABLE role_permission (
    role_permission_id BIGSERIAL PRIMARY KEY,
    role_id BIGINT NOT NULL,
    permission_id BIGINT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by BIGINT,
    CONSTRAINT fk_role_permission_role FOREIGN KEY (role_id) REFERENCES role(role_id) ON DELETE CASCADE,
    CONSTRAINT fk_role_permission_permission FOREIGN KEY (permission_id) REFERENCES permission(permission_id) ON DELETE CASCADE,
    CONSTRAINT fk_role_permission_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id),
    CONSTRAINT uq_role_permission UNIQUE (role_id, permission_id)
);

CREATE INDEX idx_role_permission_role ON role_permission(role_id);
CREATE INDEX idx_role_permission_permission ON role_permission(permission_id);

CREATE TABLE menu (
    menu_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    translation_key VARCHAR(100),
    route VARCHAR(150),
    icon VARCHAR(50),
    parent_menu_id BIGINT,
    display_order INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    permission_id BIGINT,
    CONSTRAINT fk_menu_parent FOREIGN KEY (parent_menu_id) REFERENCES menu(menu_id) ON DELETE CASCADE,
    CONSTRAINT fk_menu_permission FOREIGN KEY (permission_id) REFERENCES permission(permission_id),
    CONSTRAINT uq_menu_name UNIQUE (name)
);

CREATE INDEX idx_menu_parent ON menu(parent_menu_id);
CREATE INDEX idx_menu_permission ON menu(permission_id);

CREATE TABLE menu_role (
    menu_role_id BIGSERIAL PRIMARY KEY,
    menu_id BIGINT NOT NULL,
    role_id BIGINT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_menu_role_menu FOREIGN KEY (menu_id) REFERENCES menu(menu_id) ON DELETE CASCADE,
    CONSTRAINT fk_menu_role_role FOREIGN KEY (role_id) REFERENCES role(role_id) ON DELETE CASCADE,
    CONSTRAINT uq_menu_role UNIQUE (menu_id, role_id)
);

CREATE INDEX idx_menu_role_menu ON menu_role(menu_id);
CREATE INDEX idx_menu_role_role ON menu_role(role_id);

CREATE TABLE church (
    church_id BIGSERIAL PRIMARY KEY,
    code VARCHAR(20),
    church_name VARCHAR(150) NOT NULL,
    district_id BIGINT,
    church_type_id BIGINT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_church_district FOREIGN KEY (district_id) REFERENCES district(district_id),
    CONSTRAINT fk_church_church_type FOREIGN KEY (church_type_id) REFERENCES church_type(church_type_id)
);

CREATE INDEX idx_church_district ON church(district_id);

-- =====================================================================
-- LEVEL 5: client, supplier, product
-- =====================================================================

-- Identidad compartida entre Cliente y Proveedor: cuando la misma persona/empresa
-- es ambas cosas, su fila de client y su fila de supplier apuntan a la misma party.
CREATE TABLE party (
    party_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(150) NOT NULL,
    tax_id VARCHAR(20),
    email VARCHAR(150),
    mobile_phone VARCHAR(20)
);

CREATE UNIQUE INDEX uq_party_tax_id ON party (tax_id) WHERE tax_id IS NOT NULL AND tax_id <> '';
CREATE UNIQUE INDEX uq_party_email ON party (lower(email)) WHERE email IS NOT NULL AND email <> '';

CREATE TABLE client (
    client_id BIGSERIAL PRIMARY KEY,
    party_id BIGINT NOT NULL,
    app_user_id BIGINT,
    document_type_id BIGINT,
    district_id BIGINT,
    church_id BIGINT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    complement VARCHAR(5),
    special_case_id BIGINT,
    is_pastor BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT fk_client_party FOREIGN KEY (party_id) REFERENCES party(party_id),
    CONSTRAINT fk_client_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_client_document_type FOREIGN KEY (document_type_id) REFERENCES document_type(document_type_id),
    CONSTRAINT fk_client_district FOREIGN KEY (district_id) REFERENCES district(district_id),
    CONSTRAINT fk_client_church FOREIGN KEY (church_id) REFERENCES church(church_id),
    CONSTRAINT fk_client_special_case FOREIGN KEY (special_case_id) REFERENCES special_case(special_case_id),
    CONSTRAINT uq_client_party UNIQUE (party_id),
    CONSTRAINT uq_client_app_user UNIQUE (app_user_id)
);

CREATE INDEX idx_client_district ON client(district_id);
CREATE INDEX idx_client_church ON client(church_id);

CREATE TABLE supplier (
    supplier_id BIGSERIAL PRIMARY KEY,
    party_id BIGINT NOT NULL,
    code VARCHAR(20),
    legal_name VARCHAR(150),
    country_id BIGINT,
    address VARCHAR(255),
    phone VARCHAR(20),
    notes VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_supplier_party FOREIGN KEY (party_id) REFERENCES party(party_id),
    CONSTRAINT fk_supplier_country FOREIGN KEY (country_id) REFERENCES country(country_id),
    CONSTRAINT uq_supplier_party UNIQUE (party_id)
);

CREATE TABLE product (
    product_id BIGSERIAL PRIMARY KEY,
    code VARCHAR(20),
    name VARCHAR(200) NOT NULL,
    department_id BIGINT,
    sub_department_id BIGINT,
    media_type_id BIGINT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_product_department FOREIGN KEY (department_id) REFERENCES department(department_id),
    CONSTRAINT fk_product_sub_department FOREIGN KEY (sub_department_id) REFERENCES sub_department(sub_department_id),
    CONSTRAINT fk_product_media_type FOREIGN KEY (media_type_id) REFERENCES media_type(media_type_id)
);

CREATE INDEX idx_product_department ON product(department_id);
CREATE INDEX idx_product_sub_department ON product(sub_department_id);
CREATE INDEX idx_product_code ON product(code);

-- =====================================================================
-- LEVEL 6: transactional headers and mid-level relations
-- =====================================================================

CREATE TABLE client_district_history (
    client_district_history_id BIGSERIAL PRIMARY KEY,
    client_id BIGINT NOT NULL,
    district_id BIGINT NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE,
    CONSTRAINT fk_cdh_client FOREIGN KEY (client_id) REFERENCES client(client_id),
    CONSTRAINT fk_cdh_district FOREIGN KEY (district_id) REFERENCES district(district_id)
);

CREATE INDEX idx_cdh_client ON client_district_history(client_id);
CREATE INDEX idx_cdh_district ON client_district_history(district_id);

CREATE TABLE receipt (
    receipt_id BIGSERIAL PRIMARY KEY,
    supplier_id BIGINT,
    tax_id VARCHAR(20),
    warehouse_id BIGINT,
    warehouse_period_id BIGINT,
    receipt_type_id BIGINT,
    invoice_number VARCHAR(50),
    description VARCHAR(255),
    issue_date TIMESTAMP NOT NULL DEFAULT NOW(),
    invoice_total NUMERIC(12,2),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by BIGINT,
    is_voided BOOLEAN NOT NULL DEFAULT FALSE,
    related_issue_id BIGINT, -- Devolución: Entrada que referencia la Salida de origen. FK agregada más abajo, después de CREATE TABLE issue (issue aún no existe en este punto del script).
    CONSTRAINT fk_receipt_supplier FOREIGN KEY (supplier_id) REFERENCES supplier(supplier_id),
    CONSTRAINT fk_receipt_warehouse FOREIGN KEY (warehouse_id) REFERENCES warehouse(warehouse_id),
    CONSTRAINT fk_receipt_warehouse_period FOREIGN KEY (warehouse_period_id) REFERENCES warehouse_period(warehouse_period_id),
    CONSTRAINT fk_receipt_receipt_type FOREIGN KEY (receipt_type_id) REFERENCES receipt_type(receipt_type_id),
    CONSTRAINT fk_receipt_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_receipt_warehouse ON receipt(warehouse_id);
CREATE INDEX idx_receipt_issue_date ON receipt(issue_date);
CREATE INDEX idx_receipt_supplier ON receipt(supplier_id);

CREATE TABLE issue (
    issue_id BIGSERIAL PRIMARY KEY,
    issue_type_id BIGINT,
    warehouse_id BIGINT,
    warehouse_period_id BIGINT,
    client_id BIGINT,
    complement VARCHAR(10),
    issue_date TIMESTAMP NOT NULL DEFAULT NOW(),
    print_type_id BIGINT,
    description VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by BIGINT,
    is_voided BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT fk_issue_issue_type FOREIGN KEY (issue_type_id) REFERENCES issue_type(issue_type_id),
    CONSTRAINT fk_issue_warehouse FOREIGN KEY (warehouse_id) REFERENCES warehouse(warehouse_id),
    CONSTRAINT fk_issue_warehouse_period FOREIGN KEY (warehouse_period_id) REFERENCES warehouse_period(warehouse_period_id),
    CONSTRAINT fk_issue_client FOREIGN KEY (client_id) REFERENCES client(client_id),
    CONSTRAINT fk_issue_print_type FOREIGN KEY (print_type_id) REFERENCES print_type(print_type_id),
    CONSTRAINT fk_issue_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_issue_warehouse ON issue(warehouse_id);
CREATE INDEX idx_issue_issue_date ON issue(issue_date);
CREATE INDEX idx_issue_client ON issue(client_id);

-- Devolución: la Entrada que registra el retorno referencia la Salida de origen.
ALTER TABLE receipt ADD CONSTRAINT fk_receipt_related_issue FOREIGN KEY (related_issue_id) REFERENCES issue(issue_id);
CREATE INDEX idx_receipt_related_issue ON receipt(related_issue_id);

CREATE TABLE transfer (
    transfer_id BIGSERIAL PRIMARY KEY,
    source_warehouse_id BIGINT,
    destination_warehouse_id BIGINT,
    sender_user_id BIGINT,
    receiver_user_id BIGINT,
    receiver_client_id BIGINT,
    transfer_date TIMESTAMP NOT NULL DEFAULT NOW(),
    notes TEXT,
    is_approved BOOLEAN NOT NULL DEFAULT TRUE,
    approved_by_id BIGINT,
    approved_at TIMESTAMP,
    CONSTRAINT fk_transfer_source_warehouse FOREIGN KEY (source_warehouse_id) REFERENCES warehouse(warehouse_id),
    CONSTRAINT fk_transfer_destination_warehouse FOREIGN KEY (destination_warehouse_id) REFERENCES warehouse(warehouse_id),
    CONSTRAINT fk_transfer_sender_user FOREIGN KEY (sender_user_id) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_transfer_receiver_user FOREIGN KEY (receiver_user_id) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_transfer_receiver_client FOREIGN KEY (receiver_client_id) REFERENCES client(client_id),
    CONSTRAINT fk_transfer_approved_by FOREIGN KEY (approved_by_id) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_transfer_date ON transfer(transfer_date);

CREATE TABLE stock (
    stock_id BIGSERIAL PRIMARY KEY,
    warehouse_id BIGINT NOT NULL,
    product_id BIGINT NOT NULL,
    quantity NUMERIC(12,2) NOT NULL DEFAULT 0,
    average_cost NUMERIC(12,4) NOT NULL DEFAULT 0,
    CONSTRAINT fk_stock_warehouse FOREIGN KEY (warehouse_id) REFERENCES warehouse(warehouse_id),
    CONSTRAINT fk_stock_product FOREIGN KEY (product_id) REFERENCES product(product_id),
    CONSTRAINT uq_stock_warehouse_product UNIQUE (warehouse_id, product_id)
);

CREATE INDEX idx_stock_product ON stock(product_id);

CREATE TABLE discount (
    discount_id BIGSERIAL PRIMARY KEY,
    client_id BIGINT,
    source_table VARCHAR(50) NOT NULL,
    source_id BIGINT NOT NULL,
    amount NUMERIC(12,2) NOT NULL,
    description VARCHAR(255),
    discount_date DATE NOT NULL DEFAULT CURRENT_DATE,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    CONSTRAINT fk_discount_client FOREIGN KEY (client_id) REFERENCES client(client_id)
);

CREATE INDEX idx_discount_client ON discount(client_id);
CREATE INDEX idx_discount_status ON discount(status);

CREATE TABLE inventory_count (
    inventory_count_id BIGSERIAL PRIMARY KEY,
    warehouse_id BIGINT NOT NULL,
    source_count_id BIGINT,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    count_date TIMESTAMP NOT NULL DEFAULT NOW(),
    status VARCHAR(20) NOT NULL DEFAULT 'closed',
    created_by BIGINT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    closed_by BIGINT,
    closed_at TIMESTAMP,
    CONSTRAINT fk_inventory_count_warehouse FOREIGN KEY (warehouse_id) REFERENCES warehouse(warehouse_id),
    CONSTRAINT fk_inventory_count_source FOREIGN KEY (source_count_id) REFERENCES inventory_count(inventory_count_id),
    CONSTRAINT fk_inventory_count_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_inventory_count_closed_by FOREIGN KEY (closed_by) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_inventory_count_warehouse ON inventory_count(warehouse_id);

-- =====================================================================
-- LEVEL 7: details (depend on level 6 headers)
-- =====================================================================

CREATE TABLE receipt_detail (
    receipt_detail_id BIGSERIAL PRIMARY KEY,
    receipt_id BIGINT NOT NULL,
    product_id BIGINT NOT NULL,
    quantity NUMERIC(12,2) NOT NULL,
    unit_cost NUMERIC(12,2) NOT NULL,
    total_cost NUMERIC(12,2) NOT NULL,
    work_order VARCHAR(50),
    detail VARCHAR(255),
    CONSTRAINT fk_receipt_detail_receipt FOREIGN KEY (receipt_id) REFERENCES receipt(receipt_id) ON DELETE CASCADE,
    CONSTRAINT fk_receipt_detail_product FOREIGN KEY (product_id) REFERENCES product(product_id)
);

CREATE INDEX idx_receipt_detail_receipt ON receipt_detail(receipt_id);
CREATE INDEX idx_receipt_detail_product ON receipt_detail(product_id);

CREATE TABLE issue_detail (
    issue_detail_id BIGSERIAL PRIMARY KEY,
    issue_id BIGINT NOT NULL,
    product_id BIGINT NOT NULL,
    quantity NUMERIC(12,2) NOT NULL,
    unit_cost NUMERIC(12,2) NOT NULL,
    total_cost NUMERIC(12,2) NOT NULL,
    CONSTRAINT fk_issue_detail_issue FOREIGN KEY (issue_id) REFERENCES issue(issue_id) ON DELETE CASCADE,
    CONSTRAINT fk_issue_detail_product FOREIGN KEY (product_id) REFERENCES product(product_id)
);

CREATE INDEX idx_issue_detail_issue ON issue_detail(issue_id);
CREATE INDEX idx_issue_detail_product ON issue_detail(product_id);

CREATE TABLE transfer_detail (
    transfer_detail_id BIGSERIAL PRIMARY KEY,
    transfer_id BIGINT NOT NULL,
    product_id BIGINT NOT NULL,
    quantity NUMERIC(12,2) NOT NULL,
    unit_price NUMERIC(12,2),
    total_sale_price NUMERIC(12,2),
    CONSTRAINT fk_transfer_detail_transfer FOREIGN KEY (transfer_id) REFERENCES transfer(transfer_id) ON DELETE CASCADE,
    CONSTRAINT fk_transfer_detail_product FOREIGN KEY (product_id) REFERENCES product(product_id)
);

CREATE INDEX idx_transfer_detail_transfer ON transfer_detail(transfer_id);

CREATE TABLE account_receivable (
    account_receivable_id BIGSERIAL PRIMARY KEY,
    issue_id BIGINT,
    client_id BIGINT,
    total_amount NUMERIC(12,2) NOT NULL,
    outstanding_balance NUMERIC(12,2) NOT NULL,
    payment_type VARCHAR(20) NOT NULL,
    payment_detail VARCHAR(255),
    due_date DATE,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by BIGINT,
    CONSTRAINT fk_account_receivable_issue FOREIGN KEY (issue_id) REFERENCES issue(issue_id),
    CONSTRAINT fk_account_receivable_client FOREIGN KEY (client_id) REFERENCES client(client_id),
    CONSTRAINT fk_account_receivable_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_account_receivable_client ON account_receivable(client_id);
CREATE INDEX idx_account_receivable_status ON account_receivable(status);

CREATE TABLE inventory_count_detail (
    inventory_count_detail_id BIGSERIAL PRIMARY KEY,
    inventory_count_id BIGINT NOT NULL,
    product_id BIGINT NOT NULL,
    system_quantity NUMERIC(12,2) NOT NULL,
    unit_value NUMERIC(12,4),
    physical_quantity NUMERIC(12,2),
    difference NUMERIC(12,2),
    notes VARCHAR(255),
    CONSTRAINT fk_inventory_count_detail_header FOREIGN KEY (inventory_count_id) REFERENCES inventory_count(inventory_count_id) ON DELETE CASCADE,
    CONSTRAINT fk_inventory_count_detail_product FOREIGN KEY (product_id) REFERENCES product(product_id)
);

CREATE INDEX idx_inventory_count_detail_header ON inventory_count_detail(inventory_count_id);

-- =====================================================================
-- LEVEL 8: installment (depends on account_receivable)
-- =====================================================================

CREATE TABLE installment (
    installment_id BIGSERIAL PRIMARY KEY,
    account_receivable_id BIGINT NOT NULL,
    installment_number INT NOT NULL,
    installment_amount NUMERIC(12,2) NOT NULL,
    due_date DATE NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    CONSTRAINT fk_installment_account_receivable FOREIGN KEY (account_receivable_id) REFERENCES account_receivable(account_receivable_id) ON DELETE CASCADE
);

CREATE INDEX idx_installment_account_receivable ON installment(account_receivable_id);

-- =====================================================================
-- LEVEL 9: payment (depends on account_receivable and installment)
-- =====================================================================

CREATE TABLE payment (
    payment_id BIGSERIAL PRIMARY KEY,
    account_receivable_id BIGINT NOT NULL,
    installment_id BIGINT,
    amount NUMERIC(12,2) NOT NULL,
    payment_date TIMESTAMP NOT NULL DEFAULT NOW(),
    payment_method VARCHAR(30),
    notes VARCHAR(255),
    created_by BIGINT,
    CONSTRAINT fk_payment_account_receivable FOREIGN KEY (account_receivable_id) REFERENCES account_receivable(account_receivable_id),
    CONSTRAINT fk_payment_installment FOREIGN KEY (installment_id) REFERENCES installment(installment_id),
    CONSTRAINT fk_payment_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_payment_account_receivable ON payment(account_receivable_id);

-- =====================================================================
-- LEVEL 10: change_request (edit/delete approval workflow for
-- immutable movement headers: receipt, issue, transfer)
-- =====================================================================

CREATE TABLE change_request (
    change_request_id BIGSERIAL PRIMARY KEY,
    table_name VARCHAR(50) NOT NULL,
    record_id BIGINT NOT NULL,
    action VARCHAR(20) NOT NULL,
    current_data JSONB,
    proposed_data JSONB,
    reason VARCHAR(255) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    requested_by BIGINT NOT NULL,
    requested_at TIMESTAMP NOT NULL DEFAULT NOW(),
    reviewed_by BIGINT,
    reviewed_at TIMESTAMP,
    review_notes VARCHAR(255),
    CONSTRAINT fk_change_request_requested_by FOREIGN KEY (requested_by) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_change_request_reviewed_by FOREIGN KEY (reviewed_by) REFERENCES app_user(app_user_id),
    CONSTRAINT chk_change_request_table CHECK (table_name IN ('receipt', 'issue', 'transfer')),
    CONSTRAINT chk_change_request_action CHECK (action IN ('edit', 'delete'))
);

CREATE INDEX idx_change_request_status ON change_request(status);
CREATE INDEX idx_change_request_table_record ON change_request(table_name, record_id);

-- =====================================================================
-- LEVEL 10b: issue_void_request (anulación de Salidas: Auxiliar Contador
-- solicita, Contador o M-BOS aprueba/rechaza; solo una solicitud
-- pendiente por Salida a la vez)
-- =====================================================================

CREATE TABLE issue_void_request (
    issue_void_request_id BIGSERIAL PRIMARY KEY,
    issue_id BIGINT NOT NULL,
    void_reason_id BIGINT NOT NULL,
    detail VARCHAR(255),
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    requested_by BIGINT NOT NULL,
    requested_at TIMESTAMP NOT NULL DEFAULT NOW(),
    reviewed_by BIGINT,
    reviewed_at TIMESTAMP,
    review_notes VARCHAR(255),
    CONSTRAINT fk_issue_void_request_issue FOREIGN KEY (issue_id) REFERENCES issue(issue_id),
    CONSTRAINT fk_issue_void_request_reason FOREIGN KEY (void_reason_id) REFERENCES void_reason(void_reason_id),
    CONSTRAINT fk_issue_void_request_requested_by FOREIGN KEY (requested_by) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_issue_void_request_reviewed_by FOREIGN KEY (reviewed_by) REFERENCES app_user(app_user_id),
    CONSTRAINT chk_issue_void_request_status CHECK (status IN ('pending', 'approved', 'rejected'))
);

CREATE INDEX idx_issue_void_request_issue ON issue_void_request(issue_id);
CREATE UNIQUE INDEX uq_issue_void_request_pending ON issue_void_request(issue_id) WHERE status = 'pending';

CREATE TABLE receipt_void_request (
    receipt_void_request_id BIGSERIAL PRIMARY KEY,
    receipt_id BIGINT NOT NULL,
    void_reason_id BIGINT NOT NULL,
    detail VARCHAR(255),
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    requested_by BIGINT NOT NULL,
    requested_at TIMESTAMP NOT NULL DEFAULT NOW(),
    reviewed_by BIGINT,
    reviewed_at TIMESTAMP,
    review_notes VARCHAR(255),
    CONSTRAINT fk_receipt_void_request_receipt FOREIGN KEY (receipt_id) REFERENCES receipt(receipt_id),
    CONSTRAINT fk_receipt_void_request_reason FOREIGN KEY (void_reason_id) REFERENCES void_reason(void_reason_id),
    CONSTRAINT fk_receipt_void_request_requested_by FOREIGN KEY (requested_by) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_receipt_void_request_reviewed_by FOREIGN KEY (reviewed_by) REFERENCES app_user(app_user_id),
    CONSTRAINT chk_receipt_void_request_status CHECK (status IN ('pending', 'approved', 'rejected'))
);

CREATE INDEX idx_receipt_void_request_receipt ON receipt_void_request(receipt_id);
CREATE UNIQUE INDEX uq_receipt_void_request_pending ON receipt_void_request(receipt_id) WHERE status = 'pending';

-- =====================================================================
-- SEED DATA
-- =====================================================================

INSERT INTO configuration (key, value, description)
VALUES ('discount_accounting_account', '1135005', 'Accounting account code for missionary book discounts');

INSERT INTO void_reason (code, name) VALUES
('DATE_ERROR', 'Fallo de fecha'),
('NAME_ERROR', 'Error de nombre'),
('OTHER', 'Otros');

-- =====================================================================
-- END OF SCRIPT
-- =====================================================================