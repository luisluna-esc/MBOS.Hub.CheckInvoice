-- =====================================================================================
-- Buzón (2026-11-04):
--   * Salida por lote: el lote se firma una sola vez. El Buzón muestra una fila por lote
--     ("Salida por Lote, 6 pastores") en vez de una por pastor; una salida del lote corregida o
--     reabierta sale sola, con el nombre de su pastor. Las firmas de un lote firmado de una vez
--     salen en una sola fila en Firmados y Respuestas.
--   * Correcciones de entradas y salidas: llegan a Contador, M-BOS y Tesorero, menos a quien la
--     pidió. El Auxiliar Contador solo las pide y recibe la respuesta.
-- Se puede correr más de una vez. Solo cambia funciones; no toca datos.
--
-- Correr como postgres:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f migrationFirmaLote.sql
-- =====================================================================================

-- Salidas del buzón en un paso de firma: una fila por salida suelta y una por lote. Las salidas
-- de un lote se firman juntas (DocumentReviewService); una salida corregida o reabierta tiene otra
-- versión y sale sola, con el nombre de su pastor.
CREATE OR REPLACE FUNCTION fn_inbox_issue_groups(p_status VARCHAR)
RETURNS TABLE (document_id BIGINT, detail TEXT, at TIMESTAMP)
LANGUAGE sql
STABLE
AS $$
    SELECT min(i.issue_id),
           CASE WHEN i.issue_batch_id IS NULL THEN max(it.name)::text
                WHEN count(*) > 1 THEN max(it.name) || ', ' || count(*) || ' pastores'
                ELSE max(it.name) || ', ' || coalesce(max(p.name), '—') END,
           min(i.created_at)
    FROM issue i
    LEFT JOIN issue_type it ON it.issue_type_id = i.issue_type_id
    LEFT JOIN client c ON c.client_id = i.client_id
    LEFT JOIN party p ON p.party_id = c.party_id
    WHERE NOT i.is_voided AND i.review_status = p_status
    GROUP BY CASE WHEN i.issue_batch_id IS NULL THEN i.issue_id END, i.issue_batch_id, i.review_version;
