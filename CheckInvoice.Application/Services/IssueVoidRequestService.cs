using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Dtos.StoredProcedures;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Entities.Warehouses;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class IssueVoidRequestService : IIssueVoidRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<IssueVoidRequestCreateDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public IssueVoidRequestService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<IssueVoidRequestCreateDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    // EF Core mapea las columnas del resultado de FromSql/SqlQueryRaw por el nombre exacto
    // de la propiedad C# (ej. "IssueId"), no por el nombre de columna en snake_case que
    // devuelve la función SQL (ej. issue_id) — de ahí los alias explícitos.
    private const string GetVoidRequestsSql = """
        SELECT
            issue_void_request_id AS "IssueVoidRequestId",
            issue_id AS "IssueId",
            issue_date AS "IssueDate",
            client_name AS "ClientName",
            warehouse_name AS "WarehouseName",
            issue_total AS "IssueTotal",
            void_reason_id AS "VoidReasonId",
            void_reason_name AS "VoidReasonName",
            detail AS "Detail",
            status AS "Status",
            requested_by AS "RequestedBy",
            requested_by_full_name AS "RequestedByFullName",
            requested_at AS "RequestedAt",
            reviewed_by AS "ReviewedBy",
            reviewed_by_full_name AS "ReviewedByFullName",
            reviewed_at AS "ReviewedAt",
            review_notes AS "ReviewNotes",
            total_records AS "TotalRecords"
        FROM sp_get_issue_void_requests({0}::bigint, {1}::bigint, {2}::varchar, {3}::bigint, {4}::int, {5}::int)
        """;

    public async Task<ResponseGetObject> GetAllVoidRequests(PaginationQueryFilter paginationQueryFilter, IssueVoidRequestQueryFilter issueVoidRequestQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // Piloto de rendimiento: antes esto hacía 9 consultas separadas (página + 7
        // diccionarios: salidas, clientes vía party, nombres de almacén, totales por salida,
        // motivos de anulación, nombres de usuario solicitante/revisor). Ahora es una sola
        // con joins via sp_get_issue_void_requests (Scrips/storedProcedures.sql).
        var rows = await _unitOfWork.SqlQueryAsync<IssueVoidRequestGetAllRow>(
            GetVoidRequestsSql,
            (object?)issueVoidRequestQueryFilter.IssueVoidRequestId ?? DBNull.Value,
            (object?)issueVoidRequestQueryFilter.IssueId ?? DBNull.Value,
            (object?)issueVoidRequestQueryFilter.Status ?? DBNull.Value,
            (object?)issueVoidRequestQueryFilter.RequestedBy ?? DBNull.Value,
            pageNumber,
            pageSize);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        // SqlQueryRaw no pasa por el ValueConverter global de AppDbContext que marca todo
        // DateTime como Utc al leer (ver AppDbContext.OnModelCreating).
        foreach (var row in rows)
        {
            row.IssueDate = DateTime.SpecifyKind(row.IssueDate, DateTimeKind.Utc);
            row.RequestedAt = DateTime.SpecifyKind(row.RequestedAt, DateTimeKind.Utc);
            if (row.ReviewedAt.HasValue)
            {
                row.ReviewedAt = DateTime.SpecifyKind(row.ReviewedAt.Value, DateTimeKind.Utc);
            }
        }

        return new ResponseGetObject
        {
            Data = new PagedResult<IssueVoidRequestDto>
            {
                Items = rows,
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> RequestVoid(IssueVoidRequestCreateDto issueVoidRequestCreateDto)
    {
        var validationResult = await _validator.ValidateAsync(issueVoidRequestCreateDto);

        if (!validationResult.IsValid)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = validationResult.Errors
                    .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
                    .ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var request = new IssueVoidRequest
        {
            IssueId = issueVoidRequestCreateDto.IssueId,
            VoidReasonId = issueVoidRequestCreateDto.VoidReasonId,
            Detail = issueVoidRequestCreateDto.Detail,
            Status = "pending",
            RequestedBy = _currentUserService.AppUserId!.Value,
            RequestedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<IssueVoidRequest>().AddAsync(request);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = request.IssueVoidRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Void request submitted successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> ApproveVoidRequest(long id, IssueVoidRequestReviewDto issueVoidRequestReviewDto)
    {
        var repository = _unitOfWork.Repository<IssueVoidRequest>();
        var request = await repository.GetByIdAsync(id);

        if (request is null)
        {
            return NotFoundResponse(id);
        }

        if (request.Status != "pending")
        {
            return OnlyPendingResponse(id, request.Status, "approved");
        }

        var issueRepository = _unitOfWork.Repository<Issue>();
        var issue = await issueRepository.GetByIdAsync(request.IssueId);

        if (issue is null || issue.IsVoided)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "The issue no longer exists or is already voided." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        // Devuelve el stock exactamente como InsertIssue lo descontó (IssueService.cs), producto por producto.
        var stockRepository = _unitOfWork.Repository<Stock>();
        var issueDetails = await _unitOfWork.Repository<IssueDetail>().Query()
            .Where(d => d.IssueId == issue.IssueId)
            .ToListAsync();

        foreach (var detail in issueDetails)
        {
            var stock = await stockRepository.Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == issue.WarehouseId && s.ProductId == detail.ProductId);

            if (stock is not null)
            {
                stock.Quantity += detail.Quantity;
                stockRepository.Update(stock);
            }
        }

        issue.IsVoided = true;
        issueRepository.Update(issue);

        request.Status = "approved";
        request.ReviewedBy = _currentUserService.AppUserId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNotes = issueVoidRequestReviewDto.ReviewNotes;
        repository.Update(request);

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = request.IssueVoidRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Void request approved. The issue was voided and its stock restored." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> RejectVoidRequest(long id, IssueVoidRequestReviewDto issueVoidRequestReviewDto)
    {
        var repository = _unitOfWork.Repository<IssueVoidRequest>();
        var request = await repository.GetByIdAsync(id);

        if (request is null)
        {
            return NotFoundResponse(id);
        }

        if (request.Status != "pending")
        {
            return OnlyPendingResponse(id, request.Status, "rejected");
        }

        request.Status = "rejected";
        request.ReviewedBy = _currentUserService.AppUserId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNotes = issueVoidRequestReviewDto.ReviewNotes;
        repository.Update(request);

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = request.IssueVoidRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Void request rejected." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static ResponsePost NotFoundResponse(long id) => new()
    {
        Id = id,
        Messages = [new Message { Type = MessageType.Error, Description = "Void request not found." }],
        StatusCode = HttpStatusCode.NotFound
    };

    private static ResponsePost OnlyPendingResponse(long id, string currentStatus, string attemptedAction) => new()
    {
        Id = id,
        Messages = [new Message { Type = MessageType.Error, Description = $"Only pending void requests can be {attemptedAction}. Current status: '{currentStatus}'." }],
        StatusCode = HttpStatusCode.BadRequest
    };
}
