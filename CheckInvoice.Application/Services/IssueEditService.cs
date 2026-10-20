using System.Globalization;
using System.Net;
using System.Text.Json;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Security;
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
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Corrección de una Salida ya registrada (tipo, cliente, fecha, observación y productos), con el
// mismo circuito que la corrección de Entradas: se solicita con un motivo y el Contador la aprueba
// o la rechaza. Al aprobar se ajusta el stock (si sube una cantidad, sale más del almacén; si baja,
// vuelve al almacén con su costo) y la salida vuelve a "digitado" como una versión nueva.
public class IssueEditService : IIssueEditService
{
    public const string TableName = "issue_edit";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public IssueEditService(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    private static IssueEditRequestDto? Read(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<IssueEditRequestDto>(json, JsonOptions);

    public async Task<ResponsePost> RequestEdit(IssueEditRequestDto request)
    {
        var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(request.IssueId);
        if (issue is null)
        {
            return Error(0, "No se encontró la salida.", HttpStatusCode.NotFound);
        }
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error(0, "Escribe el motivo de la corrección.", HttpStatusCode.BadRequest);
        }
        if (await _unitOfWork.Repository<ChangeRequest>().Query()
                .AnyAsync(c => c.TableName == TableName && c.RecordId == issue.IssueId && c.Status == "pending"))
        {
            return Error(0, "Esta salida ya tiene una corrección pendiente de aprobación.", HttpStatusCode.BadRequest);
        }

        var errors = await ValidateAsync(issue, request);
        if (errors.Count > 0)
        {
            return Errors(0, errors);
        }

        var changeRequest = new ChangeRequest
        {
            TableName = TableName,
            RecordId = issue.IssueId,
            Action = "edit",
            CurrentData = JsonSerializer.Serialize(await CurrentAsRequestAsync(issue), JsonOptions),
            ProposedData = JsonSerializer.Serialize(request, JsonOptions),
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

        var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(changeRequest.RecordId);
        var edit = Read(changeRequest.ProposedData);
        if (issue is null || edit is null)
        {
            return Error(changeRequestId, "La salida ya no existe.", HttpStatusCode.BadRequest);
        }

        // Se vuelve a revisar todo: desde que se pidió pudo cambiar el stock.
        var errors = await ValidateAsync(issue, edit);
        if (errors.Count > 0)
        {
            return Errors(changeRequestId, errors);
        }

        var warehouseId = issue.WarehouseId!.Value;
        var oldLines = await _unitOfWork.Repository<IssueDetail>().Query().Where(d => d.IssueId == issue.IssueId).ToListAsync();
        var stockRepository = _unitOfWork.Repository<Stock>();

        // Cada producto conserva el costo con el que salió; uno nuevo sale al costo promedio actual.
        var newLines = new List<IssueDetail>();
        foreach (var line in edit.Details)
        {
            var old = oldLines.FirstOrDefault(l => l.ProductId == line.ProductId);
            var unitCost = old?.UnitCost ?? (await stockRepository.Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.ProductId == line.ProductId))?.AverageCost ?? 0;
            newLines.Add(new IssueDetail
            {
                IssueId = issue.IssueId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                TotalCost = line.Quantity * unitCost
            });
        }

        // Stock: lo que sale de más se descuenta; lo que sale de menos vuelve con su costo.
        foreach (var productId in oldLines.Select(l => l.ProductId).Union(newLines.Select(l => l.ProductId)))
        {
            var oldQuantity = oldLines.Where(l => l.ProductId == productId).Sum(l => l.Quantity);
            var newQuantity = newLines.Where(l => l.ProductId == productId).Sum(l => l.Quantity);
            var delta = newQuantity - oldQuantity;
            if (delta == 0)
            {
                continue;
            }
            var stock = await stockRepository.Query().FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.ProductId == productId);
            var unitCost = (oldLines.FirstOrDefault(l => l.ProductId == productId) ?? newLines.First(l => l.ProductId == productId)).UnitCost;
            if (stock is null)
            {
                await stockRepository.AddAsync(new Stock { WarehouseId = warehouseId, ProductId = productId, Quantity = -delta, AverageCost = unitCost });
                continue;
            }
            if (delta < 0)
            {
                var value = stock.Quantity * stock.AverageCost + (-delta) * unitCost;
                stock.Quantity -= delta;
                stock.AverageCost = stock.Quantity > 0 ? value / stock.Quantity : stock.AverageCost;
            }
            else
            {
                stock.Quantity -= delta;
            }
            stockRepository.Update(stock);
        }

        var detailRepository = _unitOfWork.Repository<IssueDetail>();
        foreach (var line in oldLines)
        {
            detailRepository.Remove(line);
        }
        foreach (var line in newLines)
        {
            await detailRepository.AddAsync(line);
        }

        issue.IssueTypeId = edit.IssueTypeId ?? issue.IssueTypeId;
        issue.ClientId = edit.ClientId ?? issue.ClientId;
        issue.Description = edit.Description;
        if (edit.IssueDate.HasValue)
        {
            issue.IssueDate = edit.IssueDate.Value.Date;
        }

        // Cuenta por cobrar sin pagos: se ajusta al nuevo total (y al cliente, si cambió).
        var account = await _unitOfWork.Repository<AccountReceivable>().Query().FirstOrDefaultAsync(a => a.IssueId == issue.IssueId);
        if (account is not null)
        {
            var total = newLines.Sum(l => l.TotalCost);
            account.TotalAmount = total;
            account.OutstandingBalance = total;
            account.ClientId = issue.ClientId;
            _unitOfWork.Repository<AccountReceivable>().Update(account);
        }

        // La salida cambió: vuelve a digitado como versión nueva y queda constancia en su historial.
        var version = issue.ReviewVersion;
        await _unitOfWork.Repository<DocumentReview>().AddAsync(new DocumentReview
        {
            DocumentType = DocumentReviews.Issue,
            DocumentId = issue.IssueId,
            Version = version,
            Action = DocumentReviews.Corrected,
            UserId = _currentUserService.AppUserId,
            At = DateTime.UtcNow,
            Reason = Truncate($"Corrección aprobada: {changeRequest.Reason}", 255)
        });
        issue.ReviewStatus = DocumentReviews.Entered;
        issue.ReviewVersion = version + 1;
        _unitOfWork.Repository<Issue>().Update(issue);

        changeRequest.Status = "approved";
        changeRequest.ReviewedBy = _currentUserService.AppUserId;
        changeRequest.ReviewedAt = DateTime.UtcNow;
        changeRequest.ReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        _unitOfWork.Repository<ChangeRequest>().Update(changeRequest);

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = changeRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Corrección aprobada. La salida quedó como una versión nueva." }],
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

    public async Task<ResponseGetObject> GetEdits(string? status, long? issueId)
    {
        var query = _unitOfWork.Repository<ChangeRequest>().Query().Where(c => c.TableName == TableName);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }
        if (issueId.HasValue)
        {
            query = query.Where(c => c.RecordId == issueId.Value);
        }

        var requests = await query.OrderByDescending(c => c.RequestedAt).Take(200).ToListAsync();
        var userIds = requests.Select(r => r.RequestedBy).Concat(requests.Where(r => r.ReviewedBy.HasValue).Select(r => r.ReviewedBy!.Value)).Distinct().ToList();
        var users = await _unitOfWork.Repository<AppUser>().Query()
            .Where(u => userIds.Contains(u.AppUserId))
            .ToDictionaryAsync(u => u.AppUserId, u => $"{u.FirstName} {u.LastName}".Trim());
        var numbers = await IssueNumbering.NumbersAsync(_unitOfWork, requests.Select(r => r.RecordId));
        var issueTypes = await _unitOfWork.Repository<IssueType>().Query().ToDictionaryAsync(t => t.IssueTypeId, t => t.Name);

        var parsed = requests.Select(r => (Request: r, Before: Read(r.CurrentData), After: Read(r.ProposedData))).ToList();
        var productIds = parsed.SelectMany(x => (x.Before?.Details ?? []).Concat(x.After?.Details ?? [])).Select(d => d.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Repository<Product>().Query()
            .Where(p => productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => new { p.Code, p.Name });
        var clientIds = parsed.SelectMany(x => new[] { x.Before?.ClientId, x.After?.ClientId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var clients = await _unitOfWork.Repository<Client>().Query()
            .Where(c => clientIds.Contains(c.ClientId))
            .Join(_unitOfWork.Repository<Party>().Query(), c => c.PartyId, p => p.PartyId, (c, p) => new { c.ClientId, p.Name })
            .ToDictionaryAsync(x => x.ClientId, x => x.Name);

        var items = parsed.Select(x =>
        {
            var dto = new IssueEditDto
            {
                ChangeRequestId = x.Request.ChangeRequestId,
                IssueId = x.Request.RecordId,
                NoteNumber = numbers.GetValueOrDefault(x.Request.RecordId) ?? x.Request.RecordId.ToString("00000", CultureInfo.InvariantCulture),
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
                var invariant = CultureInfo.InvariantCulture;
                void Add(string field, string? before, string? after)
                {
                    if ((before ?? string.Empty) != (after ?? string.Empty))
                    {
                        dto.HeaderChanges.Add(new ReceiptEditFieldChangeDto { Field = field, Before = before, After = after });
                    }
                }
                // Un dato que la corrección no manda (null) queda como estaba: no es un cambio.
                var afterDate = x.After.IssueDate ?? x.Before.IssueDate;
                var afterType = x.After.IssueTypeId ?? x.Before.IssueTypeId;
                var afterClient = x.After.ClientId ?? x.Before.ClientId;
                Add("issueDate", x.Before.IssueDate?.ToString("dd/MM/yyyy", invariant), afterDate?.ToString("dd/MM/yyyy", invariant));
                Add("issueType", x.Before.IssueTypeId is { } bt ? issueTypes.GetValueOrDefault(bt) : null, afterType is { } at ? issueTypes.GetValueOrDefault(at) : null);
                Add("client", x.Before.ClientId is { } bc ? clients.GetValueOrDefault(bc) : null, afterClient is { } ac ? clients.GetValueOrDefault(ac) : null);
                Add("description", x.Before.Description, x.After.Description);
                foreach (var productId in x.Before.Details.Select(d => d.ProductId).Union(x.After.Details.Select(d => d.ProductId)))
                {
                    var product = products.GetValueOrDefault(productId);
                    dto.Lines.Add(new ReceiptEditLineChangeDto
                    {
                        ProductId = productId,
                        Code = product?.Code,
                        Name = product?.Name ?? $"#{productId}",
                        QuantityBefore = x.Before.Details.Where(d => d.ProductId == productId).Sum(d => d.Quantity),
                        QuantityAfter = x.After.Details.Where(d => d.ProductId == productId).Sum(d => d.Quantity)
                    });
                }
            }
            return dto;
        }).ToList();

        return new ResponseGetObject { Data = items, Messages = [], StatusCode = HttpStatusCode.OK };
    }

    // ---------------------------------------------------------------------------------------------

    private async Task<List<string>> ValidateAsync(Issue issue, IssueEditRequestDto edit)
    {
        var errors = new List<string>();
        if (issue.IsVoided)
        {
            errors.Add("La salida está anulada: ya no se corrige.");
            return errors;
        }
        if (await _unitOfWork.Repository<IssueVoidRequest>().Query().AnyAsync(v => v.IssueId == issue.IssueId && v.Status == "pending"))
        {
            errors.Add("La salida tiene una anulación pendiente.");
            return errors;
        }
        if (!issue.WarehouseId.HasValue)
        {
            errors.Add("La salida no tiene almacén.");
            return errors;
        }
        if (issue.WarehousePeriodId.HasValue)
        {
            var period = await _unitOfWork.Repository<WarehousePeriod>().GetByIdAsync(issue.WarehousePeriodId.Value);
            if (period?.IsClosed == true)
            {
                errors.Add($"El período {period.Name} está cerrado: la salida ya no se puede corregir.");
            }
        }
        // Una cuenta por cobrar con pagos ya no se toca: hay que anular la salida.
        var account = await _unitOfWork.Repository<AccountReceivable>().Query().FirstOrDefaultAsync(a => a.IssueId == issue.IssueId);
        if (account is not null && account.OutstandingBalance != account.TotalAmount)
        {
            errors.Add("La salida ya tiene pagos en su cuenta por cobrar: no se corrige. Anúlala y regístrala de nuevo.");
        }
        if (devolutionExists(issue.IssueId))
        {
            errors.Add("La salida ya tiene devoluciones registradas: no se corrige. Anúlala y regístrala de nuevo.");
        }

        if (edit.IssueDate.HasValue)
        {
            var date = edit.IssueDate.Value.Date;
            if (date.Year != issue.IssueDate.Year || date.Month != issue.IssueDate.Month)
            {
                errors.Add("La fecha de la salida solo se puede corregir dentro del mismo mes.");
            }
            else if (date > BoliviaTime.Today)
            {
                errors.Add("La fecha de la salida no puede ser posterior a hoy.");
            }
        }
        if (edit.IssueTypeId.HasValue && !await _unitOfWork.Repository<IssueType>().Query().AnyAsync(t => t.IssueTypeId == edit.IssueTypeId.Value))
        {
            errors.Add("El tipo de salida elegido no existe.");
        }
        if (edit.ClientId.HasValue && !await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.ClientId == edit.ClientId.Value))
        {
            errors.Add("El cliente elegido no existe.");
        }
        if (edit.Details.Count == 0)
        {
            errors.Add("La salida debe tener al menos un producto. Para dejarla sin productos, anúlala.");
        }
        if (edit.Details.Select(d => d.ProductId).Distinct().Count() != edit.Details.Count)
        {
            errors.Add("No se puede repetir el mismo producto en más de una línea.");
        }
        if (edit.Details.Any(d => d.Quantity <= 0))
        {
            errors.Add("Cada producto necesita una cantidad mayor a cero.");
        }
        if (errors.Count > 0)
        {
            return errors;
        }

        // Lo que sale de más tiene que estar disponible en el almacén.
        var warehouseId = issue.WarehouseId.Value;
        var oldQuantities = await _unitOfWork.Repository<IssueDetail>().Query()
            .Where(d => d.IssueId == issue.IssueId)
            .GroupBy(d => d.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(d => d.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);
        foreach (var line in edit.Details)
        {
            var increase = line.Quantity - oldQuantities.GetValueOrDefault(line.ProductId);
            if (increase <= 0)
            {
                continue;
            }
            var stock = await _unitOfWork.Repository<Stock>().Query().FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.ProductId == line.ProductId);
            var reserved = await PendingReceiptVoidStock.GetReservedQuantityAsync(_unitOfWork, warehouseId, line.ProductId);
            var available = Math.Max((stock?.Quantity ?? 0) - reserved, 0);
            if (increase > available)
            {
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(line.ProductId);
                errors.Add($"Stock insuficiente de \"{product?.Name ?? $"#{line.ProductId}"}\": para sacar {increase:0.##} más hay {available:0.##} disponibles.");
            }
        }
        return errors;

        bool devolutionExists(long issueId) =>
            _unitOfWork.Repository<Receipt>().Query().Any(r => r.RelatedIssueId == issueId && !r.IsVoided);
    }

    private async Task<IssueEditRequestDto> CurrentAsRequestAsync(Issue issue)
    {
        var lines = await _unitOfWork.Repository<IssueDetail>().Query()
            .Where(d => d.IssueId == issue.IssueId)
            .OrderBy(d => d.IssueDetailId)
            .ToListAsync();
        return new IssueEditRequestDto
        {
            IssueId = issue.IssueId,
            IssueTypeId = issue.IssueTypeId,
            ClientId = issue.ClientId,
            Description = issue.Description,
            IssueDate = issue.IssueDate,
            Details = lines.Select(l => new IssueDetailRequestDto { ProductId = l.ProductId, Quantity = l.Quantity }).ToList()
        };
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static ResponsePost Error(long id, string message, HttpStatusCode statusCode) => new()
    {
        Id = id,
        Messages = [new Message { Type = MessageType.Error, Description = message }],
        StatusCode = statusCode
    };

    private static ResponsePost Errors(long id, List<string> errors) => new()
    {
        Id = id,
        Messages = errors.Select(e => new Message { Type = MessageType.Error, Description = e }).ToArray(),
        StatusCode = HttpStatusCode.BadRequest
    };
}
