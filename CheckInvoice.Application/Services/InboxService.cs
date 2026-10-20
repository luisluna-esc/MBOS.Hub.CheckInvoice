using System.Net;
using CheckInvoice.Application.Dtos.Governance;
using CheckInvoice.Application.Interfaces.Governance;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;

namespace CheckInvoice.Application.Services;

// Buzón (procedimientos sp_get_inbox, sp_get_inbox_counts y sp_mark_inbox_seen):
//   Pendientes: verificar y firmar (Contador, Tesorero, M-BOS); aprobar documentos y autorizar
//   anulaciones y correcciones (Contador, M-BOS).
//   Notificaciones: cómo terminaron las solicitudes del usuario y sus documentos rechazados;
//   quedan "sin leer" hasta que las abre o las marca como vistas.
public class InboxService : IInboxService
{
    private static readonly string[] VerifierRoles = ["Contador", "Tesorero", "M-BOS"];
    private static readonly string[] ApproverRoles = ["Contador", "M-BOS"];

    private const string GetInboxSql = """
        SELECT
            kind AS "Kind",
            document_type AS "DocumentType",
            document_id AS "DocumentId",
            number AS "Number",
            detail AS "Detail",
            status AS "Status",
            at AS "At",
            notification_key AS "NotificationKey",
            seen AS "Seen",
            total_records AS "TotalRecords"
        FROM sp_get_inbox({0}::bigint, {1}::boolean, {2}::boolean, {3}::varchar, {4}::varchar, {5}::varchar,
                          {6}::varchar, {7}::date, {8}::date, {9}::int, {10}::int)
        """;

    private const string GetCountsSql = """
        SELECT tab AS "Tab", kind AS "Kind", total AS "Total"
        FROM sp_get_inbox_counts({0}::bigint, {1}::boolean, {2}::boolean)
        """;

    private const string MarkSeenSql = """
        SELECT marked AS "Marked" FROM sp_mark_inbox_seen({0}::bigint, {1}::text[])
        """;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public InboxService(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    private (long UserId, bool CanVerify, bool CanApprove) Who() =>
        (_currentUserService.AppUserId ?? 0,
         VerifierRoles.Any(_currentUserService.IsInRole),
         ApproverRoles.Any(_currentUserService.IsInRole));

    public async Task<ResponseGetObject> GetCounts()
    {
        var (userId, canVerify, canApprove) = Who();
        var rows = await _unitOfWork.SqlQueryAsync<InboxCountRow>(GetCountsSql, userId, canVerify, canApprove);
        var counts = new InboxCountsDto
        {
            Pending = rows.Where(r => r.Tab == "pending").Sum(r => r.Total),
            Unread = rows.Where(r => r.Tab == "unread").Sum(r => r.Total),
            Seen = rows.Where(r => r.Tab == "seen").Sum(r => r.Total),
            Signed = rows.Where(r => r.Tab == "signed").Sum(r => r.Total),
            ByKind = rows.Where(r => r.Tab == "pending").ToDictionary(r => r.Kind, r => r.Total),
            CanVerify = canVerify,
            CanApprove = canApprove
        };
        return new ResponseGetObject { Data = counts, Messages = [], StatusCode = HttpStatusCode.OK };
    }

    public async Task<ResponseGetObject> GetItems(InboxQueryDto query)
    {
        var (userId, canVerify, canApprove) = Who();
        var tab = query.Tab is "answers" or "signed" or "unread" or "seen" ? query.Tab : "pending";
        var pageSize = query.PageSize is > 0 and <= 100 ? query.PageSize : 20;
        var pageNumber = query.PageNumber > 0 ? query.PageNumber : 1;

        var rows = await _unitOfWork.SqlQueryAsync<InboxRowDto>(
            GetInboxSql,
            userId, canVerify, canApprove, tab,
            (object?)NullIfEmpty(query.Kind) ?? DBNull.Value,
            (object?)NullIfEmpty(query.DocumentType) ?? DBNull.Value,
            (object?)NullIfEmpty(query.Search) ?? DBNull.Value,
            (object?)query.DateFrom ?? DBNull.Value,
            (object?)query.DateTo ?? DBNull.Value,
            pageNumber, pageSize);

        // Instantes en UTC (SqlQueryRaw no pasa por el conversor global de fechas).
        foreach (var row in rows)
        {
            row.At = DateTime.SpecifyKind(row.At, DateTimeKind.Utc);
        }

        return new ResponseGetObject
        {
            Data = new PagedResult<InboxRowDto>
            {
                Items = rows,
                TotalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> MarkSeen(List<string>? keys)
    {
        var (userId, _, _) = Who();
        var rows = await _unitOfWork.SqlQueryAsync<InboxMarkedRow>(MarkSeenSql, userId, (object?)keys?.ToArray() ?? DBNull.Value);
        return new ResponsePost
        {
            Id = rows.FirstOrDefault()?.Marked ?? 0,
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