$$;

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
    -- Firmas: las de un lote firmado de una vez quedan en una sola fila (la de la primera salida).
    WITH reviews AS (
        SELECT d.*
        FROM document_review d
        LEFT JOIN issue bi ON d.document_type = 'issue' AND bi.issue_id = d.document_id
        WHERE bi.issue_batch_id IS NULL
           OR NOT EXISTS (
               SELECT 1
               FROM document_review o
               JOIN issue oi ON oi.issue_id = o.document_id
               WHERE o.document_type = 'issue' AND oi.issue_batch_id = bi.issue_batch_id
                 AND o.action = d.action AND o.at = d.at AND o.user_id IS NOT DISTINCT FROM d.user_id
                 AND o.document_id < d.document_id)
    ),
    -- Correcciones por revisar: les llegan a Contador, M-BOS y Tesorero (p_can_verify), menos a
    -- quien la pidió. El Auxiliar Contador solo las pide.
    pending AS (
        SELECT 'toVerify'::varchar AS kind, 'receipt'::varchar AS document_type, r.receipt_id AS document_id,
               'I ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') AS number,
               rt.name::text AS detail, NULL::varchar AS status, r.created_at AS at
        FROM receipt r LEFT JOIN receipt_type rt ON rt.receipt_type_id = r.receipt_type_id
        WHERE p_can_verify AND NOT r.is_voided AND r.review_status = 'digitado'
        UNION ALL
        SELECT 'toVerify', 'issue', g.document_id, fn_issue_number(g.document_id), g.detail, NULL, g.at
        FROM fn_inbox_issue_groups('digitado') g
        WHERE p_can_verify
        UNION ALL
        SELECT 'toVerify', 'transfer', t.transfer_id, lpad(t.transfer_id::text, 5, '0'),
               coalesce(ws.name, '—') || ' → ' || coalesce(wd.name, '—'), NULL, t.transfer_date
        FROM transfer t
        LEFT JOIN warehouse ws ON ws.warehouse_id = t.source_warehouse_id
        LEFT JOIN warehouse wd ON wd.warehouse_id = t.destination_warehouse_id
        WHERE p_can_verify AND NOT t.is_voided AND t.review_status = 'digitado'
        UNION ALL
        SELECT 'toApprove', 'receipt', r.receipt_id,
               'I ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), rt.name::text, NULL, r.created_at
        FROM receipt r LEFT JOIN receipt_type rt ON rt.receipt_type_id = r.receipt_type_id
        WHERE p_can_approve AND NOT r.is_voided AND r.review_status = 'verificado'
        UNION ALL
        SELECT 'toApprove', 'issue', g.document_id, fn_issue_number(g.document_id), g.detail, NULL, g.at
        FROM fn_inbox_issue_groups('verificado') g
        WHERE p_can_approve
        UNION ALL
        SELECT 'toApprove', 'transfer', t.transfer_id, lpad(t.transfer_id::text, 5, '0'),
               coalesce(ws.name, '—') || ' → ' || coalesce(wd.name, '—'), NULL, t.transfer_date
        FROM transfer t
        LEFT JOIN warehouse ws ON ws.warehouse_id = t.source_warehouse_id
        LEFT JOIN warehouse wd ON wd.warehouse_id = t.destination_warehouse_id
        WHERE p_can_approve AND NOT t.is_voided AND t.review_status = 'verificado'
        UNION ALL
        SELECT 'voidRequest', 'receipt', r.receipt_id,
               'I ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), v.detail::text, NULL, v.requested_at
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
               'I ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), c.reason::text, NULL, c.requested_at
        FROM change_request c JOIN receipt r ON r.receipt_id = c.record_id
        WHERE p_can_verify AND c.requested_by IS DISTINCT FROM p_user_id AND c.table_name = 'receipt_edit' AND c.status = 'pending'
        UNION ALL
        SELECT 'issueEdit', 'issue', c.record_id, fn_issue_number(c.record_id), c.reason::text, NULL, c.requested_at
        FROM change_request c
        WHERE p_can_verify AND c.requested_by IS DISTINCT FROM p_user_id AND c.table_name = 'issue_edit' AND c.status = 'pending'
    ),
    -- Respuestas a lo que pidió o digitó el usuario (anulaciones, correcciones y la firma final
    -- de sus documentos, aprobados o rechazados).
    notifications AS (
        SELECT 'voidRequest'::varchar AS kind, 'receipt'::varchar AS document_type, r.receipt_id AS document_id,
               'I ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') AS number,
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
               'I ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), c.review_notes::text, c.status, c.reviewed_at,
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
                    THEN (SELECT 'I ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') FROM receipt r WHERE r.receipt_id = d.document_id)
                    WHEN d.document_type = 'issue' THEN fn_issue_number(d.document_id)
                    ELSE lpad(d.document_id::text, 5, '0') END,
               d.reason::text,
               CASE WHEN d.action = 'aprobado' THEN 'approved' ELSE 'rejected' END::varchar,
               d.at,
               'review_' || CASE WHEN d.action = 'aprobado' THEN 'approved' ELSE 'rejected' END || ':' || d.document_review_id
        FROM reviews d
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
                    THEN (SELECT 'I ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY') FROM receipt r WHERE r.receipt_id = d.document_id)
                    WHEN d.document_type = 'issue' THEN fn_issue_number(d.document_id)
                    ELSE lpad(d.document_id::text, 5, '0') END AS number,
               d.reason::text AS detail, d.action::varchar AS status, d.at AS at
        FROM reviews d
        WHERE d.user_id = p_user_id AND d.action IN ('verificado', 'aprobado', 'rechazado', 'reabierto')
        UNION ALL
        SELECT 'voidRequest', 'receipt', r.receipt_id,
               'I ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), v.review_notes::text, v.status, v.reviewed_at
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
               'I ' || r.monthly_number || '-' || to_char(r.issue_date, 'MM-YY'), c.review_notes::text, c.status, c.reviewed_at
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
