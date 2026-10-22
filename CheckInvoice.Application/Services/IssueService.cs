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

public partial class IssueService : IIssueService
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
            NULL::varchar AS "NoteNumber",
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
            NULL::varchar AS "ReviewStatus",
            NULL::bigint AS "IssueBatchId",
            total_records AS "TotalRecords"
        FROM sp_get_issues({0}::bigint, {1}::bigint, {2}::bigint, {3}::bigint, {4}::int, {5}::int, {6}::varchar, {7}::varchar)
        """;

    public async Task<ResponseGetObject> GetAllIssues(PaginationQueryFilter paginationQueryFilter, IssueQueryFilter issueQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // Piloto de rendimiento: antes esto hacía 8 consultas separadas (página, conteo,
        // nombre del creador, cambios pendientes, total por salida, anulaciones pendientes,
        // anulaciones relevantes, motivo de anulación). Ahora es una sola consulta con joins
        // via sp_get_issues (Scrips/storedProcedures.sql).
        if (!string.IsNullOrWhiteSpace(issueQueryFilter.NoteNumber))
        {
            issueQueryFilter.IssueId = await FindByNoteNumberAsync(issueQueryFilter.NoteNumber) ?? -1;
        }

        var rows = await _unitOfWork.SqlQueryAsync<IssueGetAllRow>(
            GetIssuesSql,
            (object?)issueQueryFilter.IssueId ?? DBNull.Value,
            (object?)issueQueryFilter.ClientId ?? DBNull.Value,
            (object?)issueQueryFilter.WarehouseId ?? DBNull.Value,
            (object?)issueQueryFilter.IssueTypeId ?? DBNull.Value,
            pageNumber,
            pageSize,
            (object?)issueQueryFilter.ClientName ?? DBNull.Value,
            ReviewStates.ForSql(issueQueryFilter.ReviewState));

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

        var pageIds = rows.Select(r => r.IssueId).ToList();
        var extras = await _unitOfWork.Repository<Issue>().Query()
            .Where(i => pageIds.Contains(i.IssueId))
            .Select(i => new { i.IssueId, i.ReviewStatus, i.MonthlyNumber, i.IssueBatchId })
            .ToDictionaryAsync(i => i.IssueId);
        var pendingEdits = await _unitOfWork.Repository<ChangeRequest>().Query()
            .Where(c => c.TableName == IssueEditService.TableName && c.Status == "pending" && pageIds.Contains(c.RecordId))
            .Select(c => c.RecordId)
            .ToListAsync();
        foreach (var row in rows)
        {
            var extra = extras.GetValueOrDefault(row.IssueId);
            row.ReviewStatus = extra?.ReviewStatus;
            row.IssueBatchId = extra?.IssueBatchId;
            row.NoteNumber = IssueNumbering.Format(extra?.MonthlyNumber, row.IssueDate, row.IssueId);
            row.HasPendingChangeRequest = row.HasPendingChangeRequest || pendingEdits.Contains(row.IssueId);
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

    private async Task<long?> FindByNoteNumberAsync(string noteNumber)
    {
        if (!IssueNumbering.TryParse(noteNumber, out var number, out var month, out var year))
        {
            // Un número sin mes se toma como el N° interno de la salida.
            return long.TryParse(noteNumber.Trim(), out var issueId) ? issueId : null;
        }

        var query = _unitOfWork.Repository<Issue>().Query()
            .Where(i => i.MonthlyNumber == number && i.IssueDate.Month == month);
        if (year.HasValue)
        {
            query = query.Where(i => i.IssueDate.Year == year.Value);
        }

        return await query
            .OrderByDescending(i => i.IssueDate)
            .Select(i => (long?)i.IssueId)
            .FirstOrDefaultAsync();
    }

    public async Task<ResponseGetObject> GetIssueSummary(long issueId)
    {
        var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId);
        if (issue is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró la salida." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        string? clientName = null;
        if (issue.ClientId.HasValue)
        {
            clientName = await (
                from c in _unitOfWork.Repository<Client>().Query()
                join p in _unitOfWork.Repository<Party>().Query() on c.PartyId equals p.PartyId
                where c.ClientId == issue.ClientId.Value
                select p.Name).FirstOrDefaultAsync();
        }
        var issueTypeName = issue.IssueTypeId.HasValue
            ? (await _unitOfWork.Repository<IssueType>().GetByIdAsync(issue.IssueTypeId.Value))?.Name
            : null;
        var warehouseName = issue.WarehouseId.HasValue
            ? (await _unitOfWork.Repository<Warehouse>().GetByIdAsync(issue.WarehouseId.Value))?.Name
            : null;
        var creator = issue.CreatedById.HasValue
            ? await _unitOfWork.Repository<AppUser>().GetByIdAsync(issue.CreatedById.Value)
            : null;

        var lines = await (
            from d in _unitOfWork.Repository<IssueDetail>().Query()
            join p in _unitOfWork.Repository<Product>().Query() on d.ProductId equals p.ProductId
            join m in _unitOfWork.Repository<MediaType>().Query() on p.MediaTypeId equals m.MediaTypeId into media
            from m in media.DefaultIfEmpty()
            join i in _unitOfWork.Repository<ProductImage>().Query() on p.ProductId equals i.ProductId into images
            from i in images.DefaultIfEmpty()
            where d.IssueId == issueId
            orderby d.IssueDetailId
            select new ReceiptSummaryLineDto
            {
                ProductId = p.ProductId,
                Code = p.Code,
                Name = p.Name,
                UnitMeasure = m != null ? m.Name : null,
                Quantity = d.Quantity,
                UnitPrice = d.UnitCost,
                UnitCost = d.UnitCost,
                TotalCost = d.TotalCost,
                ImageVersion = i != null ? i.UpdatedAt.Ticks : null
            }).ToListAsync();

        return new ResponseGetObject
        {
            Data = new IssueSummaryDto
            {
                IssueId = issue.IssueId,
                NoteNumber = IssueNumbering.Format(issue.MonthlyNumber, issue.IssueDate, issue.IssueId),
                IssueTypeId = issue.IssueTypeId,
                IssueTypeName = issueTypeName,
                ClientId = issue.ClientId,
                ClientName = clientName,
                WarehouseId = issue.WarehouseId,
                WarehouseName = warehouseName,
                IssueDate = issue.IssueDate,
                Description = issue.Description,
                IsVoided = issue.IsVoided,
                CreatedByName = creator is null ? null : $"{creator.FirstName} {creator.LastName}".Trim(),
                CreatedAt = issue.CreatedAt,
                Lines = lines
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
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró la salida." }],
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
            errors.Add(new Message { Type = MessageType.Error, Description = "El almacén seleccionado no existe." });
        }

        if (issueRequestDto.IssueTypeId.HasValue &&
            !await _unitOfWork.Repository<IssueType>().Query().AnyAsync(i => i.IssueTypeId == issueRequestDto.IssueTypeId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "El tipo de salida seleccionado no existe." });
        }

        // Como en Entradas: no se elige el período; la fecha de la salida es del mes anterior o del
        // actual, hasta hoy, y el período es el de ese mes.
        var issueDate = issueRequestDto.IssueDate?.Date ?? BoliviaTime.Today;
        WarehousePeriod? warehousePeriod = null;
        if (!MovementDates.IsWithinEntryWindow(issueDate))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = MovementDates.EntryWindowMessage("fecha de la salida") });
        }
        else
        {
            warehousePeriod = await MovementDates.GetOrCreatePeriodAsync(_unitOfWork, issueDate);
            if (warehousePeriod.IsClosed)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = $"El período {warehousePeriod.Name} está cerrado y ya no admite nuevos movimientos." });
            }
        }

        if (issueRequestDto.ClientId.HasValue &&
            !await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.ClientId == issueRequestDto.ClientId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "El cliente seleccionado no existe." });
        }

        if (issueRequestDto.PrintTypeId.HasValue &&
            !await _unitOfWork.Repository<PrintType>().Query().AnyAsync(p => p.PrintTypeId == issueRequestDto.PrintTypeId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "El tipo de impresión seleccionado no existe." });
        }

        foreach (var line in issueRequestDto.Details)
        {
            if (!await _unitOfWork.Repository<Product>().Query().AnyAsync(p => p.ProductId == line.ProductId))
            {
                errors.Add(new Message { Type = MessageType.Error, Description = $"El producto con ID {line.ProductId} no existe." });
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

            var reserved = await PendingReceiptVoidStock.GetReservedQuantityAsync(_unitOfWork, issueRequestDto.WarehouseId, productId);
            var available = Math.Max((stock?.Quantity ?? 0) - reserved, 0);
            var requested = requestedByProduct[productId];

            if (available < requested)
            {
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(productId);
                errors.Add(new Message
                {
                    Type = MessageType.Error,
                    Description = reserved > 0
                        ? $"Stock insuficiente de \"{product?.Name ?? $"#{productId}"}\": disponible {available}, solicitado {requested}. Hay {reserved} unidades apartadas por una anulación o corrección de entrada pendiente de aprobación."
                        : $"Stock insuficiente de \"{product?.Name ?? $"#{productId}"}\": disponible {available}, solicitado {requested}."
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
            WarehousePeriodId = warehousePeriod!.WarehousePeriodId,
            ClientId = issueRequestDto.ClientId,
            Complement = issueRequestDto.Complement,
            IssueDate = issueDate,
            PrintTypeId = issueRequestDto.PrintTypeId,
            Description = issueRequestDto.Description,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserService.AppUserId
        };

        // Se guarda con el siguiente N° de salida de su mes.
        await IssueNumbering.Lock.WaitAsync();
        try
        {
            issue.MonthlyNumber = await IssueNumbering.NextAsync(_unitOfWork, issue.IssueDate);
            await _unitOfWork.Repository<Issue>().AddAsync(issue);
            await _unitOfWork.SaveChangesAsync();
        }
        finally
        {
            IssueNumbering.Lock.Release();
        }
        await DocumentReviews.RecordEnteredAsync(_unitOfWork, DocumentReviews.Issue, issue.IssueId, issue.CreatedById);

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
            Messages = [new Message { Type = MessageType.Success, Description = "Salida registrada correctamente." }],
            StatusCode = HttpStatusCode.Created
        };
    }

}