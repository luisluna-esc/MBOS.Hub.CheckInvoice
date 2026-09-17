using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Dtos.StoredProcedures;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Finance;
using CheckInvoice.core.Entities.Governance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.Products;
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

public class IssueService : IIssueService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<IssueRequestDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public IssueService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<IssueRequestDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    // EF Core mapea las columnas del resultado de FromSql/SqlQueryRaw por el nombre exacto
    // de la propiedad C# (ej. "ClientId"), no por el nombre de columna en snake_case que
    // devuelve la función SQL (ej. client_id) — de ahí los alias explícitos.
    private const string GetIssuesSql = """
        SELECT
            issue_id AS "IssueId",
            issue_type_id AS "IssueTypeId",
            warehouse_id AS "WarehouseId",
            warehouse_period_id AS "WarehousePeriodId",
            client_id AS "ClientId",
            complement AS "Complement",
            issue_date AS "IssueDate",
            print_type_id AS "PrintTypeId",
            description AS "Description",
            created_at AS "CreatedAt",
            created_by_id AS "CreatedById",
            created_by_full_name AS "CreatedByFullName",
            has_pending_change_request AS "HasPendingChangeRequest",
            total AS "Total",
            is_voided AS "IsVoided",
            has_pending_void_request AS "HasPendingVoidRequest",
            void_reason_name AS "VoidReasonName",
            void_detail AS "VoidDetail",
            total_records AS "TotalRecords"
        FROM sp_get_issues({0}::bigint, {1}::bigint, {2}::bigint, {3}::bigint, {4}::int, {5}::int)
        """;

    public async Task<ResponseGetObject> GetAllIssues(PaginationQueryFilter paginationQueryFilter, IssueQueryFilter issueQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // Piloto de rendimiento: antes esto hacía 8 consultas separadas (página, conteo,
        // nombre del creador, cambios pendientes, total por salida, anulaciones pendientes,
        // anulaciones relevantes, motivo de anulación). Ahora es una sola consulta con joins
        // via sp_get_issues (Scrips/storedProcedures.sql).
        var rows = await _unitOfWork.SqlQueryAsync<IssueGetAllRow>(
            GetIssuesSql,
            (object?)issueQueryFilter.IssueId ?? DBNull.Value,
            (object?)issueQueryFilter.ClientId ?? DBNull.Value,
            (object?)issueQueryFilter.WarehouseId ?? DBNull.Value,
            (object?)issueQueryFilter.IssueTypeId ?? DBNull.Value,
            pageNumber,
            pageSize);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        // SqlQueryRaw no pasa por el ValueConverter global de AppDbContext que marca todo
        // DateTime como Utc al leer (ver AppDbContext.OnModelCreating) — sin esto, IssueDate/
        // CreatedAt llegan con Kind=Unspecified y el JSON pierde la "Z", corriendo la fecha
        // mostrada en el frontend según la zona horaria del navegador.
        foreach (var row in rows)
        {
            row.IssueDate = DateTime.SpecifyKind(row.IssueDate, DateTimeKind.Utc);
            row.CreatedAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc);
        }

        return new ResponseGetObject
        {
            Data = new PagedResult<IssueDto>
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

    public async Task<ResponseGetObject> GetIssueDetails(long issueId)
    {
        var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId);
        if (issue is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Issue not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var details = await _unitOfWork.Repository<IssueDetail>().Query()
            .Where(d => d.IssueId == issueId)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = details.Select(d => new IssueDetailDto
            {
                IssueDetailId = d.IssueDetailId,
                IssueId = d.IssueId,
                ProductId = d.ProductId,
                Quantity = d.Quantity,
                UnitCost = d.UnitCost,
                TotalCost = d.TotalCost
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertIssue(IssueRequestDto issueRequestDto)
    {
        var validationResult = await _validator.ValidateAsync(issueRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (!await _unitOfWork.Repository<Warehouse>().Query().AnyAsync(w => w.WarehouseId == issueRequestDto.WarehouseId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "WarehouseId does not reference an existing warehouse." });
        }

        if (issueRequestDto.IssueTypeId.HasValue &&
            !await _unitOfWork.Repository<IssueType>().Query().AnyAsync(i => i.IssueTypeId == issueRequestDto.IssueTypeId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "IssueTypeId does not reference an existing issue type." });
        }

        if (issueRequestDto.WarehousePeriodId.HasValue)
        {
            var warehousePeriod = await _unitOfWork.Repository<WarehousePeriod>().Query()
                .FirstOrDefaultAsync(w => w.WarehousePeriodId == issueRequestDto.WarehousePeriodId.Value);

            if (warehousePeriod is null)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "WarehousePeriodId does not reference an existing warehouse period." });
            }
            else if (warehousePeriod.IsClosed)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "This warehouse period is closed and cannot receive new movements." });
            }
        }

        if (issueRequestDto.ClientId.HasValue &&
            !await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.ClientId == issueRequestDto.ClientId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ClientId does not reference an existing client." });
        }

        if (issueRequestDto.PrintTypeId.HasValue &&
            !await _unitOfWork.Repository<PrintType>().Query().AnyAsync(p => p.PrintTypeId == issueRequestDto.PrintTypeId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "PrintTypeId does not reference an existing print type." });
        }

        foreach (var line in issueRequestDto.Details)
        {
            if (!await _unitOfWork.Repository<Product>().Query().AnyAsync(p => p.ProductId == line.ProductId))
            {
                errors.Add(new Message { Type = MessageType.Error, Description = $"ProductId {line.ProductId} does not reference an existing product." });
            }
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var requestedByProduct = issueRequestDto.Details
            .GroupBy(d => d.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));

        var stockByProduct = new Dictionary<long, Stock>();
        foreach (var productId in requestedByProduct.Keys)
        {
            var stock = await _unitOfWork.Repository<Stock>().Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == issueRequestDto.WarehouseId && s.ProductId == productId);

            var available = stock?.Quantity ?? 0;
            var requested = requestedByProduct[productId];

            if (available < requested)
            {
                errors.Add(new Message
                {
                    Type = MessageType.Error,
                    Description = $"Insufficient stock for ProductId {productId}: {available} available, {requested} requested."
                });
                continue;
            }

            stockByProduct[productId] = stock!;
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var issue = new Issue
        {
            IssueTypeId = issueRequestDto.IssueTypeId,
            WarehouseId = issueRequestDto.WarehouseId,
            WarehousePeriodId = issueRequestDto.WarehousePeriodId,
            ClientId = issueRequestDto.ClientId,
            Complement = issueRequestDto.Complement,
            IssueDate = issueRequestDto.IssueDate ?? DateTime.UtcNow,
            PrintTypeId = issueRequestDto.PrintTypeId,
            Description = issueRequestDto.Description,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserService.AppUserId
        };

        await _unitOfWork.Repository<Issue>().AddAsync(issue);
        await _unitOfWork.SaveChangesAsync();

        var stockRepository = _unitOfWork.Repository<Stock>();

        foreach (var line in issueRequestDto.Details)
        {
            var stock = stockByProduct[line.ProductId];
            var unitCost = stock.AverageCost;

            await _unitOfWork.Repository<IssueDetail>().AddAsync(new IssueDetail
            {
                IssueId = issue.IssueId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                TotalCost = line.Quantity * unitCost
            });

            stock.Quantity -= line.Quantity;
        }

        foreach (var stock in stockByProduct.Values)
        {
            stockRepository.Update(stock);
        }

        await _unitOfWork.SaveChangesAsync();

        if (issueRequestDto.SendToAccountsReceivable)
        {
            var issueDetails = await _unitOfWork.Repository<IssueDetail>().Query()
                .Where(d => d.IssueId == issue.IssueId)
                .ToListAsync();

            var totalAmount = issueDetails.Sum(d => d.TotalCost);

            var accountReceivable = new AccountReceivable
            {
                IssueId = issue.IssueId,
                ClientId = issueRequestDto.ClientId,
                TotalAmount = totalAmount,
                OutstandingBalance = totalAmount,
                PaymentType = issueRequestDto.PaymentType!,
                PaymentDetail = issueRequestDto.PaymentDetail,
                DueDate = issueRequestDto.DueDate,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                CreatedById = _currentUserService.AppUserId
            };

            await _unitOfWork.Repository<AccountReceivable>().AddAsync(accountReceivable);
            await _unitOfWork.SaveChangesAsync();
        }

        return new ResponsePost
        {
            Id = issue.IssueId,
            Messages = [new Message { Type = MessageType.Success, Description = "Issue created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

}