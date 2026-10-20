using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Dtos.StoredProcedures;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
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

public partial class ReceiptService : IReceiptService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ReceiptRequestDto> _validator;
    private readonly IValidator<ReceiptReturnRequestDto> _returnValidator;
    private readonly ICurrentUserService _currentUserService;

    public ReceiptService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<ReceiptRequestDto> validator,
        IValidator<ReceiptReturnRequestDto> returnValidator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _returnValidator = returnValidator;
        _currentUserService = currentUserService;
    }

    // EF Core mapea las columnas del resultado de FromSql/SqlQueryRaw por el nombre exacto
    // de la propiedad C# (ej. "SupplierId"), no por el nombre de columna en snake_case que
    // devuelve la función SQL (ej. supplier_id) — de ahí los alias explícitos.
    private const string GetReceiptsSql = """
        SELECT
            receipt_id AS "ReceiptId",
            supplier_id AS "SupplierId",
            tax_id AS "TaxId",
            warehouse_id AS "WarehouseId",
            warehouse_period_id AS "WarehousePeriodId",
            receipt_type_id AS "ReceiptTypeId",
            invoice_number AS "InvoiceNumber",
            description AS "Description",
            issue_date AS "IssueDate",
            invoice_total AS "InvoiceTotal",
            created_at AS "CreatedAt",
            created_by_id AS "CreatedById",
            created_by_full_name AS "CreatedByFullName",
            has_pending_change_request AS "HasPendingChangeRequest",
            is_voided AS "IsVoided",
            has_pending_void_request AS "HasPendingVoidRequest",
            void_reason_name AS "VoidReasonName",
            void_detail AS "VoidDetail",
            related_issue_id AS "RelatedIssueId",
            NULL::varchar AS "NoteNumber",
            NULL::varchar AS "ReviewStatus",
            total_records AS "TotalRecords"
        FROM sp_get_receipts({0}::bigint, {1}::bigint, {2}::bigint, {3}::bigint, {4}::varchar, {5}::int, {6}::int, {7}::varchar)
        """;

    public async Task<ResponseGetObject> GetAllReceipts(PaginationQueryFilter paginationQueryFilter, ReceiptQueryFilter receiptQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // El N° de nota ("15-01") se busca aquí y se pasa como ReceiptId al procedimiento.
        if (!string.IsNullOrWhiteSpace(receiptQueryFilter.NoteNumber))
        {
            receiptQueryFilter.ReceiptId = await FindByNoteNumberAsync(receiptQueryFilter.NoteNumber) ?? -1;
        }

        // Piloto de rendimiento: antes esto hacía 3 consultas separadas (página, nombre del
        // creador, cambios pendientes). Ahora es una sola con LEFT JOIN via sp_get_receipts
        // (Scrips/storedProcedures.sql).
        var rows = await _unitOfWork.SqlQueryAsync<ReceiptGetAllRow>(
            GetReceiptsSql,
            (object?)receiptQueryFilter.ReceiptId ?? DBNull.Value,
            (object?)receiptQueryFilter.SupplierId ?? DBNull.Value,
            (object?)receiptQueryFilter.WarehouseId ?? DBNull.Value,
            (object?)receiptQueryFilter.ReceiptTypeId ?? DBNull.Value,
            (object?)receiptQueryFilter.InvoiceNumber ?? DBNull.Value,
            pageNumber,
            pageSize,
            (object?)receiptQueryFilter.SupplierName ?? DBNull.Value);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        // SqlQueryRaw no pasa por el ValueConverter global de AppDbContext que marca todo
        // DateTime como Utc al leer (ver AppDbContext.OnModelCreating).
        foreach (var row in rows)
        {
            row.IssueDate = DateTime.SpecifyKind(row.IssueDate, DateTimeKind.Utc);
            row.CreatedAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc);
        }

        var pageIds = rows.Select(r => r.ReceiptId).ToList();
        var monthlyNumbers = await _unitOfWork.Repository<Receipt>().Query()
            .Where(r => pageIds.Contains(r.ReceiptId))
            .Select(r => new { r.ReceiptId, r.MonthlyNumber, r.ReviewStatus })
            .ToDictionaryAsync(r => r.ReceiptId);
        var pendingEdits = await _unitOfWork.Repository<ChangeRequest>().Query()
            .Where(c => c.TableName == ReceiptEditService.TableName && c.Status == "pending" && pageIds.Contains(c.RecordId))
            .Select(c => c.RecordId)
            .ToListAsync();
        foreach (var row in rows)
        {
            row.HasPendingChangeRequest = row.HasPendingChangeRequest || pendingEdits.Contains(row.ReceiptId);
            var extra = monthlyNumbers.GetValueOrDefault(row.ReceiptId);
            row.NoteNumber = ReceiptNumbering.Format(extra?.MonthlyNumber, row.IssueDate, row.ReceiptId);
            row.ReviewStatus = extra?.ReviewStatus;
        }

        return new ResponseGetObject
        {
            Data = new PagedResult<ReceiptDto>
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
        if (!ReceiptNumbering.TryParse(noteNumber, out var number, out var month, out var year))
        {
            // Un número sin mes se toma como el N° interno de la entrada.
            return long.TryParse(noteNumber.Trim(), out var receiptId) ? receiptId : null;
        }

        var query = _unitOfWork.Repository<Receipt>().Query()
            .Where(r => r.MonthlyNumber == number && r.IssueDate.Month == month);
        if (year.HasValue)
        {
            query = query.Where(r => r.IssueDate.Year == year.Value);
        }

        return await query
            .OrderByDescending(r => r.IssueDate)
            .Select(r => (long?)r.ReceiptId)
            .FirstOrDefaultAsync();
    }

    // Guarda la entrada con el siguiente N° de nota de su mes.
    private async Task SaveNumberedAsync(Receipt receipt)
    {
        await ReceiptNumbering.Lock.WaitAsync();
        try
        {
            receipt.MonthlyNumber = await ReceiptNumbering.NextAsync(_unitOfWork, receipt.IssueDate);
            await _unitOfWork.Repository<Receipt>().AddAsync(receipt);
            await _unitOfWork.SaveChangesAsync();
        }
        finally
        {
            ReceiptNumbering.Lock.Release();
        }

        await DocumentReviews.RecordEnteredAsync(_unitOfWork, DocumentReviews.Receipt, receipt.ReceiptId, receipt.CreatedById);
    }

    // Todo lo de una entrada en una sola respuesta: la usa la página que abre el QR de la nota.
    public async Task<ResponseGetObject> GetReceiptSummary(long receiptId)
    {
        var receipt = await _unitOfWork.Repository<Receipt>().GetByIdAsync(receiptId);
        if (receipt is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró la entrada." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        string? supplierName = null;
        if (receipt.SupplierId.HasValue)
        {
            supplierName = await (
                from s in _unitOfWork.Repository<Supplier>().Query()
                join p in _unitOfWork.Repository<Party>().Query() on s.PartyId equals p.PartyId
                where s.SupplierId == receipt.SupplierId.Value
                select s.LegalName ?? p.Name).FirstOrDefaultAsync();
        }

        var receiptTypeName = receipt.ReceiptTypeId.HasValue
            ? (await _unitOfWork.Repository<ReceiptType>().GetByIdAsync(receipt.ReceiptTypeId.Value))?.Name
            : null;
        var warehouseName = receipt.WarehouseId.HasValue
            ? (await _unitOfWork.Repository<Warehouse>().GetByIdAsync(receipt.WarehouseId.Value))?.Name
            : null;
        var creator = receipt.CreatedById.HasValue
            ? await _unitOfWork.Repository<AppUser>().GetByIdAsync(receipt.CreatedById.Value)
            : null;

        var lines = await (
            from d in _unitOfWork.Repository<ReceiptDetail>().Query()
            join p in _unitOfWork.Repository<Product>().Query() on d.ProductId equals p.ProductId
            join m in _unitOfWork.Repository<MediaType>().Query() on p.MediaTypeId equals m.MediaTypeId into media
            from m in media.DefaultIfEmpty()
            join i in _unitOfWork.Repository<ProductImage>().Query() on p.ProductId equals i.ProductId into images
            from i in images.DefaultIfEmpty()
            where d.ReceiptId == receiptId
            orderby d.ReceiptDetailId
            select new ReceiptSummaryLineDto
            {
                ProductId = p.ProductId,
                Code = p.Code,
                Name = p.Name,
                UnitMeasure = m != null ? m.Name : null,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice ?? d.UnitCost,
                UnitCost = d.UnitCost,
                TotalCost = d.TotalCost,
                ImageVersion = i != null ? i.UpdatedAt.Ticks : null
            }).ToListAsync();

        return new ResponseGetObject
        {
            Data = new ReceiptSummaryDto
            {
                ReceiptId = receipt.ReceiptId,
                NoteNumber = ReceiptNumbering.Format(receipt.MonthlyNumber, receipt.IssueDate, receipt.ReceiptId),
                ReceiptTypeId = receipt.ReceiptTypeId,
                SupplierId = receipt.SupplierId,
                WarehouseId = receipt.WarehouseId,
                RelatedIssueId = receipt.RelatedIssueId,
                IssueDate = receipt.IssueDate,
                PurchaseDate = receipt.PurchaseDate,
                ReceiptTypeName = receiptTypeName,
                WarehouseName = warehouseName,
                SupplierName = supplierName,
                TaxId = receipt.TaxId,
                InvoiceNumber = receipt.InvoiceNumber,
                Description = receipt.Description,
                InvoiceTotal = receipt.InvoiceTotal,
                IsVoided = receipt.IsVoided,
                CreatedByName = creator is null ? null : $"{creator.FirstName} {creator.LastName}".Trim(),
                CreatedAt = receipt.CreatedAt,
                Lines = lines
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetReceiptDetails(long receiptId)
    {
        var receipt = await _unitOfWork.Repository<Receipt>().GetByIdAsync(receiptId);
        if (receipt is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró la entrada." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var details = await _unitOfWork.Repository<ReceiptDetail>().Query()
            .Where(d => d.ReceiptId == receiptId)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = details.Select(d => new ReceiptDetailDto
            {
                ReceiptDetailId = d.ReceiptDetailId,
                ReceiptId = d.ReceiptId,
                ProductId = d.ProductId,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                UnitCost = d.UnitCost,
                TotalCost = d.TotalCost,
                WorkOrder = d.WorkOrder,
                Detail = d.Detail
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertReceipt(ReceiptRequestDto receiptRequestDto)
    {
        var validationResult = await _validator.ValidateAsync(receiptRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (!await _unitOfWork.Repository<Warehouse>().Query().AnyAsync(w => w.WarehouseId == receiptRequestDto.WarehouseId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "El almacén seleccionado no existe." });
        }

        if (receiptRequestDto.SupplierId.HasValue &&
            !await _unitOfWork.Repository<Supplier>().Query().AnyAsync(s => s.SupplierId == receiptRequestDto.SupplierId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "El proveedor seleccionado no existe." });
        }

        // Compras (con factura o con retención) y, sin proveedor, Ajuste e Inventario Inicial.
        // Las devoluciones van por InsertReturn.
        ReceiptType? receiptType = null;
        if (receiptRequestDto.ReceiptTypeId.HasValue)
        {
            receiptType = await _unitOfWork.Repository<ReceiptType>().GetByIdAsync(receiptRequestDto.ReceiptTypeId.Value);
        }

        var isPurchase = ReceiptCosting.IsPurchaseType(receiptType?.Name);
        if (!isPurchase && !ReceiptCosting.IsDirectType(receiptType?.Name))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "Elige si la compra es con factura o con retención." });
        }

        var withRetention = receiptType?.Name == ReceiptCosting.PurchaseWithRetention;

        if (receiptType?.Name == ReceiptCosting.Donation && !receiptRequestDto.SupplierId.HasValue)
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "Elige quién dona el material." });
        }

        if (isPurchase)
        {
            if (string.IsNullOrWhiteSpace(receiptRequestDto.TaxId))
            {
                errors.Add(new Message { Type = MessageType.Error, Description = withRetention ? "El CI es obligatorio." : "El NIT es obligatorio." });
            }

            if (!withRetention && string.IsNullOrWhiteSpace(receiptRequestDto.InvoiceNumber))
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "El número de factura es obligatorio." });
            }

            if (receiptRequestDto.PurchaseDate is null)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "La fecha de compra es obligatoria." });
            }
            else if (receiptRequestDto.PurchaseDate.Value.Date > BoliviaTime.Today)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "La fecha de compra no puede ser posterior a hoy." });
            }

            if (receiptRequestDto.IssueDate is null)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "La fecha de ingreso es obligatoria." });
            }

            var purchaseTotal = receiptRequestDto.Details.Sum(d => d.Quantity * d.UnitPrice);
            if (receiptRequestDto.InvoiceTotal is null || Math.Abs(purchaseTotal - receiptRequestDto.InvoiceTotal.Value) > 0.01m)
            {
                errors.Add(new Message
                {
                    Type = MessageType.Error,
                    Description = $"La suma de los productos ({purchaseTotal:N2}) no coincide con el valor total de la compra ({receiptRequestDto.InvoiceTotal ?? 0:N2})."
                });
            }
        }

        // Fecha de ingreso: la escrita en la compra o la donación; en un ajuste, hoy.
        var entryDate = receiptRequestDto.IssueDate?.Date ?? BoliviaTime.Today;
        WarehousePeriod? warehousePeriod = null;
        if (!MovementDates.IsWithinEntryWindow(entryDate))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = MovementDates.EntryWindowMessage("fecha de ingreso") });
        }
        else
        {
            warehousePeriod = await MovementDates.GetOrCreatePeriodAsync(_unitOfWork, entryDate);
            if (warehousePeriod.IsClosed)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = $"El período {warehousePeriod.Name} está cerrado y ya no admite nuevos movimientos." });
            }
        }

        foreach (var line in receiptRequestDto.Details)
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

        var receipt = new Receipt
        {
            SupplierId = receiptRequestDto.SupplierId,
            TaxId = receiptRequestDto.TaxId,
            WarehouseId = receiptRequestDto.WarehouseId,
            WarehousePeriodId = warehousePeriod!.WarehousePeriodId,
            ReceiptTypeId = receiptRequestDto.ReceiptTypeId,
            InvoiceNumber = string.IsNullOrWhiteSpace(receiptRequestDto.InvoiceNumber) ? null : receiptRequestDto.InvoiceNumber.Trim(),
            Description = receiptRequestDto.Description,
            IssueDate = entryDate,
            PurchaseDate = isPurchase ? receiptRequestDto.PurchaseDate!.Value.Date : null,
            InvoiceTotal = receiptRequestDto.InvoiceTotal,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserService.AppUserId
        };

        await SaveNumberedAsync(receipt);

        var stockCache = new Dictionary<long, Stock>();

        foreach (var line in receiptRequestDto.Details)
        {
            var unitCost = ReceiptCosting.UnitCost(line.UnitPrice, withRetention);

            await _unitOfWork.Repository<ReceiptDetail>().AddAsync(new ReceiptDetail
            {
                ReceiptId = receipt.ReceiptId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                UnitCost = unitCost,
                TotalCost = ReceiptCosting.TotalCost(line.Quantity, line.UnitPrice, withRetention)
            });

            await IncreaseStock(stockCache, receiptRequestDto.WarehouseId, line.ProductId, line.Quantity, unitCost);
        }

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = receipt.ReceiptId,
            Messages = [new Message { Type = MessageType.Success, Description = "Entrada registrada correctamente." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponseGetObject> GetReturnableIssueLines(long issueId)
    {
        var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId);
        if (issue is null || issue.IsVoided)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró la salida, o ya fue anulada." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var issueDetails = await _unitOfWork.Repository<IssueDetail>().Query()
            .Where(d => d.IssueId == issueId)
            .ToListAsync();

        var alreadyReturnedByProduct = await GetAlreadyReturnedByProduct(issueId);

        return new ResponseGetObject
        {
            Data = issueDetails.Select(d =>
            {
                var alreadyReturned = alreadyReturnedByProduct.GetValueOrDefault(d.ProductId, 0m);
                return new ReturnableIssueLineDto
                {
                    ProductId = d.ProductId,
                    QuantityIssued = d.Quantity,
                    QuantityAlreadyReturned = alreadyReturned,
                    QuantityReturnable = d.Quantity - alreadyReturned,
                    UnitCost = d.UnitCost
                };
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertReturn(ReceiptReturnRequestDto receiptReturnRequestDto)
    {
        var validationResult = await _returnValidator.ValidateAsync(receiptReturnRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(receiptReturnRequestDto.IssueId);
        if (issue is null || issue.IsVoided)
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "La salida indicada no existe o ya fue anulada." });
            return new ResponsePost { Id = 0, Messages = errors.ToArray(), StatusCode = HttpStatusCode.BadRequest };
        }

        if (issue.WarehouseId is null)
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "La salida original no tiene un almacén asignado." });
            return new ResponsePost { Id = 0, Messages = errors.ToArray(), StatusCode = HttpStatusCode.BadRequest };
        }

        if (!await _unitOfWork.Repository<ReceiptType>().Query().AnyAsync(r => r.ReceiptTypeId == receiptReturnRequestDto.ReceiptTypeId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "El tipo de entrada seleccionado no existe." });
        }

        // La devolución también lleva su fecha, con la misma ventana que una compra.
        var returnDate = receiptReturnRequestDto.ReturnDate?.Date ?? BoliviaTime.Today;
        WarehousePeriod? returnPeriod = null;
        if (!MovementDates.IsWithinEntryWindow(returnDate))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = MovementDates.EntryWindowMessage("fecha de devolución") });
        }
        else
        {
            returnPeriod = await MovementDates.GetOrCreatePeriodAsync(_unitOfWork, returnDate);
            if (returnPeriod.IsClosed)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = $"El período {returnPeriod.Name} está cerrado y ya no admite nuevos movimientos." });
            }
        }

        var issueDetails = await _unitOfWork.Repository<IssueDetail>().Query()
            .Where(d => d.IssueId == issue.IssueId)
            .ToListAsync();

        // Agrupado por si la Salida original tuviera más de una línea para el mismo
        // producto — evita una excepción de clave duplicada al construir el diccionario.
        var issueDetailsByProduct = issueDetails
            .GroupBy(d => d.ProductId)
            .ToDictionary(g => g.Key, g => (Quantity: g.Sum(d => d.Quantity), UnitCost: g.First().UnitCost));

        var alreadyReturnedByProduct = await GetAlreadyReturnedByProduct(issue.IssueId);

        foreach (var line in receiptReturnRequestDto.Lines)
        {
            if (!issueDetailsByProduct.TryGetValue(line.ProductId, out var issueDetail))
            {
                errors.Add(new Message { Type = MessageType.Error, Description = $"El producto con ID {line.ProductId} no formaba parte de la salida original." });
                continue;
            }

            var alreadyReturned = alreadyReturnedByProduct.GetValueOrDefault(line.ProductId, 0m);
            var returnable = issueDetail.Quantity - alreadyReturned;

            if (line.Quantity > returnable)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = $"El producto con ID {line.ProductId}: no se puede devolver {line.Quantity}, solo quedan {returnable} disponibles para devolver." });
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

        var warehouseId = issue.WarehouseId.Value;

        var receipt = new Receipt
        {
            WarehouseId = warehouseId,
            WarehousePeriodId = returnPeriod!.WarehousePeriodId,
            ReceiptTypeId = receiptReturnRequestDto.ReceiptTypeId,
            Description = receiptReturnRequestDto.Description,
            IssueDate = returnDate,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserService.AppUserId,
            RelatedIssueId = issue.IssueId
        };

        await SaveNumberedAsync(receipt);

        var stockCache = new Dictionary<long, Stock>();

        foreach (var line in receiptReturnRequestDto.Lines)
        {
            var unitCost = issueDetailsByProduct[line.ProductId].UnitCost;
            var totalCost = line.Quantity * unitCost;

            await _unitOfWork.Repository<ReceiptDetail>().AddAsync(new ReceiptDetail
            {
                ReceiptId = receipt.ReceiptId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = unitCost,
                UnitCost = unitCost,
                TotalCost = totalCost
            });

            await IncreaseStock(stockCache, warehouseId, line.ProductId, line.Quantity, unitCost);
        }

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = receipt.ReceiptId,
            Messages = [new Message { Type = MessageType.Success, Description = "Devolución registrada correctamente." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    // Cuánto ya se ha devuelto de esta Salida por producto, sumando todas las Entradas de
    // tipo Devolución (RelatedIssueId == issueId) que no estén anuladas — usado tanto para
    // mostrar lo devolvible como para impedir devolver más de lo que salió. Sin propiedades
    // de navegación en este modelo (ver ReceiptDetail.cs/ReceiptConfiguration.cs), el cruce
    // Receipt->ReceiptDetail se hace por ReceiptId, no por join de EF.
    private async Task<Dictionary<long, decimal>> GetAlreadyReturnedByProduct(long issueId)
    {
        var returnReceiptIds = await _unitOfWork.Repository<Receipt>().Query()
            .Where(r => r.RelatedIssueId == issueId && !r.IsVoided)
            .Select(r => r.ReceiptId)
            .ToListAsync();

        if (returnReceiptIds.Count == 0)
        {
            return new Dictionary<long, decimal>();
        }

        return await _unitOfWork.Repository<ReceiptDetail>().Query()
            .Where(d => returnReceiptIds.Contains(d.ReceiptId))
            .GroupBy(d => d.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(d => d.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Total);
    }

    // Reuses a per-request in-memory cache so multiple lines for the same product
    // accumulate onto one Stock instance instead of racing separate DB lookups
    // (which would otherwise insert duplicate rows and violate the unique constraint).
    private async Task IncreaseStock(Dictionary<long, Stock> stockCache, long warehouseId, long productId, decimal quantity, decimal unitCost)
    {
        if (stockCache.TryGetValue(productId, out var cachedStock))
        {
            var cachedNewQuantity = cachedStock.Quantity + quantity;
            cachedStock.AverageCost = ((cachedStock.Quantity * cachedStock.AverageCost) + (quantity * unitCost)) / cachedNewQuantity;
            cachedStock.Quantity = cachedNewQuantity;
            return;
        }

        var repository = _unitOfWork.Repository<Stock>();
        var stock = await repository.Query()
            .FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.ProductId == productId);

        if (stock is null)
        {
            stock = new Stock
            {
                WarehouseId = warehouseId,
                ProductId = productId,
                Quantity = quantity,
                AverageCost = unitCost
            };
            await repository.AddAsync(stock);
            stockCache[productId] = stock;
            return;
        }

        var newQuantity = stock.Quantity + quantity;
        stock.AverageCost = ((stock.Quantity * stock.AverageCost) + (quantity * unitCost)) / newQuantity;
        stock.Quantity = newQuantity;
        repository.Update(stock);
        stockCache[productId] = stock;
    }

}