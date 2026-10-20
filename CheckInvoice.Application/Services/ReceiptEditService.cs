using System.Globalization;
using System.Net;
using System.Text.Json;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Security;
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
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Corrección de una Entrada ya registrada (datos de la compra y productos), con el mismo
// circuito que las anulaciones: quien trabaja en almacén la solicita con un motivo y el Contador
// la aprueba o la rechaza. Si la entrada ya tuvo salidas, una cantidad solo puede bajar hasta lo
// que todavía queda en el almacén; esas unidades quedan apartadas mientras se decide. Al aprobar
// se ajusta el stock y la nota vuelve a "digitado" como una versión nueva (hay que volver a
// verificarla y aprobarla).
public class ReceiptEditService : IReceiptEditService
{
    public const string TableName = "receipt_edit";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ReceiptEditService(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    internal static ReceiptEditProposal? ReadProposal(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<ReceiptEditProposal>(json, JsonOptions);

    public async Task<ResponsePost> RequestEdit(ReceiptEditRequestDto request)
    {
        var receipt = await _unitOfWork.Repository<Receipt>().GetByIdAsync(request.ReceiptId);
        if (receipt is null)
        {
            return Error(0, "No se encontró la entrada.", HttpStatusCode.NotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error(0, "Escribe el motivo de la corrección.", HttpStatusCode.BadRequest);
        }

        if (await _unitOfWork.Repository<ChangeRequest>().Query()
                .AnyAsync(c => c.TableName == TableName && c.RecordId == receipt.ReceiptId && c.Status == "pending"))
        {
            return Error(0, "Esta entrada ya tiene una corrección pendiente de aprobación.", HttpStatusCode.BadRequest);
        }

        var (errors, decreases) = await ValidateAsync(receipt, request, excludeChangeRequestId: null);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.Select(e => new Message { Type = MessageType.Error, Description = e }).ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var current = await CurrentAsRequestAsync(receipt);
        var changeRequest = new ChangeRequest
        {
            TableName = TableName,
            RecordId = receipt.ReceiptId,
            Action = "edit",
            CurrentData = JsonSerializer.Serialize(current, JsonOptions),
            ProposedData = JsonSerializer.Serialize(new ReceiptEditProposal
            {
                WarehouseId = receipt.WarehouseId ?? 0,
                Edit = request,
                Decreases = decreases
            }, JsonOptions),
            Reason = request.Reason.Trim(),
            Status = "pending",
            RequestedBy = _currentUserService.AppUserId!.Value,
            RequestedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<ChangeRequest>().AddAsync(changeRequest);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = changeRequest.ChangeRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Corrección enviada. El Contador debe aprobarla." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> Approve(long changeRequestId, string? notes)
    {
        var changeRequest = await _unitOfWork.Repository<ChangeRequest>().GetByIdAsync(changeRequestId);
        if (changeRequest is null || changeRequest.TableName != TableName)
        {
            return Error(changeRequestId, "No se encontró la corrección.", HttpStatusCode.NotFound);
        }
        if (changeRequest.Status != "pending")
        {
            return Error(changeRequestId, "La corrección ya fue revisada.", HttpStatusCode.BadRequest);
        }

        var receipt = await _unitOfWork.Repository<Receipt>().GetByIdAsync(changeRequest.RecordId);
        var proposal = ReadProposal(changeRequest.ProposedData);
        if (receipt is null || proposal is null)
        {
            return Error(changeRequestId, "La entrada ya no existe.", HttpStatusCode.BadRequest);
        }

        // Se vuelve a revisar todo: desde que se pidió pudo haber nuevas salidas.
        var edit = proposal.Edit;
        var (errors, _) = await ValidateAsync(receipt, edit, changeRequestId);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = changeRequestId,
                Messages = errors.Select(e => new Message { Type = MessageType.Error, Description = e }).ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var receiptType = receipt.ReceiptTypeId.HasValue
            ? await _unitOfWork.Repository<ReceiptType>().GetByIdAsync(receipt.ReceiptTypeId.Value)
            : null;
        var withRetention = receiptType?.Name == ReceiptCosting.PurchaseWithRetention;
        var isPurchase = ReceiptCosting.IsPurchaseType(receiptType?.Name);
        var warehouseId = receipt.WarehouseId!.Value;

        // Stock: se revierte lo que sumó cada línea vieja y se suma lo de la nueva, en cantidad y en
        // valor; el costo promedio se recalcula con el valor que queda.
        var oldLines = await _unitOfWork.Repository<ReceiptDetail>().Query()
            .Where(d => d.ReceiptId == receipt.ReceiptId)
            .ToListAsync();
        var newLines = edit.Details.Select(d => new ReceiptDetail
        {
            ReceiptId = receipt.ReceiptId,
            ProductId = d.ProductId,
            Quantity = d.Quantity,
            UnitPrice = d.UnitPrice,
            UnitCost = ReceiptCosting.UnitCost(d.UnitPrice, withRetention),
            TotalCost = ReceiptCosting.TotalCost(d.Quantity, d.UnitPrice, withRetention)
        }).ToList();

        var productIds = oldLines.Select(l => l.ProductId).Union(newLines.Select(l => l.ProductId)).ToList();
        var stockRepository = _unitOfWork.Repository<Stock>();
        foreach (var productId in productIds)
        {
            var quantityDelta = newLines.Where(l => l.ProductId == productId).Sum(l => l.Quantity)
                                - oldLines.Where(l => l.ProductId == productId).Sum(l => l.Quantity);
            var valueDelta = newLines.Where(l => l.ProductId == productId).Sum(l => l.TotalCost)
                             - oldLines.Where(l => l.ProductId == productId).Sum(l => l.TotalCost);
            if (quantityDelta == 0 && valueDelta == 0)
            {
                continue;
            }

            var stock = await stockRepository.Query().FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.ProductId == productId);
            if (stock is null)
            {
                var newLine = newLines.First(l => l.ProductId == productId);
                await stockRepository.AddAsync(new Stock
                {
                    WarehouseId = warehouseId,
                    ProductId = productId,
                    Quantity = quantityDelta,
                    AverageCost = newLine.UnitCost
                });
                continue;
            }

            var value = stock.Quantity * stock.AverageCost + valueDelta;
            stock.Quantity += quantityDelta;
            if (stock.Quantity > 0)
            {
                stock.AverageCost = Math.Max(value, 0) / stock.Quantity;
            }
            stockRepository.Update(stock);
        }

        var detailRepository = _unitOfWork.Repository<ReceiptDetail>();
        foreach (var line in oldLines)
        {
            detailRepository.Remove(line);
        }
        foreach (var line in newLines)
        {
            await detailRepository.AddAsync(line);
        }

        receipt.InvoiceNumber = string.IsNullOrWhiteSpace(edit.InvoiceNumber) ? null : edit.InvoiceNumber.Trim();
        receipt.TaxId = string.IsNullOrWhiteSpace(edit.TaxId) ? null : edit.TaxId.Trim();
        receipt.Description = edit.Description;
        receipt.InvoiceTotal = isPurchase ? edit.InvoiceTotal : null;
        receipt.PurchaseDate = isPurchase ? edit.PurchaseDate?.Date : receipt.PurchaseDate;
        if (edit.SupplierId.HasValue)
        {
            receipt.SupplierId = edit.SupplierId;
        }
        if (edit.IssueDate.HasValue)
        {
            receipt.IssueDate = edit.IssueDate.Value.Date;
        }

        // La nota cambió: vuelve a digitado como versión nueva y queda constancia en su historial.
        var version = receipt.ReviewVersion;
        await _unitOfWork.Repository<DocumentReview>().AddAsync(new DocumentReview
        {
            DocumentType = DocumentReviews.Receipt,
            DocumentId = receipt.ReceiptId,
            Version = version,
            Action = DocumentReviews.Corrected,
            UserId = _currentUserService.AppUserId,
            At = DateTime.UtcNow,
            Reason = Truncate($"Corrección aprobada: {changeRequest.Reason}", 255)
        });
        receipt.ReviewStatus = DocumentReviews.Entered;
        receipt.ReviewVersion = version + 1;
        _unitOfWork.Repository<Receipt>().Update(receipt);

        changeRequest.Status = "approved";
        changeRequest.ReviewedBy = _currentUserService.AppUserId;
        changeRequest.ReviewedAt = DateTime.UtcNow;
        changeRequest.ReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        _unitOfWork.Repository<ChangeRequest>().Update(changeRequest);

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = changeRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Corrección aprobada. La nota quedó como una versión nueva." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> Reject(long changeRequestId, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return Error(changeRequestId, "Escribe el motivo del rechazo.", HttpStatusCode.BadRequest);
        }

        var changeRequest = await _unitOfWork.Repository<ChangeRequest>().GetByIdAsync(changeRequestId);
        if (changeRequest is null || changeRequest.TableName != TableName)
        {
            return Error(changeRequestId, "No se encontró la corrección.", HttpStatusCode.NotFound);
        }
        if (changeRequest.Status != "pending")
        {
            return Error(changeRequestId, "La corrección ya fue revisada.", HttpStatusCode.BadRequest);
        }

        changeRequest.Status = "rejected";
        changeRequest.ReviewedBy = _currentUserService.AppUserId;
        changeRequest.ReviewedAt = DateTime.UtcNow;
        changeRequest.ReviewNotes = notes.Trim();
        _unitOfWork.Repository<ChangeRequest>().Update(changeRequest);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = changeRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Corrección rechazada." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetEdits(string? status, long? receiptId)
    {
        var query = _unitOfWork.Repository<ChangeRequest>().Query().Where(c => c.TableName == TableName);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }
        if (receiptId.HasValue)
        {
            query = query.Where(c => c.RecordId == receiptId.Value);
        }

        var requests = await query.OrderByDescending(c => c.RequestedAt).Take(200).ToListAsync();
        var userIds = requests.Select(r => r.RequestedBy).Concat(requests.Where(r => r.ReviewedBy.HasValue).Select(r => r.ReviewedBy!.Value)).Distinct().ToList();
        var users = await _unitOfWork.Repository<AppUser>().Query()
            .Where(u => userIds.Contains(u.AppUserId))
            .ToDictionaryAsync(u => u.AppUserId, u => $"{u.FirstName} {u.LastName}".Trim());
        var receiptIds = requests.Select(r => r.RecordId).Distinct().ToList();
        var receipts = await _unitOfWork.Repository<Receipt>().Query()
            .Where(r => receiptIds.Contains(r.ReceiptId))
            .Select(r => new { r.ReceiptId, r.MonthlyNumber, r.IssueDate })
            .ToDictionaryAsync(r => r.ReceiptId);

        var productIds = new HashSet<long>();
        var parsed = requests.Select(r =>
        {
            var before = string.IsNullOrEmpty(r.CurrentData) ? null : JsonSerializer.Deserialize<ReceiptEditRequestDto>(r.CurrentData, JsonOptions);
            var after = ReadProposal(r.ProposedData)?.Edit;
            foreach (var line in (before?.Details ?? []).Concat(after?.Details ?? []))
            {
                productIds.Add(line.ProductId);
            }
            return (Request: r, Before: before, After: after);
        }).ToList();

        var products = await _unitOfWork.Repository<Product>().Query()
            .Where(p => productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => new { p.Code, p.Name });

        var items = parsed.Select(x =>
        {
            var receipt = receipts.GetValueOrDefault(x.Request.RecordId);
            var dto = new ReceiptEditDto
            {
                ChangeRequestId = x.Request.ChangeRequestId,
                ReceiptId = x.Request.RecordId,
                NoteNumber = receipt is null ? x.Request.RecordId.ToString(CultureInfo.InvariantCulture) : ReceiptNumbering.Format(receipt.MonthlyNumber, receipt.IssueDate, receipt.ReceiptId),
                Status = x.Request.Status,
                Reason = x.Request.Reason,
                RequestedByName = users.GetValueOrDefault(x.Request.RequestedBy),
                RequestedAt = x.Request.RequestedAt,
                ReviewedByName = x.Request.ReviewedBy.HasValue ? users.GetValueOrDefault(x.Request.ReviewedBy.Value) : null,
                ReviewedAt = x.Request.ReviewedAt,
                ReviewNotes = x.Request.ReviewNotes
            };
            if (x.Before is not null && x.After is not null)
            {
                dto.HeaderChanges = HeaderChanges(x.Before, x.After);
                foreach (var productId in x.Before.Details.Select(d => d.ProductId).Union(x.After.Details.Select(d => d.ProductId)))
                {
                    var before = x.Before.Details.FirstOrDefault(d => d.ProductId == productId);
                    var after = x.After.Details.FirstOrDefault(d => d.ProductId == productId);
                    var product = products.GetValueOrDefault(productId);
                    dto.Lines.Add(new ReceiptEditLineChangeDto
                    {
                        ProductId = productId,
                        Code = product?.Code,
                        Name = product?.Name ?? $"#{productId}",
                        QuantityBefore = before?.Quantity ?? 0,
                        QuantityAfter = after?.Quantity ?? 0,
                        PriceBefore = before?.UnitPrice ?? 0,
                        PriceAfter = after?.UnitPrice ?? 0
                    });
                }
            }
            return dto;
        }).ToList();

        return new ResponseGetObject { Data = items, Messages = [], StatusCode = HttpStatusCode.OK };
    }

    // ---------------------------------------------------------------------------------------------

    private async Task<(List<string> Errors, List<ReceiptEditDecrease> Decreases)> ValidateAsync(
        Receipt receipt, ReceiptEditRequestDto edit, long? excludeChangeRequestId)
    {
        var errors = new List<string>();
        var decreases = new List<ReceiptEditDecrease>();

        if (receipt.IsVoided)
        {
            errors.Add("La entrada está anulada: ya no se corrige.");
            return (errors, decreases);
        }
        if (receipt.RelatedIssueId.HasValue)
        {
            errors.Add("Una devolución no se corrige: anúlala y registra la devolución de nuevo.");
            return (errors, decreases);
        }
        if (await _unitOfWork.Repository<ReceiptVoidRequest>().Query().AnyAsync(v => v.ReceiptId == receipt.ReceiptId && v.Status == "pending"))
        {
            errors.Add("La entrada tiene una anulación pendiente.");
            return (errors, decreases);
        }
        if (!receipt.WarehouseId.HasValue)
        {
            errors.Add("La entrada no tiene almacén.");
            return (errors, decreases);
        }

        if (receipt.WarehousePeriodId.HasValue)
        {
            var period = await _unitOfWork.Repository<WarehousePeriod>().GetByIdAsync(receipt.WarehousePeriodId.Value);
            if (period?.IsClosed == true)
            {
                errors.Add($"El período {period.Name} está cerrado: la entrada ya no se puede corregir.");
            }
        }

        var receiptType = receipt.ReceiptTypeId.HasValue
            ? await _unitOfWork.Repository<ReceiptType>().GetByIdAsync(receipt.ReceiptTypeId.Value)
            : null;
        var isPurchase = ReceiptCosting.IsPurchaseType(receiptType?.Name);
        var withRetention = receiptType?.Name == ReceiptCosting.PurchaseWithRetention;

        if (edit.IssueDate.HasValue)
        {
            var date = edit.IssueDate.Value.Date;
            if (date.Year != receipt.IssueDate.Year || date.Month != receipt.IssueDate.Month)
            {
                errors.Add("La fecha de ingreso solo se puede corregir dentro del mismo mes de la nota.");
            }
            else if (date > BoliviaTime.Today)
            {
                errors.Add("La fecha de ingreso no puede ser posterior a hoy.");
            }
        }

        if (edit.SupplierId.HasValue && !await _unitOfWork.Repository<Supplier>().Query().AnyAsync(s => s.SupplierId == edit.SupplierId.Value))
        {
            errors.Add("El proveedor elegido no existe.");
        }

        if (isPurchase)
        {
            if (string.IsNullOrWhiteSpace(edit.TaxId))
            {
                errors.Add(withRetention ? "El CI es obligatorio." : "El NIT es obligatorio.");
            }
            if (!withRetention && string.IsNullOrWhiteSpace(edit.InvoiceNumber))
            {
                errors.Add("El número de factura es obligatorio.");
            }
            if (edit.PurchaseDate is null || edit.PurchaseDate.Value.Date > BoliviaTime.Today)
            {
                errors.Add("La fecha de compra es obligatoria y no puede ser posterior a hoy.");
            }
            var purchaseTotal = edit.Details.Sum(d => d.Quantity * d.UnitPrice);
            if (edit.InvoiceTotal is null || Math.Abs(purchaseTotal - edit.InvoiceTotal.Value) > 0.01m)
            {
                errors.Add($"La suma de los productos ({purchaseTotal:N2}) no coincide con el valor total de la compra ({edit.InvoiceTotal ?? 0:N2}).");
            }
        }

        if (edit.Details.Count == 0)
        {
            errors.Add("La entrada debe tener al menos un producto. Para dejarla sin productos, anúlala.");
        }
        if (edit.Details.Select(d => d.ProductId).Distinct().Count() != edit.Details.Count)
        {
            errors.Add("No se puede repetir el mismo producto en más de una línea.");
        }
        if (edit.Details.Any(d => d.Quantity <= 0 || d.UnitPrice < 0))
        {
            errors.Add("Cada producto necesita una cantidad mayor a cero y un precio de 0 o más.");
        }
        var newIds = edit.Details.Select(d => d.ProductId).Distinct().ToList();
        var existing = await _unitOfWork.Repository<Product>().Query().Where(p => newIds.Contains(p.ProductId)).Select(p => p.ProductId).ToListAsync();
        if (existing.Count != newIds.Count)
        {
            errors.Add("Uno de los productos elegidos no existe.");
        }

        if (errors.Count > 0)
        {
            return (errors, decreases);
        }

        // Si la entrada ya tuvo salidas, solo se puede bajar hasta lo que queda en el almacén
        // (descontando lo apartado por anulaciones o por otras correcciones pendientes).
        var oldQuantities = await _unitOfWork.Repository<ReceiptDetail>().Query()
            .Where(d => d.ReceiptId == receipt.ReceiptId)
            .GroupBy(d => d.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(d => d.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);
        var warehouseId = receipt.WarehouseId.Value;

        foreach (var (productId, oldQuantity) in oldQuantities)
        {
            var newQuantity = edit.Details.Where(d => d.ProductId == productId).Sum(d => d.Quantity);
            var decrease = oldQuantity - newQuantity;
            if (decrease <= 0)
            {
                continue;
            }

            var stock = await _unitOfWork.Repository<Stock>().Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.ProductId == productId);
            var reserved = await PendingReceiptVoidStock.GetReservedQuantityAsync(_unitOfWork, warehouseId, productId, excludeChangeRequestId: excludeChangeRequestId);
            var available = Math.Max((stock?.Quantity ?? 0) - reserved, 0);
            if (decrease > available)
            {
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(productId);
                var minimum = oldQuantity - available;
                errors.Add($"{product?.Name ?? $"#{productId}"}: de esta entrada ya salieron unidades. Solo quedan {available:0.##} en el almacén, así que la cantidad no puede bajar de {minimum:0.##}.");
                continue;
            }

            decreases.Add(new ReceiptEditDecrease { ProductId = productId, Quantity = decrease });
        }

        return (errors, decreases);
    }

    private async Task<ReceiptEditRequestDto> CurrentAsRequestAsync(Receipt receipt)
    {
        var lines = await _unitOfWork.Repository<ReceiptDetail>().Query()
            .Where(d => d.ReceiptId == receipt.ReceiptId)
            .OrderBy(d => d.ReceiptDetailId)
            .ToListAsync();
        return new ReceiptEditRequestDto
        {
            ReceiptId = receipt.ReceiptId,
            SupplierId = receipt.SupplierId,
            TaxId = receipt.TaxId,
            InvoiceNumber = receipt.InvoiceNumber,
            Description = receipt.Description,
            InvoiceTotal = receipt.InvoiceTotal,
            IssueDate = receipt.IssueDate,
            PurchaseDate = receipt.PurchaseDate,
            Details = lines.Select(l => new ReceiptDetailRequestDto
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice ?? l.UnitCost
            }).ToList()
        };
    }

    private static List<ReceiptEditFieldChangeDto> HeaderChanges(ReceiptEditRequestDto before, ReceiptEditRequestDto after)
    {
        var changes = new List<ReceiptEditFieldChangeDto>();
        void Add(string field, string? oldValue, string? newValue)
        {
            if ((oldValue ?? string.Empty) != (newValue ?? string.Empty))
            {
                changes.Add(new ReceiptEditFieldChangeDto { Field = field, Before = oldValue, After = newValue });
            }
        }
        var invariant = CultureInfo.InvariantCulture;
        Add("issueDate", before.IssueDate?.ToString("dd/MM/yyyy", invariant), after.IssueDate?.ToString("dd/MM/yyyy", invariant));
        Add("purchaseDate", before.PurchaseDate?.ToString("dd/MM/yyyy", invariant), after.PurchaseDate?.ToString("dd/MM/yyyy", invariant));
        Add("invoiceNumber", before.InvoiceNumber, after.InvoiceNumber);
        Add("taxId", before.TaxId, after.TaxId);
        Add("invoiceTotal", before.InvoiceTotal?.ToString("0.00", invariant), after.InvoiceTotal?.ToString("0.00", invariant));
        Add("description", before.Description, after.Description);
        return changes;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static ResponsePost Error(long id, string message, HttpStatusCode statusCode) => new()
    {
        Id = id,
        Messages = [new Message { Type = MessageType.Error, Description = message }],
        StatusCode = statusCode
    };
}
