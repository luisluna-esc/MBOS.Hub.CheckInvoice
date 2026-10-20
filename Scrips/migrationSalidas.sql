-- =====================================================================================
-- Salidas igual que Entradas. Se puede correr más de una vez. Correr DESPUÉS de
-- migrationEntradasCompras.sql y ANTES de levantar el backend nuevo.
-- =====================================================================================

-- 1. N° de salida por mes ("S 15-10-26"), como la nota de ingreso. Las salidas que ya existen
--    conservan su N° interno (no se renumeran).
ALTER TABLE issue ADD COLUMN IF NOT EXISTS monthly_number INT;
CREATE UNIQUE INDEX IF NOT EXISTS uq_issue_monthly_number
    ON issue (date_trunc('month', issue_date), monthly_number);

CREATE OR REPLACE FUNCTION fn_issue_number(p_issue_id BIGINT)
RETURNS TEXT
LANGUAGE sql
STABLE
AS $$
    -- N° de salida como se imprime: "S 15-10-26" (salida 15 de octubre de 2026). Las salidas de
    -- antes de la numeración por mes muestran su N° interno ("00019").
    SELECT CASE WHEN i.monthly_number IS NOT NULL
                THEN 'S ' || i.monthly_number || '-' || to_char(i.issue_date, 'MM-YY')
                ELSE lpad(i.issue_id::text, 5, '0') END
    FROM issue i
    WHERE i.issue_id = p_issue_id;
$$;

-- 2. Correcciones de Salidas: se piden con motivo y las aprueba el Contador (Autorizaciones).
ALTER TABLE change_request DROP CONSTRAINT IF EXISTS chk_change_request_table;
ALTER TABLE change_request ADD CONSTRAINT chk_change_request_table
    CHECK (table_name IN ('receipt', 'issue', 'transfer', 'receipt_edit', 'issue_edit'));

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Correcciones de Salidas', 'nav.issueEditRequests', '/issues/edit-requests', NULL, m.menu_id, 5, TRUE, NULL
FROM menu m
WHERE m.name = 'Autorizaciones' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu_role (menu_id, role_id)
SELECT c.menu_id, mr.role_id
FROM menu c
JOIN menu r ON r.name = 'Correcciones de Entradas'
JOIN menu_role mr ON mr.menu_id = r.menu_id
WHERE c.name = 'Correcciones de Salidas'
ON CONFLICT DO NOTHING;

-- 3. Historial de Salidas en Reportes (la lista); el menú Salidas queda para registrar.
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active, permission_id)
SELECT 'Historial de Salidas', 'nav.reportsIssueHistory', '/reports/issue-history', NULL, m.menu_id, 1, TRUE, NULL
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO menu_role (menu_id, role_id)
SELECT h.menu_id, mr.role_id
FROM menu h
JOIN menu r ON r.name = 'Historial de Entradas'
JOIN menu_role mr ON mr.menu_id = r.menu_id
WHERE h.name = 'Historial de Salidas'
ON CONFLICT DO NOTHING;

