-- =====================================================================
-- Procedimientos almacenados
-- Piloto de rendimiento: reemplaza los múltiples round-trips que
-- IssueService.GetAllIssues hacía en C# (8 consultas separadas: página,
-- conteo, nombre del creador, cambios pendientes, total por salida,
-- anulaciones pendientes, anulaciones relevantes, motivo de anulación)
-- por una sola consulta con joins.
-- =====================================================================

CREATE OR REPLACE FUNCTION sp_get_issues(
    p_issue_id BIGINT,
    p_client_id BIGINT,
    p_warehouse_id BIGINT,
    p_issue_type_id BIGINT,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    issue_id BIGINT,
    issue_type_id BIGINT,
    warehouse_id BIGINT,
    warehouse_period_id BIGINT,
    client_id BIGINT,
    complement VARCHAR,
    issue_date TIMESTAMP,
    print_type_id BIGINT,
    description VARCHAR,
    created_at TIMESTAMP,
    created_by_id BIGINT,
    created_by_full_name TEXT,
    has_pending_change_request BOOLEAN,
    total NUMERIC,
    is_voided BOOLEAN,
    has_pending_void_request BOOLEAN,
    void_reason_name TEXT,
    void_detail VARCHAR,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        i.issue_id,
        i.issue_type_id,
        i.warehouse_id,
        i.warehouse_period_id,
        i.client_id,
        i.complement,
        i.issue_date,
        i.print_type_id,
        i.description,
        i.created_at,
        i.created_by AS created_by_id,
        (au.first_name || ' ' || au.last_name) AS created_by_full_name,
        EXISTS (
            SELECT 1 FROM change_request cr
            WHERE cr.table_name = 'issue' AND cr.status = 'pending' AND cr.record_id = i.issue_id
        ) AS has_pending_change_request,
        COALESCE(det.total, 0) AS total,
        i.is_voided,
        EXISTS (
            SELECT 1 FROM issue_void_request vr
            WHERE vr.status = 'pending' AND vr.issue_id = i.issue_id
        ) AS has_pending_void_request,
        vctx.void_reason_name,
        vctx.detail AS void_detail,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM issue i
    LEFT JOIN app_user au ON au.app_user_id = i.created_by
    LEFT JOIN LATERAL (
        SELECT SUM(d.total_cost) AS total
        FROM issue_detail d
        WHERE d.issue_id = i.issue_id
    ) det ON true
    LEFT JOIN LATERAL (
        -- Más reciente de las anulaciones pendientes/aprobadas de esta salida.
        -- (El código original en C# asumía que solo existe una relevante a la vez.)
        SELECT vr2.detail, vru.name AS void_reason_name
        FROM issue_void_request vr2
        JOIN void_reason vru ON vru.void_reason_id = vr2.void_reason_id
        WHERE vr2.issue_id = i.issue_id AND vr2.status IN ('pending', 'approved')
        ORDER BY vr2.issue_void_request_id DESC
        LIMIT 1
    ) vctx ON true
    WHERE (p_issue_id IS NULL OR i.issue_id = p_issue_id)
      AND (p_client_id IS NULL OR i.client_id = p_client_id)
      AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
      AND (p_issue_type_id IS NULL OR i.issue_type_id = p_issue_type_id)
    ORDER BY i.issue_date DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- Piloto 2: AccountReceivableService.GetAllAccountReceivables hacía 2 consultas
-- separadas (página + fecha de la salida vinculada). Ahora es una sola con LEFT JOIN.
CREATE OR REPLACE FUNCTION sp_get_account_receivables(
    p_account_receivable_id BIGINT,
    p_client_id BIGINT,
    p_payment_type VARCHAR,
    p_status VARCHAR,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    account_receivable_id BIGINT,
    issue_id BIGINT,
    issue_date TIMESTAMP,
    client_id BIGINT,
    total_amount NUMERIC,
    outstanding_balance NUMERIC,
    payment_type VARCHAR,
    payment_detail VARCHAR,
    due_date DATE,
    status VARCHAR,
    created_at TIMESTAMP,
    created_by_id BIGINT,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        a.account_receivable_id,
        a.issue_id,
        i.issue_date,
        a.client_id,
        a.total_amount,
        a.outstanding_balance,
        a.payment_type,
        a.payment_detail,
        a.due_date,
        a.status,
        a.created_at,
        a.created_by AS created_by_id,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM account_receivable a
    LEFT JOIN issue i ON i.issue_id = a.issue_id
    WHERE (p_account_receivable_id IS NULL OR a.account_receivable_id = p_account_receivable_id)
      AND (p_client_id IS NULL OR a.client_id = p_client_id)
      AND (p_payment_type IS NULL OR a.payment_type = p_payment_type)
      AND (p_status IS NULL OR a.status = p_status)
    ORDER BY a.created_at DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- Reporte "Cuentas por Cobrar" general: a diferencia de sp_get_account_receivables (grilla
-- paginada de la pantalla CRUD), este devuelve la cartera completa sin paginar, con el
-- nombre del cliente ya resuelto (join client -> party) porque el reporte es de todos los
-- clientes a la vez y no tendría sentido resolver cada nombre uno por uno en el backend
-- (como sí hace el Reporte Campo Pastor, que es de un solo cliente). El filtro de fecha
-- aplica sobre due_date (fecha límite), que es el dato relevante para una cartera de cobro.
CREATE OR REPLACE FUNCTION sp_get_account_receivables_report(
    p_client_id BIGINT,
    p_status VARCHAR,
    p_date_from DATE,
    p_date_to DATE
)
RETURNS TABLE (
    account_receivable_id BIGINT,
    issue_id BIGINT,
    issue_date TIMESTAMP,
    client_id BIGINT,
    client_name VARCHAR,
    total_amount NUMERIC,
    outstanding_balance NUMERIC,
    due_date DATE,
    status VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        ar.account_receivable_id,
        ar.issue_id,
        i.issue_date,
        ar.client_id,
        p.name AS client_name,
        ar.total_amount,
        ar.outstanding_balance,
        ar.due_date,
        ar.status
    FROM account_receivable ar
    LEFT JOIN issue i ON i.issue_id = ar.issue_id
    LEFT JOIN client c ON c.client_id = ar.client_id
    LEFT JOIN party p ON p.party_id = c.party_id
    WHERE (p_client_id IS NULL OR ar.client_id = p_client_id)
      AND (p_status IS NULL OR ar.status = p_status)
      AND (p_date_from IS NULL OR ar.due_date >= p_date_from)
      AND (p_date_to IS NULL OR ar.due_date < p_date_to + 1)
    ORDER BY (ar.status = 'paid'), ar.due_date NULLS LAST, ar.account_receivable_id;
$$;

-- Piloto 3: ClientService.GetAllClients hacía 2 consultas separadas (página con join a
-- party + diccionario de proveedores vinculados a la misma identidad). Ahora es una sola
-- con LEFT JOIN. El filtro de texto (p_search) preserva LIKE case-sensitive, igual que el
-- .Contains() de EF Core sobre Npgsql que reemplaza.
CREATE OR REPLACE FUNCTION sp_get_clients(
    p_client_id BIGINT,
    p_app_user_id BIGINT,
    p_tax_id VARCHAR,
    p_district_id BIGINT,
    p_church_id BIGINT,
    p_search VARCHAR,
    p_is_active BOOLEAN,
    p_is_pastor BOOLEAN,
    p_pending_portal_access BOOLEAN,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    client_id BIGINT,
    party_id BIGINT,
    app_user_id BIGINT,
    document_type_id BIGINT,
    tax_id VARCHAR,
    name VARCHAR,
    email VARCHAR,
    mobile_phone VARCHAR,
    district_id BIGINT,
    church_id BIGINT,
    is_active BOOLEAN,
    complement VARCHAR,
    special_case_id BIGINT,
    is_pastor BOOLEAN,
    linked_supplier_id BIGINT,
    linked_supplier_name VARCHAR,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        c.client_id,
        c.party_id,
        c.app_user_id,
        c.document_type_id,
        p.tax_id,
        p.name,
        p.email,
        p.mobile_phone,
        c.district_id,
        c.church_id,
        c.is_active,
        c.complement,
        c.special_case_id,
        c.is_pastor,
        s.supplier_id AS linked_supplier_id,
        sp.name AS linked_supplier_name,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM client c
    JOIN party p ON p.party_id = c.party_id
    LEFT JOIN supplier s ON s.party_id = c.party_id
    LEFT JOIN party sp ON sp.party_id = s.party_id
    WHERE (p_client_id IS NULL OR c.client_id = p_client_id)
      AND (p_app_user_id IS NULL OR c.app_user_id = p_app_user_id)
      AND (p_tax_id IS NULL OR p.tax_id = p_tax_id)
      AND (p_district_id IS NULL OR c.district_id = p_district_id)
      AND (p_church_id IS NULL OR c.church_id = p_church_id)
      AND (p_search IS NULL OR p.name LIKE '%' || p_search || '%')
      AND (p_is_active IS NULL OR c.is_active = p_is_active)
      AND (p_is_pastor IS NULL OR c.is_pastor = p_is_pastor)
      AND (p_pending_portal_access IS NOT TRUE OR (c.is_pastor AND c.app_user_id IS NULL))
    ORDER BY p.name
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- Piloto 4: ReceiptService.GetAllReceipts (Entradas) hacía 3 consultas separadas (página,
-- nombre del creador, cambios pendientes). Ahora es una sola con LEFT JOIN, igual patrón
-- que sp_get_issues (Salidas es prácticamente el mismo flujo del lado de entrada).
DROP FUNCTION IF EXISTS sp_get_receipts(BIGINT, BIGINT, BIGINT, BIGINT, VARCHAR, INT, INT);
CREATE OR REPLACE FUNCTION sp_get_receipts(
    p_receipt_id BIGINT,
    p_supplier_id BIGINT,
    p_warehouse_id BIGINT,
    p_receipt_type_id BIGINT,
    p_invoice_number VARCHAR,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    receipt_id BIGINT,
    supplier_id BIGINT,
    tax_id VARCHAR,
    warehouse_id BIGINT,
    warehouse_period_id BIGINT,
    receipt_type_id BIGINT,
    invoice_number VARCHAR,
    description VARCHAR,
    issue_date TIMESTAMP,
    invoice_total NUMERIC,
    created_at TIMESTAMP,
    created_by_id BIGINT,
    created_by_full_name TEXT,
    has_pending_change_request BOOLEAN,
    is_voided BOOLEAN,
    has_pending_void_request BOOLEAN,
    void_reason_name TEXT,
    void_detail VARCHAR,
    related_issue_id BIGINT,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        r.receipt_id,
        r.supplier_id,
        r.tax_id,
        r.warehouse_id,
        r.warehouse_period_id,
        r.receipt_type_id,
        r.invoice_number,
        r.description,
        r.issue_date,
        r.invoice_total,
        r.created_at,
        r.created_by AS created_by_id,
        (au.first_name || ' ' || au.last_name) AS created_by_full_name,
        EXISTS (
            SELECT 1 FROM change_request cr
            WHERE cr.table_name = 'receipt' AND cr.status = 'pending' AND cr.record_id = r.receipt_id
        ) AS has_pending_change_request,
        r.is_voided,
        EXISTS (
            SELECT 1 FROM receipt_void_request vr
            WHERE vr.status = 'pending' AND vr.receipt_id = r.receipt_id
        ) AS has_pending_void_request,
        vctx.void_reason_name,
        vctx.detail AS void_detail,
        r.related_issue_id,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM receipt r
    LEFT JOIN app_user au ON au.app_user_id = r.created_by
    LEFT JOIN LATERAL (
        -- Más reciente de las anulaciones pendientes/aprobadas de esta entrada.
        SELECT vr2.detail, vru.name AS void_reason_name
        FROM receipt_void_request vr2
        JOIN void_reason vru ON vru.void_reason_id = vr2.void_reason_id
        WHERE vr2.receipt_id = r.receipt_id AND vr2.status IN ('pending', 'approved')
        ORDER BY vr2.receipt_void_request_id DESC
        LIMIT 1
    ) vctx ON true
    WHERE (p_receipt_id IS NULL OR r.receipt_id = p_receipt_id)
      AND (p_supplier_id IS NULL OR r.supplier_id = p_supplier_id)
      AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
      AND (p_receipt_type_id IS NULL OR r.receipt_type_id = p_receipt_type_id)
      AND (p_invoice_number IS NULL OR r.invoice_number = p_invoice_number)
    ORDER BY r.issue_date DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- Piloto 5: IssueVoidRequestService.GetAllVoidRequests (Anulaciones) hacía 9 consultas
-- separadas (página + 7 diccionarios: salidas, clientes vía party, nombres de almacén,
-- totales por salida, motivos de anulación, nombres de usuario solicitante/revisor). Ahora
-- es una sola consulta con joins.
CREATE OR REPLACE FUNCTION sp_get_issue_void_requests(
    p_issue_void_request_id BIGINT,
    p_issue_id BIGINT,
    p_status VARCHAR,
    p_requested_by BIGINT,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    issue_void_request_id BIGINT,
    issue_id BIGINT,
    issue_date TIMESTAMP,
    client_name VARCHAR,
    warehouse_name VARCHAR,
    issue_total NUMERIC,
    void_reason_id BIGINT,
    void_reason_name VARCHAR,
    detail VARCHAR,
    status VARCHAR,
    requested_by BIGINT,
    requested_by_full_name TEXT,
    requested_at TIMESTAMP,
    reviewed_by BIGINT,
    reviewed_by_full_name TEXT,
    reviewed_at TIMESTAMP,
    review_notes VARCHAR,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        vr.issue_void_request_id,
        vr.issue_id,
        COALESCE(i.issue_date, '0001-01-01'::timestamp) AS issue_date,
        cp.name AS client_name,
        w.name AS warehouse_name,
        COALESCE(det.total, 0) AS issue_total,
        vr.void_reason_id,
        COALESCE(vru.name, '') AS void_reason_name,
        vr.detail,
        vr.status,
        vr.requested_by,
        (rqu.first_name || ' ' || rqu.last_name) AS requested_by_full_name,
        vr.requested_at,
        vr.reviewed_by,
        (rvu.first_name || ' ' || rvu.last_name) AS reviewed_by_full_name,
        vr.reviewed_at,
        vr.review_notes,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM issue_void_request vr
    LEFT JOIN issue i ON i.issue_id = vr.issue_id
    LEFT JOIN client c ON c.client_id = i.client_id
    LEFT JOIN party cp ON cp.party_id = c.party_id
    LEFT JOIN warehouse w ON w.warehouse_id = i.warehouse_id
    LEFT JOIN LATERAL (
        SELECT SUM(d.total_cost) AS total
        FROM issue_detail d
        WHERE d.issue_id = i.issue_id
    ) det ON true
    LEFT JOIN void_reason vru ON vru.void_reason_id = vr.void_reason_id
    LEFT JOIN app_user rqu ON rqu.app_user_id = vr.requested_by
    LEFT JOIN app_user rvu ON rvu.app_user_id = vr.reviewed_by
    WHERE (p_issue_void_request_id IS NULL OR vr.issue_void_request_id = p_issue_void_request_id)
      AND (p_issue_id IS NULL OR vr.issue_id = p_issue_id)
      AND (p_status IS NULL OR vr.status = p_status)
      AND (p_requested_by IS NULL OR vr.requested_by = p_requested_by)
    ORDER BY vr.requested_at DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- Mismo patrón que sp_get_issue_void_requests, pero para Entradas: el proveedor viene
-- directo de supplier.legal_name (sin pasar por party) y el total ya está en receipt.invoice_total
-- (no hace falta una suma lateral de receipt_detail como con issue_detail).
CREATE OR REPLACE FUNCTION sp_get_receipt_void_requests(
    p_receipt_void_request_id BIGINT,
    p_receipt_id BIGINT,
    p_status VARCHAR,
    p_requested_by BIGINT,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    receipt_void_request_id BIGINT,
    receipt_id BIGINT,
    receipt_date TIMESTAMP,
    supplier_name VARCHAR,
    warehouse_name VARCHAR,
    receipt_total NUMERIC,
    void_reason_id BIGINT,
    void_reason_name VARCHAR,
    detail VARCHAR,
    status VARCHAR,
    requested_by BIGINT,
    requested_by_full_name TEXT,
    requested_at TIMESTAMP,
    reviewed_by BIGINT,
    reviewed_by_full_name TEXT,
    reviewed_at TIMESTAMP,
    review_notes VARCHAR,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        vr.receipt_void_request_id,
        vr.receipt_id,
        COALESCE(r.issue_date, '0001-01-01'::timestamp) AS receipt_date,
        s.legal_name AS supplier_name,
        w.name AS warehouse_name,
        COALESCE(r.invoice_total, 0) AS receipt_total,
        vr.void_reason_id,
        COALESCE(vru.name, '') AS void_reason_name,
        vr.detail,
        vr.status,
        vr.requested_by,
        (rqu.first_name || ' ' || rqu.last_name) AS requested_by_full_name,
        vr.requested_at,
        vr.reviewed_by,
        (rvu.first_name || ' ' || rvu.last_name) AS reviewed_by_full_name,
        vr.reviewed_at,
        vr.review_notes,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM receipt_void_request vr
    LEFT JOIN receipt r ON r.receipt_id = vr.receipt_id
    LEFT JOIN supplier s ON s.supplier_id = r.supplier_id
    LEFT JOIN warehouse w ON w.warehouse_id = r.warehouse_id
    LEFT JOIN void_reason vru ON vru.void_reason_id = vr.void_reason_id
    LEFT JOIN app_user rqu ON rqu.app_user_id = vr.requested_by
    LEFT JOIN app_user rvu ON rvu.app_user_id = vr.reviewed_by
    WHERE (p_receipt_void_request_id IS NULL OR vr.receipt_void_request_id = p_receipt_void_request_id)
      AND (p_receipt_id IS NULL OR vr.receipt_id = p_receipt_id)
      AND (p_status IS NULL OR vr.status = p_status)
      AND (p_requested_by IS NULL OR vr.requested_by = p_requested_by)
    ORDER BY vr.requested_at DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- Piloto 6: SupplierService.GetAllSuppliers hacía 2 consultas separadas (página con join a
-- party + diccionario de clientes vinculados a la misma identidad). Mismo patrón que
-- sp_get_clients pero en espejo (Proveedor -> Cliente vinculado).
CREATE OR REPLACE FUNCTION sp_get_suppliers(
    p_supplier_id BIGINT,
    p_code VARCHAR,
    p_tax_id VARCHAR,
    p_country_id BIGINT,
    p_search VARCHAR,
    p_is_active BOOLEAN,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    supplier_id BIGINT,
    party_id BIGINT,
    code VARCHAR,
    legal_name VARCHAR,
    name VARCHAR,
    tax_id VARCHAR,
    country_id BIGINT,
    address VARCHAR,
    phone VARCHAR,
    mobile_phone VARCHAR,
    email VARCHAR,
    notes VARCHAR,
    is_active BOOLEAN,
    linked_client_id BIGINT,
    linked_client_name VARCHAR,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        s.supplier_id,
        s.party_id,
        s.code,
        s.legal_name,
        p.name,
        p.tax_id,
        s.country_id,
        s.address,
        s.phone,
        p.mobile_phone,
        p.email,
        s.notes,
        s.is_active,
        c.client_id AS linked_client_id,
        cp.name AS linked_client_name,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM supplier s
    JOIN party p ON p.party_id = s.party_id
    LEFT JOIN client c ON c.party_id = s.party_id
    LEFT JOIN party cp ON cp.party_id = c.party_id
    WHERE (p_supplier_id IS NULL OR s.supplier_id = p_supplier_id)
      AND (p_code IS NULL OR s.code = p_code)
      AND (p_tax_id IS NULL OR p.tax_id = p_tax_id)
      AND (p_country_id IS NULL OR s.country_id = p_country_id)
      AND (
        p_search IS NULL
        OR p.name LIKE '%' || p_search || '%'
        OR s.legal_name LIKE '%' || p_search || '%'
        OR p.tax_id LIKE '%' || p_search || '%'
      )
      AND (p_is_active IS NULL OR s.is_active = p_is_active)
    ORDER BY p.name
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- =====================================================================
-- Reportes de negocio (no son GET de un CRUD existente, son reportes
-- nuevos armados específicamente para exportar/analizar).
-- =====================================================================

-- Reporte "Stock - Almacén": movimiento neto por producto en un período
-- (Entradas - Salidas + Transferencias entrantes - Transferencias salientes),
-- valorizado al costo unitario promedio ponderado de las Entradas del período.
-- Junta 4 fuentes (entrada, salida, transferencia x2 direcciones) agregadas
-- por producto — exactamente el tipo de consulta que no conviene traer cruda
-- a C# para sumarla ahí.
CREATE OR REPLACE FUNCTION sp_get_stock_report(
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE
)
RETURNS TABLE (
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_id BIGINT,
    department_name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    net_quantity NUMERIC,
    unit_value NUMERIC,
    total_value NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH receipts AS (
        SELECT rd.product_id,
               SUM(rd.quantity) AS qty,
               SUM(rd.quantity * rd.unit_cost) AS cost_total
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR r.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR r.issue_date < p_date_to + 1)
        GROUP BY rd.product_id
    ),
    issues AS (
        SELECT idt.product_id,
               SUM(idt.quantity) AS qty
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR i.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR i.issue_date < p_date_to + 1)
        GROUP BY idt.product_id
    ),
    transfers_in AS (
        SELECT td.product_id,
               SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE (p_warehouse_id IS NULL OR t.destination_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    ),
    transfers_out AS (
        SELECT td.product_id,
               SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE (p_warehouse_id IS NULL OR t.source_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    )
    SELECT
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        COALESCE(r.qty, 0) - COALESCE(iss.qty, 0) + COALESCE(ti.qty, 0) - COALESCE(tou.qty, 0) AS net_quantity,
        CASE WHEN COALESCE(r.qty, 0) = 0 THEN 0 ELSE r.cost_total / r.qty END AS unit_value,
        (COALESCE(r.qty, 0) - COALESCE(iss.qty, 0) + COALESCE(ti.qty, 0) - COALESCE(tou.qty, 0))
            * (CASE WHEN COALESCE(r.qty, 0) = 0 THEN 0 ELSE r.cost_total / r.qty END) AS total_value
    FROM product p
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    LEFT JOIN receipts r ON r.product_id = p.product_id
    LEFT JOIN issues iss ON iss.product_id = p.product_id
    LEFT JOIN transfers_in ti ON ti.product_id = p.product_id
    LEFT JOIN transfers_out tou ON tou.product_id = p.product_id
    WHERE r.product_id IS NOT NULL OR iss.product_id IS NOT NULL
       OR ti.product_id IS NOT NULL OR tou.product_id IS NOT NULL
    ORDER BY d.name, sd.name, p.name;
$$;

-- Reporte "Stock - Departamento": misma fórmula que sp_get_stock_report (movimiento
-- neto valorizado al costo promedio ponderado), pero con Categoría (departamento)
-- como filtro en lugar de agrupador — el reporte se centra en Subcategoría.
CREATE OR REPLACE FUNCTION sp_get_stock_report_by_department(
    p_warehouse_id BIGINT,
    p_department_id BIGINT,
    p_date_from DATE,
    p_date_to DATE
)
RETURNS TABLE (
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_id BIGINT,
    department_name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    net_quantity NUMERIC,
    unit_value NUMERIC,
    total_value NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH receipts AS (
        SELECT rd.product_id,
               SUM(rd.quantity) AS qty,
               SUM(rd.quantity * rd.unit_cost) AS cost_total
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR r.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR r.issue_date < p_date_to + 1)
        GROUP BY rd.product_id
    ),
    issues AS (
        SELECT idt.product_id,
               SUM(idt.quantity) AS qty
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR i.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR i.issue_date < p_date_to + 1)
        GROUP BY idt.product_id
    ),
    transfers_in AS (
        SELECT td.product_id,
               SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE (p_warehouse_id IS NULL OR t.destination_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    ),
    transfers_out AS (
        SELECT td.product_id,
               SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE (p_warehouse_id IS NULL OR t.source_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    )
    SELECT
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        COALESCE(r.qty, 0) - COALESCE(iss.qty, 0) + COALESCE(ti.qty, 0) - COALESCE(tou.qty, 0) AS net_quantity,
        CASE WHEN COALESCE(r.qty, 0) = 0 THEN 0 ELSE r.cost_total / r.qty END AS unit_value,
        (COALESCE(r.qty, 0) - COALESCE(iss.qty, 0) + COALESCE(ti.qty, 0) - COALESCE(tou.qty, 0))
            * (CASE WHEN COALESCE(r.qty, 0) = 0 THEN 0 ELSE r.cost_total / r.qty END) AS total_value
    FROM product p
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    LEFT JOIN receipts r ON r.product_id = p.product_id
    LEFT JOIN issues iss ON iss.product_id = p.product_id
    LEFT JOIN transfers_in ti ON ti.product_id = p.product_id
    LEFT JOIN transfers_out tou ON tou.product_id = p.product_id
    WHERE (p_department_id IS NULL OR p.department_id = p_department_id)
      AND (r.product_id IS NOT NULL OR iss.product_id IS NOT NULL
       OR ti.product_id IS NOT NULL OR tou.product_id IS NOT NULL)
    ORDER BY sd.name, p.name;
$$;

-- Reporte "Kardex Físico Valorado": saldo inicial, entradas, salidas y saldo final
-- por producto en un período, valorizado. El costo de las Salidas ya viene calculado
-- (costo promedio ponderado automático, guardado en issue_detail.total_cost al momento
-- de la Salida) — este reporte no lo recalcula, solo lo acumula. Las Transferencias no
-- tienen columna propia (no la pide el reporte) pero si existen se pliegan dentro de
-- Entradas (transferencia entrante) y Salidas (transferencia saliente) tanto en cantidad
-- como en valor, valorizadas al costo promedio ponderado de las Entradas del tramo
-- correspondiente (antes de p_date_from para el saldo inicial, dentro del rango para el
-- período) — igual criterio de valorización que sp_get_stock_report. Si no hay
-- transferencias, el resultado es exactamente Saldo Inicial = Entradas − Salidas
-- históricas, y Saldos = Saldo Inicial + Entradas − Salidas del período.
CREATE OR REPLACE FUNCTION sp_get_kardex_report(
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE
)
RETURNS TABLE (
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    opening_quantity NUMERIC,
    opening_value NUMERIC,
    receipts_quantity NUMERIC,
    receipts_value NUMERIC,
    issues_quantity NUMERIC,
    issues_value NUMERIC,
    closing_quantity NUMERIC,
    closing_value NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH receipts_before AS (
        SELECT rd.product_id, SUM(rd.quantity) AS qty, SUM(rd.total_cost) AS cost_total
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND r.issue_date < p_date_from
        GROUP BY rd.product_id
    ),
    issues_before AS (
        SELECT idt.product_id, SUM(idt.quantity) AS qty, SUM(idt.total_cost) AS cost_total
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND i.issue_date < p_date_from
        GROUP BY idt.product_id
    ),
    transfers_in_before AS (
        SELECT td.product_id, SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE (p_warehouse_id IS NULL OR t.destination_warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND t.transfer_date < p_date_from
        GROUP BY td.product_id
    ),
    transfers_out_before AS (
        SELECT td.product_id, SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE (p_warehouse_id IS NULL OR t.source_warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND t.transfer_date < p_date_from
        GROUP BY td.product_id
    ),
    receipts_period AS (
        SELECT rd.product_id, SUM(rd.quantity) AS qty, SUM(rd.total_cost) AS cost_total
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR r.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR r.issue_date < p_date_to + 1)
        GROUP BY rd.product_id
    ),
    issues_period AS (
        SELECT idt.product_id, SUM(idt.quantity) AS qty, SUM(idt.total_cost) AS cost_total
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR i.issue_date >= p_date_from)
          AND (p_date_to IS NULL OR i.issue_date < p_date_to + 1)
        GROUP BY idt.product_id
    ),
    transfers_in_period AS (
        SELECT td.product_id, SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE (p_warehouse_id IS NULL OR t.destination_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    ),
    transfers_out_period AS (
        SELECT td.product_id, SUM(td.quantity) AS qty
        FROM transfer_detail td
        JOIN transfer t ON t.transfer_id = td.transfer_id
        WHERE (p_warehouse_id IS NULL OR t.source_warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR t.transfer_date >= p_date_from)
          AND (p_date_to IS NULL OR t.transfer_date < p_date_to + 1)
        GROUP BY td.product_id
    )
    SELECT
        p.product_id,
        p.code,
        p.name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        (COALESCE(rb.qty, 0) - COALESCE(ib.qty, 0) + COALESCE(tib.qty, 0) - COALESCE(tob.qty, 0)) AS opening_quantity,
        (COALESCE(rb.cost_total, 0) - COALESCE(ib.cost_total, 0)
            + COALESCE(tib.qty, 0) * (CASE WHEN COALESCE(rb.qty, 0) = 0 THEN 0 ELSE rb.cost_total / rb.qty END)
            - COALESCE(tob.qty, 0) * (CASE WHEN COALESCE(rb.qty, 0) = 0 THEN 0 ELSE rb.cost_total / rb.qty END)
        ) AS opening_value,
        (COALESCE(rp.qty, 0) + COALESCE(tip.qty, 0)) AS receipts_quantity,
        (COALESCE(rp.cost_total, 0)
            + COALESCE(tip.qty, 0) * (CASE WHEN COALESCE(rp.qty, 0) = 0 THEN 0 ELSE rp.cost_total / rp.qty END)
        ) AS receipts_value,
        (COALESCE(ip.qty, 0) + COALESCE(tup.qty, 0)) AS issues_quantity,
        (COALESCE(ip.cost_total, 0)
            + COALESCE(tup.qty, 0) * (CASE WHEN COALESCE(rp.qty, 0) = 0 THEN 0 ELSE rp.cost_total / rp.qty END)
        ) AS issues_value,
        (
            (COALESCE(rb.qty, 0) - COALESCE(ib.qty, 0) + COALESCE(tib.qty, 0) - COALESCE(tob.qty, 0))
            + (COALESCE(rp.qty, 0) + COALESCE(tip.qty, 0))
            - (COALESCE(ip.qty, 0) + COALESCE(tup.qty, 0))
        ) AS closing_quantity,
        (
            (COALESCE(rb.cost_total, 0) - COALESCE(ib.cost_total, 0)
                + COALESCE(tib.qty, 0) * (CASE WHEN COALESCE(rb.qty, 0) = 0 THEN 0 ELSE rb.cost_total / rb.qty END)
                - COALESCE(tob.qty, 0) * (CASE WHEN COALESCE(rb.qty, 0) = 0 THEN 0 ELSE rb.cost_total / rb.qty END))
            + (COALESCE(rp.cost_total, 0)
                + COALESCE(tip.qty, 0) * (CASE WHEN COALESCE(rp.qty, 0) = 0 THEN 0 ELSE rp.cost_total / rp.qty END))
            - (COALESCE(ip.cost_total, 0)
                + COALESCE(tup.qty, 0) * (CASE WHEN COALESCE(rp.qty, 0) = 0 THEN 0 ELSE rp.cost_total / rp.qty END))
        ) AS closing_value
    FROM product p
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN receipts_before rb ON rb.product_id = p.product_id
    LEFT JOIN issues_before ib ON ib.product_id = p.product_id
    LEFT JOIN transfers_in_before tib ON tib.product_id = p.product_id
    LEFT JOIN transfers_out_before tob ON tob.product_id = p.product_id
    LEFT JOIN receipts_period rp ON rp.product_id = p.product_id
    LEFT JOIN issues_period ip ON ip.product_id = p.product_id
    LEFT JOIN transfers_in_period tip ON tip.product_id = p.product_id
    LEFT JOIN transfers_out_period tup ON tup.product_id = p.product_id
    WHERE rb.product_id IS NOT NULL OR ib.product_id IS NOT NULL
       OR tib.product_id IS NOT NULL OR tob.product_id IS NOT NULL
       OR rp.product_id IS NOT NULL OR ip.product_id IS NOT NULL
       OR tip.product_id IS NOT NULL OR tup.product_id IS NOT NULL
    ORDER BY sd.name, p.name;
$$;

-- Reporte "Levantamiento de Inventario": detalle de conteo físico (cantidad de sistema
-- vs. física, valorizado) por almacén y rango de fechas, filtrado sobre inventory_count
-- (levantamiento_inventario). inventory_count.source_count_id encadena un recuento con el
-- levantamiento original del que nació (mismo patrón auto-referencial que menu.parent_menu_id).
-- Cuando el rango de fechas filtrado incluye tanto un levantamiento como su recuento
-- posterior, se muestra solo el más reciente de la cadena (visible_counts descarta
-- cualquier levantamiento que sea el origen de otro levantamiento también incluido en el
-- filtro) — así no aparecen cantidades físicas contradictorias del mismo producto.
CREATE OR REPLACE FUNCTION sp_get_inventory_count_report(
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE
)
RETURNS TABLE (
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_id BIGINT,
    department_name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    quantity NUMERIC,
    unit_value NUMERIC,
    total_value NUMERIC,
    physical_quantity NUMERIC,
    difference NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH filtered_counts AS (
        SELECT ic.inventory_count_id, ic.source_count_id
        FROM inventory_count ic
        WHERE (p_warehouse_id IS NULL OR ic.warehouse_id = p_warehouse_id)
          AND (p_date_from IS NULL OR ic.start_date >= p_date_from)
          AND (p_date_to IS NULL OR ic.end_date <= p_date_to)
    ),
    visible_counts AS (
        SELECT fc.inventory_count_id
        FROM filtered_counts fc
        WHERE fc.inventory_count_id NOT IN (
            SELECT fc2.source_count_id FROM filtered_counts fc2 WHERE fc2.source_count_id IS NOT NULL
        )
    )
    SELECT
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        icd.system_quantity AS quantity,
        icd.unit_value,
        (icd.system_quantity * COALESCE(icd.unit_value, 0)) AS total_value,
        icd.physical_quantity,
        icd.difference
    FROM inventory_count_detail icd
    JOIN visible_counts vc ON vc.inventory_count_id = icd.inventory_count_id
    JOIN product p ON p.product_id = icd.product_id
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    ORDER BY d.name, sd.name, p.name;
$$;

-- Reporte "Salidas de Almacén": detalle de línea por línea de cada Salida en el período
-- (no agrupa por producto como Kardex/Stock — el mismo producto puede repetirse una fila
-- por cada Salida en la que aparece, ya que cada fila es un movimiento individual).
CREATE OR REPLACE FUNCTION sp_get_issues_report(
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE
)
RETURNS TABLE (
    issue_id BIGINT,
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_id BIGINT,
    department_name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    quantity NUMERIC,
    unit_cost NUMERIC,
    total_cost NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        i.issue_id,
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        idt.quantity,
        idt.unit_cost,
        idt.total_cost
    FROM issue_detail idt
    JOIN issue i ON i.issue_id = idt.issue_id
    JOIN product p ON p.product_id = idt.product_id
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    WHERE (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
      AND (p_date_from IS NULL OR i.issue_date >= p_date_from)
      AND (p_date_to IS NULL OR i.issue_date < p_date_to + 1)
    ORDER BY d.name, sd.name, p.name, i.issue_id;
$$;

-- Reporte "Ingresos de Almacén": detalle de línea por línea de cada Entrada en el período
-- (mismo criterio que sp_get_issues_report — no agrupa por producto, cada fila es un
-- movimiento individual, ya que el mismo producto puede repetirse una fila por cada
-- Entrada en la que aparece).
CREATE OR REPLACE FUNCTION sp_get_receipts_report(
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE
)
RETURNS TABLE (
    receipt_id BIGINT,
    receipt_date TIMESTAMP,
    product_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_id BIGINT,
    department_name VARCHAR,
    sub_department_id BIGINT,
    sub_department_name VARCHAR,
    quantity NUMERIC,
    unit_cost NUMERIC,
    total_cost NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        r.receipt_id,
        r.issue_date AS receipt_date,
        p.product_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        p.department_id,
        d.name AS department_name,
        p.sub_department_id,
        sd.name AS sub_department_name,
        rd.quantity,
        rd.unit_cost,
        rd.total_cost
    FROM receipt_detail rd
    JOIN receipt r ON r.receipt_id = rd.receipt_id
    JOIN product p ON p.product_id = rd.product_id
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN sub_department sd ON sd.sub_department_id = p.sub_department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    WHERE (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
      AND (p_date_from IS NULL OR r.issue_date >= p_date_from)
      AND (p_date_to IS NULL OR r.issue_date < p_date_to + 1)
    ORDER BY d.name, sd.name, p.name, r.receipt_id;
$$;

-- Comprobante de Ingreso individual: líneas de UNA sola entrada (botón "Imprimir" en la
-- lista de Entradas), sin agrupar por departamento como sp_get_receipts_report — el
-- comprobante es una tabla plana en el orden en que se cargaron las líneas.
CREATE OR REPLACE FUNCTION sp_get_receipt_voucher_lines(
    p_receipt_id BIGINT
)
RETURNS TABLE (
    receipt_detail_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_name VARCHAR,
    quantity NUMERIC,
    unit_cost NUMERIC,
    total_cost NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        rd.receipt_detail_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        d.name AS department_name,
        rd.quantity,
        rd.unit_cost,
        rd.total_cost
    FROM receipt_detail rd
    JOIN product p ON p.product_id = rd.product_id
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    WHERE rd.receipt_id = p_receipt_id
    ORDER BY rd.receipt_detail_id;
$$;

-- Comprobante de Salida individual: líneas de UNA sola salida (se genera automáticamente al
-- guardar la Salida, no desde un botón en la lista, salvo que el Tipo de Impresión sea "Sin
-- Impresión"). Misma forma que sp_get_receipt_voucher_lines.
CREATE OR REPLACE FUNCTION sp_get_issue_voucher_lines(
    p_issue_id BIGINT
)
RETURNS TABLE (
    issue_detail_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_name VARCHAR,
    quantity NUMERIC,
    unit_cost NUMERIC,
    total_cost NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        idt.issue_detail_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        d.name AS department_name,
        idt.quantity,
        idt.unit_cost,
        idt.total_cost
    FROM issue_detail idt
    JOIN product p ON p.product_id = idt.product_id
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    WHERE idt.issue_id = p_issue_id
    ORDER BY idt.issue_detail_id;
$$;

-- Comprobante de Transferencia individual: mismo patrón que sp_get_receipt_voucher_lines/
-- sp_get_issue_voucher_lines, pero transfer_detail usa unit_price/total_sale_price (Precio
-- de Venta) en vez de unit_cost/total_cost — es un concepto de dinero distinto (precio al que
-- se transfiere, no el costo promedio del producto).
CREATE OR REPLACE FUNCTION sp_get_transfer_voucher_lines(
    p_transfer_id BIGINT
)
RETURNS TABLE (
    transfer_detail_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    unit_measure VARCHAR,
    department_name VARCHAR,
    quantity NUMERIC,
    unit_price NUMERIC,
    total_sale_price NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        td.transfer_detail_id,
        p.code,
        p.name,
        mt.name AS unit_measure,
        d.name AS department_name,
        td.quantity,
        COALESCE(td.unit_price, 0),
        COALESCE(td.total_sale_price, 0)
    FROM transfer_detail td
    JOIN product p ON p.product_id = td.product_id
    LEFT JOIN department d ON d.department_id = p.department_id
    LEFT JOIN media_type mt ON mt.media_type_id = p.media_type_id
    WHERE td.transfer_id = p_transfer_id
    ORDER BY td.transfer_detail_id;
$$;

-- Reporte "Kardex por Material": ficha kardex tradicional de UN producto — cada fila es un
-- movimiento individual (Entrada o Salida, con su N° de comprobante) en orden cronológico,
-- con el saldo (cantidad/valor) acumulado después de ese movimiento. Decisión explícita: no
-- incluye Transferencias — el saldo solo se explica por Entradas y Salidas visibles en la
-- tabla, para que no haya saltos de saldo entre dos filas sin una fila que los explique.
CREATE OR REPLACE FUNCTION sp_get_kardex_by_product_report(
    p_product_id BIGINT,
    p_warehouse_id BIGINT,
    p_date_from DATE,
    p_date_to DATE
)
RETURNS TABLE (
    movement_type VARCHAR,
    voucher_id BIGINT,
    voucher_date TIMESTAMP,
    quantity NUMERIC,
    unit_cost NUMERIC,
    total_cost NUMERIC,
    balance_quantity NUMERIC,
    balance_value NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH movements AS (
        SELECT 'receipt' AS movement_type, r.receipt_id AS voucher_id, r.issue_date AS voucher_date,
               rd.quantity, rd.unit_cost, rd.total_cost
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE rd.product_id = p_product_id
          AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
        UNION ALL
        SELECT 'issue' AS movement_type, i.issue_id AS voucher_id, i.issue_date AS voucher_date,
               idt.quantity, idt.unit_cost, idt.total_cost
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE idt.product_id = p_product_id
          AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
    ),
    running AS (
        SELECT
            movement_type, voucher_id, voucher_date, quantity, unit_cost, total_cost,
            SUM(CASE WHEN movement_type = 'receipt' THEN quantity ELSE -quantity END)
                OVER (ORDER BY voucher_date, voucher_id ROWS UNBOUNDED PRECEDING) AS balance_quantity,
            SUM(CASE WHEN movement_type = 'receipt' THEN total_cost ELSE -total_cost END)
                OVER (ORDER BY voucher_date, voucher_id ROWS UNBOUNDED PRECEDING) AS balance_value
        FROM movements
    )
    SELECT movement_type, voucher_id, voucher_date, quantity, unit_cost, total_cost, balance_quantity, balance_value
    FROM running
    WHERE (p_date_from IS NULL OR voucher_date >= p_date_from)
      AND (p_date_to IS NULL OR voucher_date < p_date_to + 1)
    ORDER BY voucher_date, voucher_id;
$$;

-- Saldo inicial del "Kardex por Material": neto Entradas − Salidas del producto en el almacén,
-- estrictamente antes de la fecha de inicio filtrada (mismo criterio de "antes del período" que
-- usa sp_get_kardex_report, sin Transferencias).
CREATE OR REPLACE FUNCTION sp_get_kardex_by_product_opening_balance(
    p_product_id BIGINT,
    p_warehouse_id BIGINT,
    p_date_from DATE
)
RETURNS TABLE (
    opening_quantity NUMERIC,
    opening_value NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH receipts_before AS (
        SELECT COALESCE(SUM(rd.quantity), 0) AS qty, COALESCE(SUM(rd.total_cost), 0) AS cost_total
        FROM receipt_detail rd
        JOIN receipt r ON r.receipt_id = rd.receipt_id
        WHERE rd.product_id = p_product_id
          AND (p_warehouse_id IS NULL OR r.warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND r.issue_date < p_date_from
    ),
    issues_before AS (
        SELECT COALESCE(SUM(idt.quantity), 0) AS qty, COALESCE(SUM(idt.total_cost), 0) AS cost_total
        FROM issue_detail idt
        JOIN issue i ON i.issue_id = idt.issue_id
        WHERE idt.product_id = p_product_id
          AND (p_warehouse_id IS NULL OR i.warehouse_id = p_warehouse_id)
          AND p_date_from IS NOT NULL AND i.issue_date < p_date_from
    )
    SELECT
        (SELECT qty FROM receipts_before) - (SELECT qty FROM issues_before) AS opening_quantity,
        (SELECT cost_total FROM receipts_before) - (SELECT cost_total FROM issues_before) AS opening_value;
$$;

-- Reporte "Campo Pastor": ficha de un cliente/pastor específico. "Depósito" en este sistema
-- es un Pago (payment) contra una Cuenta por Cobrar — no existe una tabla "deposito" separada
-- ligada a producto; el histórico de depósitos que pide el reporte es el histórico de pagos,
-- mismo criterio que ya usa la pantalla de autoservicio "Mi Cuenta" del Pastor.
CREATE OR REPLACE FUNCTION sp_get_pastor_field_receivables(
    p_client_id BIGINT
)
RETURNS TABLE (
    account_receivable_id BIGINT,
    issue_id BIGINT,
    issue_date TIMESTAMP,
    due_date DATE,
    outstanding_balance NUMERIC,
    status VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        ar.account_receivable_id,
        ar.issue_id,
        i.issue_date,
        ar.due_date,
        ar.outstanding_balance,
        ar.status
    FROM account_receivable ar
    LEFT JOIN issue i ON i.issue_id = ar.issue_id
    WHERE ar.client_id = p_client_id
    ORDER BY (ar.status = 'paid'), ar.due_date NULLS LAST;
$$;

CREATE OR REPLACE FUNCTION sp_get_pastor_field_last_delivery(
    p_client_id BIGINT
)
RETURNS TABLE (
    last_delivery_date TIMESTAMP
)
LANGUAGE sql
STABLE
AS $$
    SELECT MAX(i.issue_date) AS last_delivery_date
    FROM issue i
    WHERE i.client_id = p_client_id;
$$;

CREATE OR REPLACE FUNCTION sp_get_pastor_field_deliveries(
    p_client_id BIGINT,
    p_date_from DATE,
    p_date_to DATE
)
RETURNS TABLE (
    issue_id BIGINT,
    issue_date TIMESTAMP,
    total NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        i.issue_id,
        i.issue_date,
        COALESCE(SUM(idt.total_cost), 0) AS total
    FROM issue i
    LEFT JOIN issue_detail idt ON idt.issue_id = i.issue_id
    WHERE i.client_id = p_client_id
      AND (p_date_from IS NULL OR i.issue_date >= p_date_from)
      AND (p_date_to IS NULL OR i.issue_date < p_date_to + 1)
    GROUP BY i.issue_id, i.issue_date
    ORDER BY i.issue_date DESC;
$$;

CREATE OR REPLACE FUNCTION sp_get_pastor_field_deposits(
    p_client_id BIGINT,
    p_date_from DATE,
    p_date_to DATE
)
RETURNS TABLE (
    payment_id BIGINT,
    payment_date TIMESTAMP,
    issue_id BIGINT,
    amount NUMERIC,
    payment_method VARCHAR,
    notes VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        p.payment_id,
        p.payment_date,
        ar.issue_id,
        p.amount,
        p.payment_method,
        p.notes
    FROM payment p
    JOIN account_receivable ar ON ar.account_receivable_id = p.account_receivable_id
    WHERE ar.client_id = p_client_id
      AND (p_date_from IS NULL OR p.payment_date >= p_date_from)
      AND (p_date_to IS NULL OR p.payment_date < p_date_to + 1)
    ORDER BY p.payment_date DESC;
$$;
