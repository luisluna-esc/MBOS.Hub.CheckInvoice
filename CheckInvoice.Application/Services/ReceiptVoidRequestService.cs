using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Dtos.StoredProcedures;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Warehouses;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class ReceiptVoidRequestService : IReceiptVoidRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ReceiptVoidRequestCreateDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public ReceiptVoidRequestService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<ReceiptVoidRequestCreateDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    // EF Core mapea las columnas del resultado de FromSql/SqlQueryRaw por el nombre exacto
    // de la propiedad C# (ej. "ReceiptId"), no por el nombre de columna en snake_case que
    // devuelve la función SQL (ej. receipt_id) — de ahí los alias explícitos.
    private const string GetVoidRequestsSql = """
        SELECT
            receipt_void_request_id AS "ReceiptVoidRequestId",
            receipt_id AS "ReceiptId",
            receipt_date AS "ReceiptDate",
            supplier_name AS "SupplierName",
            warehouse_name AS "WarehouseName",
            receipt_total AS "ReceiptTotal",
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
        FROM sp_get_receipt_void_requests({0}::bigint, {1}::bigint, {2}::varchar, {3}::bigint, {4}::int, {5}::int)
        """;

    public async Task<ResponseGetObject> GetAllVoidRequests(PaginationQueryFilter paginationQueryFilter, ReceiptVoidRequestQueryFilter receiptVoidRequestQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var rows = await _unitOfWork.SqlQueryAsync<ReceiptVoidRequestGetAllRow>(
            GetVoidRequestsSql,
            (object?)receiptVoidRequestQueryFilter.ReceiptVoidRequestId ?? DBNull.Value,
            (object?)receiptVoidRequestQueryFilter.ReceiptId ?? DBNull.Value,
            (object?)receiptVoidRequestQueryFilter.Status ?? DBNull.Value,
            (object?)receiptVoidRequestQueryFilter.RequestedBy ?? DBNull.Value,
            pageNumber,
            pageSize);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        // SqlQueryRaw no pasa por el ValueConverter global de AppDbContext que marca todo
        // DateTime como Utc al leer (ver AppDbContext.OnModelCreating).
        foreach (var row in rows)
        {
            row.ReceiptDate = DateTime.SpecifyKind(row.ReceiptDate, DateTimeKind.Utc);
            row.RequestedAt = DateTime.SpecifyKind(row.RequestedAt, DateTimeKind.Utc);
            if (row.ReviewedAt.HasValue)
            {
                row.ReviewedAt = DateTime.SpecifyKind(row.ReviewedAt.Value, DateTimeKind.Utc);
            }
        }

        return new ResponseGetObject
        {
            Data = new PagedResult<ReceiptVoidRequestDto>
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

    public async Task<ResponsePost> RequestVoid(ReceiptVoidRequestCreateDto receiptVoidRequestCreateDto)
    {
        var validationResult = await _validator.ValidateAsync(receiptVoidRequestCreateDto);

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

        var request = new ReceiptVoidRequest
        {
            ReceiptId = receiptVoidRequestCreateDto.ReceiptId,
            VoidReasonId = receiptVoidRequestCreateDto.VoidReasonId,
            Detail = receiptVoidRequestCreateDto.Detail,
            Status = "pending",
            RequestedBy = _currentUserService.AppUserId!.Value,
            RequestedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<ReceiptVoidRequest>().AddAsync(request);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = request.ReceiptVoidRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Void request submitted successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> ApproveVoidRequest(long id, ReceiptVoidRequestReviewDto receiptVoidRequestReviewDto)
    {
        var repository = _unitOfWork.Repository<ReceiptVoidRequest>();
        var request = await repository.GetByIdAsync(id);

        if (request is null)
        {
            return NotFoundResponse(id);
        }

        if (request.Status != "pending")
        {
            return OnlyPendingResponse(id, request.Status, "approved");
        }

        var receiptRepository = _unitOfWork.Repository<Receipt>();
        var receipt = await receiptRepository.GetByIdAsync(request.ReceiptId);

        if (receipt is null || receipt.IsVoided)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "The receipt no longer exists or is already voided." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        // A diferencia de anular una Salida (que solo devuelve stock, siempre seguro), anular
        // una Entrada descuenta el stock que esa entrada agregó — si parte de ese stock ya se
        // consumió (Salidas/Transferencias posteriores), la anulación dejaría el stock negativo.
        // Se valida todo antes de tocar nada, sin aplicar cambios parciales.
        var stockRepository = _unitOfWork.Repository<Stock>();
        var receiptDetails = await _unitOfWork.Repository<ReceiptDetail>().Query()
            .Where(d => d.ReceiptId == receipt.ReceiptId)
            .ToListAsync();

        var stocksByProduct = new Dictionary<long, Stock>();
        var insufficientProductIds = new List<long>();

        foreach (var detail in receiptDetails)
        {
            var stock = await stockRepository.Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == receipt.WarehouseId && s.ProductId == detail.ProductId);

            if (stock is null || stock.Quantity < detail.Quantity)
            {
                insufficientProductIds.Add(detail.ProductId);
                continue;
            }

            stocksByProduct[detail.ProductId] = stock;
        }

        if (insufficientProductIds.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message
                {
                    Type = MessageType.Error,
                    Description = $"Cannot void this receipt: stock for product(s) {string.Join(", ", insufficientProductIds)} has already been partially or fully used and is insufficient to reverse."
                }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        foreach (var detail in receiptDetails)
        {
            var stock = stocksByProduct[detail.ProductId];
            stock.Quantity -= detail.Quantity;
            stockRepository.Update(stock);
        }

        receipt.IsVoided = true;
        receiptRepository.Update(receipt);

        request.Status = "approved";
        request.ReviewedBy = _currentUserService.AppUserId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNotes = receiptVoidRequestReviewDto.ReviewNotes;
        repository.Update(request);

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = request.ReceiptVoidRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Void request approved. The receipt was voided and its stock reversed." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> RejectVoidRequest(long id, ReceiptVoidRequestReviewDto receiptVoidRequestReviewDto)
    {
        var repository = _unitOfWork.Repository<ReceiptVoidRequest>();
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
        request.ReviewNotes = receiptVoidRequestReviewDto.ReviewNotes;
        repository.Update(request);

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = request.ReceiptVoidRequestId,
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