-- 4. Buzón: N° de salida nuevo y correcciones de salidas (pendientes, respuestas y lo que firmé).
CREATE OR REPLACE FUNCTION sp_inbox_items(
    p_user_id BIGINT,
    p_can_verify BOOLEAN,
    p_can_approve BOOLEAN
)
RETURNS TABLE (
    tab VARCHAR,
    kind VARCHAR,
    document_type VARCHAR,
    document_id BIGINT,
    number TEXT,
    detail TEXT,
    status VARCHAR,
    at TIMESTAMP,
    notification_key TEXT
)
LANGUAGE sql
STABLE
AS $$
    WITH pending AS (
        SELECT 'toVerify'::varchar AS kind, 'receipt'::varchar AS document_type, r.receipt_id AS document_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') AS number,
               rt.name::text AS detail, NULL::varchar AS status, r.created_at AS at
        FROM receipt r LEFT JOIN receipt_type rt ON rt.receipt_type_id = r.receipt_type_id
        WHERE p_can_verify AND NOT r.is_voided AND r.review_status = 'digitado'
        UNION ALL
        SELECT 'toVerify', 'issue', i.issue_id, fn_issue_number(i.issue_id), it.name::text, NULL, i.created_at
        FROM issue i LEFT JOIN issue_type it ON it.issue_type_id = i.issue_type_id
        WHERE p_can_verify AND NOT i.is_voided AND i.review_status = 'digitado'
        UNION ALL
        SELECT 'toVerify', 'transfer', t.transfer_id, lpad(t.transfer_id::text, 5, '0'),
               coalesce(ws.name, '—') || ' → ' || coalesce(wd.name, '—'), NULL, t.transfer_date
        FROM transfer t
        LEFT JOIN warehouse ws ON ws.warehouse_id = t.source_warehouse_id
        LEFT JOIN warehouse wd ON wd.warehouse_id = t.destination_warehouse_id
        WHERE p_can_verify AND NOT t.is_voided AND t.review_status = 'digitado'
        UNION ALL
        SELECT 'toApprove', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), rt.name::text, NULL, r.created_at
        FROM receipt r LEFT JOIN receipt_type rt ON rt.receipt_type_id = r.receipt_type_id
        WHERE p_can_approve AND NOT r.is_voided AND r.review_status = 'verificado'
        UNION ALL
        SELECT 'toApprove', 'issue', i.issue_id, fn_issue_number(i.issue_id), it.name::text, NULL, i.created_at
        FROM issue i LEFT JOIN issue_type it ON it.issue_type_id = i.issue_type_id
        WHERE p_can_approve AND NOT i.is_voided AND i.review_status = 'verificado'
        UNION ALL
        SELECT 'toApprove', 'transfer', t.transfer_id, lpad(t.transfer_id::text, 5, '0'),
               coalesce(ws.name, '—') || ' → ' || coalesce(wd.name, '—'), NULL, t.transfer_date
        FROM transfer t
        LEFT JOIN warehouse ws ON ws.warehouse_id = t.source_warehouse_id
        LEFT JOIN warehouse wd ON wd.warehouse_id = t.destination_warehouse_id
        WHERE p_can_approve AND NOT t.is_voided AND t.review_status = 'verificado'
        UNION ALL
        SELECT 'voidRequest', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), v.detail::text, NULL, v.requested_at
        FROM receipt_void_request v JOIN receipt r ON r.receipt_id = v.receipt_id
        WHERE p_can_approve AND v.status = 'pending'
        UNION ALL
        SELECT 'voidRequest', 'issue', v.issue_id, fn_issue_number(v.issue_id), v.detail::text, NULL, v.requested_at
        FROM issue_void_request v
        WHERE p_can_approve AND v.status = 'pending'
        UNION ALL
        SELECT 'voidRequest', 'transfer', v.transfer_id, lpad(v.transfer_id::text, 5, '0'), v.detail::text, NULL, v.requested_at
        FROM transfer_void_request v
        WHERE p_can_approve AND v.status = 'pending'
        UNION ALL
        SELECT 'receiptEdit', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), c.reason::text, NULL, c.requested_at
        FROM change_request c JOIN receipt r ON r.receipt_id = c.record_id
        WHERE p_can_approve AND c.table_name = 'receipt_edit' AND c.status = 'pending'
        UNION ALL
        SELECT 'issueEdit', 'issue', c.record_id, fn_issue_number(c.record_id), c.reason::text, NULL, c.requested_at
        FROM change_request c
        WHERE p_can_approve AND c.table_name = 'issue_edit' AND c.status = 'pending'
    ),
    -- Respuestas a lo que pidió o digitó el usuario (anulaciones, correcciones y la firma final
    -- de sus documentos, aprobados o rechazados).
    notifications AS (
        SELECT 'voidRequest'::varchar AS kind, 'receipt'::varchar AS document_type, r.receipt_id AS document_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') AS number,
               v.review_notes::text AS detail, v.status AS status, v.reviewed_at AS at,
               'receipt_void:' || v.receipt_void_request_id AS notification_key
        FROM receipt_void_request v JOIN receipt r ON r.receipt_id = v.receipt_id
        WHERE v.requested_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'voidRequest', 'issue', v.issue_id, fn_issue_number(v.issue_id), v.review_notes::text, v.status, v.reviewed_at,
               'issue_void:' || v.issue_void_request_id
        FROM issue_void_request v
        WHERE v.requested_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'voidRequest', 'transfer', v.transfer_id, lpad(v.transfer_id::text, 5, '0'), v.review_notes::text, v.status, v.reviewed_at,
               'transfer_void:' || v.transfer_void_request_id
        FROM transfer_void_request v
        WHERE v.requested_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'receiptEdit', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), c.review_notes::text, c.status, c.reviewed_at,
               'receipt_edit:' || c.change_request_id
        FROM change_request c JOIN receipt r ON r.receipt_id = c.record_id
        WHERE c.table_name = 'receipt_edit' AND c.requested_by = p_user_id AND c.status <> 'pending'
        UNION ALL
        SELECT 'issueEdit', 'issue', c.record_id, fn_issue_number(c.record_id), c.review_notes::text, c.status, c.reviewed_at,
               'issue_edit:' || c.change_request_id
        FROM change_request c
        WHERE c.table_name = 'issue_edit' AND c.requested_by = p_user_id AND c.status <> 'pending'
        UNION ALL
        -- Documentos que digitó el usuario y el Contador aprobó o rechazó.
        SELECT CASE WHEN d.action = 'aprobado' THEN 'reviewApproved' ELSE 'reviewRejected' END::varchar,
               d.document_type, d.document_id,
               CASE WHEN d.document_type = 'receipt'
                    THEN (SELECT 'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') FROM receipt r WHERE r.receipt_id = d.document_id)
                    WHEN d.document_type = 'issue' THEN fn_issue_number(d.document_id)
                    ELSE lpad(d.document_id::text, 5, '0') END,
               d.reason::text,
               CASE WHEN d.action = 'aprobado' THEN 'approved' ELSE 'rejected' END::varchar,
               d.at,
               'review_' || CASE WHEN d.action = 'aprobado' THEN 'approved' ELSE 'rejected' END || ':' || d.document_review_id
        FROM document_review d
        WHERE d.action IN ('aprobado', 'rechazado')
          AND d.user_id <> p_user_id
          AND EXISTS (
              SELECT 1 FROM document_review e
              WHERE e.document_type = d.document_type AND e.document_id = d.document_id
                AND e.action = 'digitado' AND e.user_id = p_user_id)
    ),
    -- Lo que firmó o autorizó el propio usuario.
    signed AS (
        SELECT 'reviewSigned'::varchar AS kind, d.document_type::varchar AS document_type, d.document_id AS document_id,
               CASE WHEN d.document_type = 'receipt'
                    THEN (SELECT 'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') FROM receipt r WHERE r.receipt_id = d.document_id)
                    WHEN d.document_type = 'issue' THEN fn_issue_number(d.document_id)
                    ELSE lpad(d.document_id::text, 5, '0') END AS number,
               d.reason::text AS detail, d.action::varchar AS status, d.at AS at
        FROM document_review d
        WHERE d.user_id = p_user_id AND d.action IN ('verificado', 'aprobado', 'rechazado', 'reabierto')
        UNION ALL
        SELECT 'voidRequest', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), v.review_notes::text, v.status, v.reviewed_at
        FROM receipt_void_request v JOIN receipt r ON r.receipt_id = v.receipt_id
        WHERE v.reviewed_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'voidRequest', 'issue', v.issue_id, fn_issue_number(v.issue_id), v.review_notes::text, v.status, v.reviewed_at
        FROM issue_void_request v
        WHERE v.reviewed_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'voidRequest', 'transfer', v.transfer_id, lpad(v.transfer_id::text, 5, '0'), v.review_notes::text, v.status, v.reviewed_at
        FROM transfer_void_request v
        WHERE v.reviewed_by = p_user_id AND v.status <> 'pending'
        UNION ALL
        SELECT 'receiptEdit', 'receipt', r.receipt_id,
               'MB ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), c.review_notes::text, c.status, c.reviewed_at
        FROM change_request c JOIN receipt r ON r.receipt_id = c.record_id
        WHERE c.table_name = 'receipt_edit' AND c.reviewed_by = p_user_id AND c.status <> 'pending'
        UNION ALL
        SELECT 'issueEdit', 'issue', c.record_id, fn_issue_number(c.record_id), c.review_notes::text, c.status, c.reviewed_at
        FROM change_request c
        WHERE c.table_name = 'issue_edit' AND c.reviewed_by = p_user_id AND c.status <> 'pending'
    )
    SELECT 'pending'::varchar, p.kind, p.document_type, p.document_id, p.number, p.detail, p.status, p.at, NULL::text
    FROM pending p
    UNION ALL
    SELECT CASE WHEN s.notification_key IS NULL THEN 'unread' ELSE 'seen' END::varchar,
           n.kind, n.document_type, n.document_id, n.number, n.detail, n.status, n.at, n.notification_key
    FROM notifications n
    LEFT JOIN inbox_seen s ON s.user_id = p_user_id AND s.notification_key = n.notification_key
    UNION ALL
    SELECT 'signed'::varchar, g.kind, g.document_type, g.document_id, g.number, g.detail, g.status, g.at, NULL::text
    FROM signed g;
