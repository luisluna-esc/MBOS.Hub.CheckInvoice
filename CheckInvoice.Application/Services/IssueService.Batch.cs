using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Finance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Warehouses;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Salida por lote: los pastores elegidos salen con el mismo almacén, fecha y cobro, y cada uno
// con sus cantidades. Es UNA salida con UN N° ("S 5-10-26"): por dentro se guarda una fila de
// salida por pastor (así cada uno tiene su cuenta por cobrar, su anulación y su línea en
// contabilidad) y todas comparten ese mismo N°. Todo o nada: si falta stock de un material para
// el total del lote, no se guarda nada.
public partial class IssueService
{
    /// <summary>Nombre del tipo de salida de los lotes (tabla issue_type).</summary>
    public const string BatchIssueTypeName = "Salida por Lote";

    public async Task<ResponsePost> InsertIssueBatch(IssueBatchRequestDto request)
    {
        var errors = new List<string>();

        // Pastores sin ninguna cantidad no sacan nada: no generan salida.
        var pastors = request.Pastors
            .Select(p => new IssueBatchPastorRequestDto
            {
                ClientId = p.ClientId,
                Details = p.Details.Where(d => d.Quantity > 0).ToList()
            })
            .Where(p => p.Details.Count > 0)
            .ToList();

        if (request.WarehouseId <= 0)
        {
            errors.Add("Debes seleccionar un almacén.");
        }
        else if (!await _unitOfWork.Repository<Warehouse>().Query().AnyAsync(w => w.WarehouseId == request.WarehouseId))
        {
            errors.Add("El almacén seleccionado no existe.");
        }
        // El tipo es siempre "Salida por Lote" (la tarjeta que se eligió en Salidas).
        var batchTypeId = await _unitOfWork.Repository<IssueType>().Query()
            .Where(i => i.Name == BatchIssueTypeName)
            .Select(i => (long?)i.IssueTypeId)
            .FirstOrDefaultAsync();
        if (batchTypeId is null)
        {
            errors.Add($"Falta el tipo de salida \"{BatchIssueTypeName}\" en el catálogo de tipos de salida.");
        }
        if (request.PrintTypeId.HasValue &&
            !await _unitOfWork.Repository<PrintType>().Query().AnyAsync(p => p.PrintTypeId == request.PrintTypeId.Value))
        {
            errors.Add("El tipo de impresión seleccionado no existe.");
        }
        if ((request.Description?.Length ?? 0) > 200)
        {
            errors.Add("La observación del lote no puede superar los 200 caracteres.");
        }
        if (request.SendToAccountsReceivable && string.IsNullOrWhiteSpace(request.PaymentType))
        {
            errors.Add("Debes indicar el tipo de pago para enviar a Cuentas por Cobrar.");
        }
        if ((request.PaymentType?.Length ?? 0) > 20 || (request.PaymentDetail?.Length ?? 0) > 255)
        {
            errors.Add("El tipo o el detalle de pago es demasiado largo.");
        }

        if (pastors.Count == 0)
        {
            errors.Add("Ningún pastor tiene cantidades: el lote debe tener al menos un pastor con material.");
        }
        if (pastors.Select(p => p.ClientId).Distinct().Count() != pastors.Count)
        {
            errors.Add("Un pastor está repetido en el lote.");
        }
        if (pastors.Any(p => p.Details.Select(d => d.ProductId).Distinct().Count() != p.Details.Count))
        {
            errors.Add("No se puede repetir el mismo material para un pastor.");
        }

        var clientIds = pastors.Select(p => p.ClientId).Distinct().ToList();
        var validPastors = await _unitOfWork.Repository<Client>().Query()
            .Where(c => clientIds.Contains(c.ClientId) && c.IsPastor && c.IsActive)
            .Select(c => c.ClientId)
            .ToListAsync();
        if (validPastors.Count != clientIds.Count)
        {
            errors.Add("Algún pastor del lote no existe, está inactivo o no está registrado como pastor.");
        }

        var productIds = pastors.SelectMany(p => p.Details).Select(d => d.ProductId).Distinct().ToList();
        var existingProducts = await _unitOfWork.Repository<Product>().Query()
            .Where(p => productIds.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.Name })
            .ToDictionaryAsync(p => p.ProductId, p => p.Name);
        if (existingProducts.Count != productIds.Count)
        {
            errors.Add("Algún material del lote no existe.");
        }

        var issueDate = request.IssueDate?.Date ?? BoliviaTime.Today;
        WarehousePeriod? warehousePeriod = null;
        if (!MovementDates.IsWithinEntryWindow(issueDate))
        {
            errors.Add(MovementDates.EntryWindowMessage("fecha de la salida"));
        }
        else
        {
            warehousePeriod = await MovementDates.GetOrCreatePeriodAsync(_unitOfWork, issueDate);
            if (warehousePeriod.IsClosed)
            {
                errors.Add($"El período {warehousePeriod.Name} está cerrado y ya no admite nuevos movimientos.");
            }
        }