$$;

CREATE OR REPLACE FUNCTION sp_get_inbox(
    p_user_id BIGINT,
    p_can_verify BOOLEAN,
    p_can_approve BOOLEAN,
    p_tab VARCHAR,
    p_kind VARCHAR,
    p_document_type VARCHAR,
    p_search VARCHAR,
    p_date_from DATE,
    p_date_to DATE,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    kind VARCHAR,
    document_type VARCHAR,
    document_id BIGINT,
    number TEXT,
    detail TEXT,
    status VARCHAR,
    at TIMESTAMP,
    notification_key TEXT,
    seen BOOLEAN,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    -- p_tab: 'pending', 'answers' (respuestas: sin leer primero, luego las vistas) o 'signed'.
    SELECT i.kind, i.document_type, i.document_id, i.number, i.detail, i.status, i.at, i.notification_key,
           i.tab = 'seen', count(*) OVER ()::int
    FROM sp_inbox_items(p_user_id, p_can_verify, p_can_approve) i
    WHERE (CASE WHEN p_tab = 'answers' THEN i.tab IN ('unread', 'seen') ELSE i.tab = p_tab END)
      AND (p_kind IS NULL OR i.kind = p_kind)
      AND (p_document_type IS NULL OR i.document_type = p_document_type)
      AND (p_search IS NULL OR i.number ILIKE '%' || p_search || '%' OR i.detail ILIKE '%' || p_search || '%')
      AND (p_date_from IS NULL OR (i.at AT TIME ZONE 'UTC' AT TIME ZONE 'America/La_Paz')::date >= p_date_from)
      AND (p_date_to IS NULL OR (i.at AT TIME ZONE 'UTC' AT TIME ZONE 'America/La_Paz')::date <= p_date_to)
    ORDER BY (i.tab = 'unread') DESC, i.at DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- Comprobación
SELECT m.name, string_agg(r.name, ', ' ORDER BY r.name) AS roles
FROM menu m JOIN menu_role mr ON mr.menu_id = m.menu_id JOIN role r ON r.role_id = mr.role_id
WHERE m.name IN ('Historial de Salidas', 'Correcciones de Salidas')
GROUP BY m.name;

-- Historial de Salidas: las del mismo día, la más nueva primero (S 2 antes que S 1).
CREATE OR REPLACE FUNCTION sp_get_issues(
    p_issue_id BIGINT,
    p_client_id BIGINT,
    p_warehouse_id BIGINT,
    p_issue_type_id BIGINT,
    p_page_number INT,
    p_page_size INT,
    p_client_name VARCHAR DEFAULT NULL
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
    LEFT JOIN client c ON c.client_id = i.client_id
    LEFT JOIN party cp ON cp.party_id = c.party_id
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
      AND (p_client_name IS NULL OR cp.name ILIKE '%' || p_client_name || '%')
    ORDER BY i.issue_date DESC, i.issue_id DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;