        if (errors.Count > 0)
        {
            return BatchError(errors);
        }

        // Stock: lo que suman todos los pastores de cada material contra lo disponible.
        var requestedByProduct = pastors.SelectMany(p => p.Details)
            .GroupBy(d => d.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));
        var stockByProduct = new Dictionary<long, Stock>();
        foreach (var (productId, requested) in requestedByProduct)
        {
            var stock = await _unitOfWork.Repository<Stock>().Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == request.WarehouseId && s.ProductId == productId);
            var reserved = await PendingReceiptVoidStock.GetReservedQuantityAsync(_unitOfWork, request.WarehouseId, productId);
            var available = Math.Max((stock?.Quantity ?? 0) - reserved, 0);
            if (stock is null || available < requested)
            {
                errors.Add($"Stock insuficiente de \"{existingProducts.GetValueOrDefault(productId) ?? $"#{productId}"}\": el lote pide {requested:0.##} y hay {available:0.##}.");
                continue;
            }
            stockByProduct[productId] = stock;
        }
        if (errors.Count > 0)
        {
            return BatchError(errors);
        }

        var createdById = _currentUserService.AppUserId;
        // Un solo candado para todo el lote: el N° del lote y los N° de las salidas salen seguidos.
        await IssueNumbering.Lock.WaitAsync();
        try
        {
            var (batchId, batchNumber) = await _unitOfWork.InTransactionAsync(async () =>
            {
                var batch = new IssueBatch
                {
                    // El N° del lote es el siguiente N° de salida del mes; lo comparten todas sus filas.
                    MonthlyNumber = await IssueNumbering.NextAsync(_unitOfWork, issueDate),
                    BatchDate = issueDate,
                    WarehouseId = request.WarehouseId,
                    IssueTypeId = batchTypeId,
                    Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = createdById
                };
                await _unitOfWork.Repository<IssueBatch>().AddAsync(batch);
                await _unitOfWork.SaveChangesAsync();
                var number = IssueNumbering.Format(batch.MonthlyNumber, batch.BatchDate, batch.IssueBatchId);

                foreach (var pastor in pastors)
                {
                    var issue = new Issue
                    {
                        IssueTypeId = batchTypeId,
                        WarehouseId = request.WarehouseId,
                        WarehousePeriodId = warehousePeriod!.WarehousePeriodId,
                        ClientId = pastor.ClientId,
                        IssueDate = issueDate,
                        PrintTypeId = request.PrintTypeId,
                        Description = batch.Description,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = createdById,
                        IssueBatchId = batch.IssueBatchId,
                        MonthlyNumber = batch.MonthlyNumber
                    };
                    await _unitOfWork.Repository<Issue>().AddAsync(issue);
                    await _unitOfWork.SaveChangesAsync();
                    await DocumentReviews.RecordEnteredAsync(_unitOfWork, DocumentReviews.Issue, issue.IssueId, createdById);

                    var total = 0m;
                    foreach (var line in pastor.Details)
                    {
                        var stock = stockByProduct[line.ProductId];
                        var lineTotal = line.Quantity * stock.AverageCost;
                        total += lineTotal;
                        await _unitOfWork.Repository<IssueDetail>().AddAsync(new IssueDetail
                        {
                            IssueId = issue.IssueId,
                            ProductId = line.ProductId,
                            Quantity = line.Quantity,
                            UnitCost = stock.AverageCost,
                            TotalCost = lineTotal
                        });
                        stock.Quantity -= line.Quantity;
                    }

                    if (request.SendToAccountsReceivable)
                    {
                        await _unitOfWork.Repository<AccountReceivable>().AddAsync(new AccountReceivable
                        {
                            IssueId = issue.IssueId,
                            ClientId = pastor.ClientId,
                            TotalAmount = total,
                            OutstandingBalance = total,
                            PaymentType = request.PaymentType!,
                            PaymentDetail = request.PaymentDetail,
                            DueDate = request.DueDate,
                            Status = "pending",
                            CreatedAt = DateTime.UtcNow,
                            CreatedById = createdById
                        });
                    }
                    await _unitOfWork.SaveChangesAsync();
                }

                foreach (var stock in stockByProduct.Values)
                {
                    _unitOfWork.Repository<Stock>().Update(stock);
                }
                await _unitOfWork.SaveChangesAsync();
                return (batch.IssueBatchId, number);
            });

            return new ResponsePost
            {
                Id = batchId,
                Messages = [new Message { Type = MessageType.Success, Description = $"N° {batchNumber} registrado para {pastors.Count} pastores." }],
                StatusCode = HttpStatusCode.Created
            };
        }
        finally
        {
            IssueNumbering.Lock.Release();
        }
    }

    private static ResponsePost BatchError(IEnumerable<string> errors) => new()
    {
        Id = 0,
        Messages = errors.Select(e => new Message { Type = MessageType.Error, Description = e }).ToArray(),
        StatusCode = HttpStatusCode.BadRequest
    };
}
