using System.Globalization;
using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Finance;
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
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Notas de ingreso sin factura. Mientras están pendientes, ni la nota ni sus entregas tocan el
// stock ni los reportes: viven en sus propias tablas. Al unir la factura, cada nota se convierte
// en una entrada real (Compras con Factura o con Retención) con la fecha de recepción, y cada
// entrega en una salida real con su fecha; recién ahí cuentan en el Kardex y en contabilidad.
public class PendingReceiptService : IPendingReceiptService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly ICurrentUserService _currentUserService;

    public PendingReceiptService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAll(PaginationQueryFilter paginationQueryFilter, PendingReceiptQueryFilter filter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = Filtered(_unitOfWork, filter);
        var totalRecords = await query.CountAsync();
        var notes = await query
            .OrderByDescending(n => n.ReceivedDate).ThenByDescending(n => n.PendingReceiptId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<PendingReceiptDto>
            {
                Items = await LoadAsync(_unitOfWork, notes),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetById(long pendingReceiptId)
    {
        var note = await _unitOfWork.Repository<PendingReceipt>().GetByIdAsync(pendingReceiptId);
        if (note is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró la nota." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        return new ResponseGetObject
        {
            Data = (await LoadAsync(_unitOfWork, [note]))[0],
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> Insert(PendingReceiptRequestDto request)
    {
        var errors = new List<string>();
        if (!await _unitOfWork.Repository<Warehouse>().Query().AnyAsync(w => w.WarehouseId == request.WarehouseId))
        {
            errors.Add("Debes seleccionar un almacén.");
        }
        if (request.SupplierId.HasValue &&
            !await _unitOfWork.Repository<Supplier>().Query().AnyAsync(s => s.SupplierId == request.SupplierId.Value))
        {
            errors.Add("El proveedor seleccionado no existe.");
        }
        if ((request.Description?.Length ?? 0) > 255)
        {
            errors.Add("La observación no puede superar los 255 caracteres.");
        }

        var receivedDate = request.ReceivedDate?.Date ?? BoliviaTime.Today;
        if (!MovementDates.IsWithinEntryWindow(receivedDate))
        {
            errors.Add(MovementDates.EntryWindowMessage("fecha de recepción"));
        }

        errors.AddRange(await LineErrorsAsync(request.Details, "La nota"));
        if (errors.Count > 0)
        {
            return Error(errors);
        }

        var note = new PendingReceipt
        {
            WarehouseId = request.WarehouseId,
            SupplierId = request.SupplierId,
            ReceivedDate = receivedDate,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserService.AppUserId
        };

        await PendingReceiptNumbering.Lock.WaitAsync();
        try
        {
            await _unitOfWork.InTransactionAsync(async () =>
            {
                note.MonthlyNumber = await PendingReceiptNumbering.NextAsync(_unitOfWork, receivedDate);
                await _unitOfWork.Repository<PendingReceipt>().AddAsync(note);
                await _unitOfWork.SaveChangesAsync();
                foreach (var line in request.Details)
                {
                    await _unitOfWork.Repository<PendingReceiptDetail>().AddAsync(new PendingReceiptDetail
                    {
                        PendingReceiptId = note.PendingReceiptId,
                        ProductId = line.ProductId,
                        Quantity = line.Quantity
                    });
                }
                await _unitOfWork.SaveChangesAsync();
                return true;
            });
        }
        finally
        {
            PendingReceiptNumbering.Lock.Release();
        }

        var number = PendingReceiptNumbering.Format(note.MonthlyNumber, note.ReceivedDate);
        return Success(note.PendingReceiptId, $"Nota {number} registrada. Queda pendiente hasta unirle la factura.");
    }

    public async Task<ResponsePost> Void(long pendingReceiptId)
    {
        var note = await _unitOfWork.Repository<PendingReceipt>().GetByIdAsync(pendingReceiptId);
        if (note is null || note.Status != PendingReceiptStatuses.Pending)
        {
            return Error(["Solo se puede anular una nota que sigue pendiente."]);
        }
        if (await _unitOfWork.Repository<PendingDelivery>().Query().AnyAsync(d => d.PendingReceiptId == pendingReceiptId && !d.IsVoided))
        {
            return Error(["La nota tiene entregas: anúlalas primero."]);
        }

        note.Status = PendingReceiptStatuses.Voided;
        note.ClosedAt = DateTime.UtcNow;
        note.ClosedById = _currentUserService.AppUserId;
        _unitOfWork.Repository<PendingReceipt>().Update(note);
        await _unitOfWork.SaveChangesAsync();
        return Success(note.PendingReceiptId, "Nota anulada.");
    }

    public async Task<ResponsePost> InsertDelivery(long pendingReceiptId, PendingDeliveryRequestDto request)
    {
        var note = await _unitOfWork.Repository<PendingReceipt>().GetByIdAsync(pendingReceiptId);
        if (note is null || note.Status != PendingReceiptStatuses.Pending)
        {
            return Error(["Solo se puede entregar material de una nota que sigue pendiente."]);
        }

        var errors = new List<string>();
        var deliveryDate = request.DeliveryDate?.Date ?? BoliviaTime.Today;
        if (deliveryDate < note.ReceivedDate.Date || deliveryDate > BoliviaTime.Today)
        {
            errors.Add($"La fecha de entrega debe estar entre el {note.ReceivedDate:dd/MM/yyyy} (recepción) y hoy.");
        }
        else if (!MovementDates.IsWithinEntryWindow(deliveryDate))
        {
            errors.Add(MovementDates.EntryWindowMessage("fecha de entrega"));
        }
        if (request.IssueTypeId.HasValue &&
            !await _unitOfWork.Repository<IssueType>().Query().AnyAsync(i => i.IssueTypeId == request.IssueTypeId.Value))
        {
            errors.Add("El tipo de salida seleccionado no existe.");
        }
        if (request.ClientId.HasValue &&
            !await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.ClientId == request.ClientId.Value))
        {
            errors.Add("El cliente seleccionado no existe.");
        }
        if (request.SendToAccountsReceivable)
        {
            if (!request.ClientId.HasValue)
            {
                errors.Add("Debes seleccionar un cliente para enviar a Cuentas por Cobrar.");
            }
            if (string.IsNullOrWhiteSpace(request.PaymentType))
            {
                errors.Add("Debes indicar el tipo de pago para enviar a Cuentas por Cobrar.");
            }
        }
        if ((request.Description?.Length ?? 0) > 255 || (request.PaymentType?.Length ?? 0) > 20 || (request.PaymentDetail?.Length ?? 0) > 255)
        {
            errors.Add("La observación o el detalle de pago es demasiado largo.");
        }

        errors.AddRange(await LineErrorsAsync(request.Details, "La entrega"));
        if (errors.Count == 0)
        {
            // Solo material de esta nota y hasta lo que queda de ella.
            var remaining = await RemainingAsync(pendingReceiptId);
            foreach (var line in request.Details)
            {
                if (!remaining.TryGetValue(line.ProductId, out var left))
                {
                    errors.Add($"El producto con ID {line.ProductId} no está en la nota.");
                }
                else if (line.Quantity > left)
                {
                    var name = await _unitOfWork.Repository<Product>().Query()
                        .Where(p => p.ProductId == line.ProductId).Select(p => p.Name).FirstOrDefaultAsync();
                    errors.Add($"\"{name}\": quedan {left:0.##} de la nota y se quieren entregar {line.Quantity:0.##}.");
                }
            }
        }
        if (errors.Count > 0)
        {
            return Error(errors);
        }

        var delivery = new PendingDelivery
        {
            PendingReceiptId = pendingReceiptId,
            DeliveryDate = deliveryDate,
            IssueTypeId = request.IssueTypeId,
            ClientId = request.ClientId,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SendToAccountsReceivable = request.SendToAccountsReceivable,
            PaymentType = request.SendToAccountsReceivable ? request.PaymentType : null,
            PaymentDetail = request.SendToAccountsReceivable ? request.PaymentDetail : null,
            DueDate = request.SendToAccountsReceivable ? request.DueDate : null,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserService.AppUserId
        };
        await _unitOfWork.InTransactionAsync(async () =>
        {
            await _unitOfWork.Repository<PendingDelivery>().AddAsync(delivery);
            await _unitOfWork.SaveChangesAsync();
            foreach (var line in request.Details)
            {
                await _unitOfWork.Repository<PendingDeliveryDetail>().AddAsync(new PendingDeliveryDetail
                {
                    PendingDeliveryId = delivery.PendingDeliveryId,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity
                });
            }
            await _unitOfWork.SaveChangesAsync();
            return true;
        });

        return Success(delivery.PendingDeliveryId, "Entrega registrada. Se volverá salida al unir la factura de la nota.");
    }

    public async Task<ResponsePost> VoidDelivery(long pendingDeliveryId)
    {
        var delivery = await _unitOfWork.Repository<PendingDelivery>().GetByIdAsync(pendingDeliveryId);
        if (delivery is null || delivery.IsVoided)
        {
            return Error(["No se encontró la entrega, o ya fue anulada."]);
        }
        var note = await _unitOfWork.Repository<PendingReceipt>().GetByIdAsync(delivery.PendingReceiptId);
        if (note?.Status != PendingReceiptStatuses.Pending)
        {
            return Error(["La nota ya no está pendiente: la entrega ya es una salida."]);
        }

        delivery.IsVoided = true;
        _unitOfWork.Repository<PendingDelivery>().Update(delivery);
        await _unitOfWork.SaveChangesAsync();
        return Success(delivery.PendingDeliveryId, "Entrega anulada.");
    }

    public async Task<ResponsePost> Validate(PendingReceiptValidateRequestDto request)
    {
        var errors = new List<string>();
        var noteIds = request.PendingReceiptIds.Distinct().ToList();
        var notes = await _unitOfWork.Repository<PendingReceipt>().Query()
            .Where(n => noteIds.Contains(n.PendingReceiptId))
            .OrderBy(n => n.ReceivedDate).ThenBy(n => n.PendingReceiptId)
            .ToListAsync();
        if (noteIds.Count == 0 || notes.Count != noteIds.Count)
        {
            return Error(["Elige las notas que cubre la factura."]);
        }
        if (notes.Any(n => n.Status != PendingReceiptStatuses.Pending))
        {
            return Error(["Alguna de las notas ya no está pendiente."]);
        }
        if (notes.Select(n => n.WarehouseId).Distinct().Count() > 1)
        {
            return Error(["Una factura solo puede cubrir notas del mismo almacén."]);
        }

        var receiptType = request.ReceiptTypeId.HasValue
            ? await _unitOfWork.Repository<ReceiptType>().GetByIdAsync(request.ReceiptTypeId.Value)
            : null;
        if (!ReceiptCosting.IsPurchaseType(receiptType?.Name))
        {
            errors.Add("Elige si la compra es con factura o con retención.");
        }
        var withRetention = receiptType?.Name == ReceiptCosting.PurchaseWithRetention;
        if (!request.SupplierId.HasValue ||
            !await _unitOfWork.Repository<Supplier>().Query().AnyAsync(s => s.SupplierId == request.SupplierId.Value))
        {
            errors.Add("Elige el proveedor de la factura.");
        }
        if (string.IsNullOrWhiteSpace(request.TaxId))
        {
            errors.Add(withRetention ? "El CI es obligatorio." : "El NIT es obligatorio.");
        }
        if (!withRetention && string.IsNullOrWhiteSpace(request.InvoiceNumber))
        {
            errors.Add("El número de factura es obligatorio.");
        }
        if (request.PurchaseDate is null)
        {
            errors.Add("La fecha de compra es obligatoria.");
        }
        else if (request.PurchaseDate.Value.Date > BoliviaTime.Today)
        {
            errors.Add("La fecha de compra no puede ser posterior a hoy.");
        }

        // Una línea por cada material de cada nota: precio y, si la factura dice otra, la cantidad.
        var details = await _unitOfWork.Repository<PendingReceiptDetail>().Query()
            .Where(d => noteIds.Contains(d.PendingReceiptId))
            .ToListAsync();
        var deliveries = await _unitOfWork.Repository<PendingDelivery>().Query()
            .Where(d => noteIds.Contains(d.PendingReceiptId) && !d.IsVoided)
            .OrderBy(d => d.DeliveryDate).ThenBy(d => d.PendingDeliveryId)
            .ToListAsync();
        var deliveryIds = deliveries.Select(d => d.PendingDeliveryId).ToList();
        var deliveryLines = await _unitOfWork.Repository<PendingDeliveryDetail>().Query()
            .Where(d => deliveryIds.Contains(d.PendingDeliveryId))
            .ToListAsync();
        var noteOfDelivery = deliveries.ToDictionary(d => d.PendingDeliveryId, d => d.PendingReceiptId);
        var delivered = deliveryLines
            .GroupBy(l => (noteOfDelivery[l.PendingDeliveryId], l.ProductId))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var productNames = await ProductNamesAsync(details.Select(d => d.ProductId));

        var lines = new Dictionary<(long, long), PendingInvoiceLineDto>();
        foreach (var line in request.Lines)
        {
            if (!lines.TryAdd((line.PendingReceiptId, line.ProductId), line))
            {
                errors.Add("Un material está repetido en la factura.");
            }
        }
        if (lines.Keys.Any(k => !details.Any(d => d.PendingReceiptId == k.Item1 && d.ProductId == k.Item2)))
        {
            errors.Add("La factura trae un material que no está en las notas elegidas.");
        }
        foreach (var detail in details)
        {
            var name = productNames.GetValueOrDefault(detail.ProductId) ?? $"#{detail.ProductId}";
            if (!lines.TryGetValue((detail.PendingReceiptId, detail.ProductId), out var line))
            {
                errors.Add($"Falta el precio de \"{name}\".");
                continue;
            }
            var alreadyDelivered = delivered.GetValueOrDefault((detail.PendingReceiptId, detail.ProductId));
            if (line.Quantity <= 0)
            {
                errors.Add($"\"{name}\": la cantidad debe ser mayor a cero.");
            }
            else if (line.Quantity < alreadyDelivered)
            {
                errors.Add($"\"{name}\": ya se entregaron {alreadyDelivered:0.##}, la cantidad no puede ser menor.");
            }
            if (line.UnitPrice < 0)
            {
                errors.Add($"\"{name}\": el precio no puede ser negativo.");
            }
        }

        var purchaseTotal = request.Lines.Sum(l => l.Quantity * l.UnitPrice);
        if (request.InvoiceTotal is null || Math.Abs(purchaseTotal - request.InvoiceTotal.Value) > 0.01m)
        {
            errors.Add($"La suma de los productos ({purchaseTotal:N2}) no coincide con el valor total de la compra ({request.InvoiceTotal ?? 0:N2}).");
        }

        // Entran con la fecha de recepción y salen con la de entrega: esos meses deben estar abiertos.
        var periods = new Dictionary<DateTime, WarehousePeriod>();
        foreach (var date in notes.Select(n => n.ReceivedDate.Date).Concat(deliveries.Select(d => d.DeliveryDate.Date)).Distinct())
        {
            var period = await MovementDates.GetOrCreatePeriodAsync(_unitOfWork, date);
            if (period.IsClosed)
            {
                errors.Add($"El período {period.Name} está cerrado: no se puede validar una nota o entrega del {date:dd/MM/yyyy}.");
            }
            periods[date] = period;
        }

        if (errors.Count > 0)
        {
            return Error(errors.Distinct());
        }

        var userId = _currentUserService.AppUserId;
        var warehouseId = notes[0].WarehouseId;
        var receiptNumbers = new List<string>();

        // Los dos candados a la vez: los N° de entrada y de salida salen seguidos y sin repetirse.
        await ReceiptNumbering.Lock.WaitAsync();
        await IssueNumbering.Lock.WaitAsync();
        try
        {
            await _unitOfWork.InTransactionAsync(async () =>
            {
                var stocks = new Dictionary<long, Stock>();
                foreach (var note in notes)
                {
                    var noteLines = details.Where(d => d.PendingReceiptId == note.PendingReceiptId).ToList();
                    var receipt = new Receipt
                    {
                        SupplierId = request.SupplierId,
                        TaxId = request.TaxId!.Trim(),
                        WarehouseId = warehouseId,
                        WarehousePeriodId = periods[note.ReceivedDate.Date].WarehousePeriodId,
                        ReceiptTypeId = request.ReceiptTypeId,
                        InvoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber) ? null : request.InvoiceNumber.Trim(),
                        Description = note.Description,
                        IssueDate = note.ReceivedDate.Date,
                        PurchaseDate = request.PurchaseDate!.Value.Date,
                        // Con varias notas, cada entrada lleva la parte de la factura que le toca.
                        InvoiceTotal = noteLines.Sum(d => lines[(note.PendingReceiptId, d.ProductId)].Quantity * lines[(note.PendingReceiptId, d.ProductId)].UnitPrice),
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = userId
                    };
                    receipt.MonthlyNumber = await ReceiptNumbering.NextAsync(_unitOfWork, receipt.IssueDate);
                    await _unitOfWork.Repository<Receipt>().AddAsync(receipt);
                    await _unitOfWork.SaveChangesAsync();
                    await DocumentReviews.RecordEnteredAsync(_unitOfWork, DocumentReviews.Receipt, receipt.ReceiptId, userId);
                    receiptNumbers.Add(ReceiptNumbering.Format(receipt.MonthlyNumber, receipt.IssueDate, receipt.ReceiptId));

                    foreach (var detail in noteLines)
                    {
                        var line = lines[(note.PendingReceiptId, detail.ProductId)];
                        var unitCost = ReceiptCosting.UnitCost(line.UnitPrice, withRetention);
                        await _unitOfWork.Repository<ReceiptDetail>().AddAsync(new ReceiptDetail
                        {
                            ReceiptId = receipt.ReceiptId,
                            ProductId = detail.ProductId,
                            Quantity = line.Quantity,
                            UnitPrice = line.UnitPrice,
                            UnitCost = unitCost,
                            TotalCost = ReceiptCosting.TotalCost(line.Quantity, line.UnitPrice, withRetention)
                        });
                        await IncreaseStockAsync(stocks, warehouseId, detail.ProductId, line.Quantity, unitCost);

                        // La nota queda con la cantidad de la factura.
                        detail.Quantity = line.Quantity;
                        _unitOfWork.Repository<PendingReceiptDetail>().Update(detail);
                    }

                    note.Status = PendingReceiptStatuses.Validated;
                    note.ReceiptId = receipt.ReceiptId;
                    note.ClosedAt = DateTime.UtcNow;
                    note.ClosedById = userId;
                    _unitOfWork.Repository<PendingReceipt>().Update(note);
                    await _unitOfWork.SaveChangesAsync();
                }

                // Las entregas, en orden de fecha, salen al costo promedio que dejó la factura.
                foreach (var delivery in deliveries)
                {
                    var date = delivery.DeliveryDate.Date;
                    var issue = new Issue
                    {
                        IssueTypeId = delivery.IssueTypeId,
                        WarehouseId = warehouseId,
                        WarehousePeriodId = periods[date].WarehousePeriodId,
                        ClientId = delivery.ClientId,
                        IssueDate = date,
                        Description = delivery.Description,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = delivery.CreatedById ?? userId
                    };
                    issue.MonthlyNumber = await IssueNumbering.NextAsync(_unitOfWork, date);
                    await _unitOfWork.Repository<Issue>().AddAsync(issue);
                    await _unitOfWork.SaveChangesAsync();
                    await DocumentReviews.RecordEnteredAsync(_unitOfWork, DocumentReviews.Issue, issue.IssueId, issue.CreatedById);

                    var total = 0m;
                    foreach (var line in deliveryLines.Where(l => l.PendingDeliveryId == delivery.PendingDeliveryId))
                    {
                        var stock = stocks[line.ProductId];
                        if (stock.Quantity < line.Quantity)
                        {
                            throw new InvalidOperationException(
                                $"Stock insuficiente de \"{productNames.GetValueOrDefault(line.ProductId)}\" para la entrega del {date:dd/MM/yyyy}.");
                        }
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

                    if (delivery.SendToAccountsReceivable)
                    {
                        await _unitOfWork.Repository<AccountReceivable>().AddAsync(new AccountReceivable
                        {
                            IssueId = issue.IssueId,
                            ClientId = delivery.ClientId,
                            TotalAmount = total,
                            OutstandingBalance = total,
                            PaymentType = delivery.PaymentType ?? string.Empty,
                            PaymentDetail = delivery.PaymentDetail,
                            DueDate = delivery.DueDate,
                            Status = "pending",
                            CreatedAt = DateTime.UtcNow,
                            CreatedById = issue.CreatedById
                        });
                    }

                    delivery.IssueId = issue.IssueId;
                    _unitOfWork.Repository<PendingDelivery>().Update(delivery);
                    await _unitOfWork.SaveChangesAsync();
                }

                foreach (var stock in stocks.Values)
                {
                    _unitOfWork.Repository<Stock>().Update(stock);
                }
                await _unitOfWork.SaveChangesAsync();
                return true;
            });
        }
        catch (InvalidOperationException ex)
        {
            return Error([ex.Message]);
        }
        finally
        {
            IssueNumbering.Lock.Release();
            ReceiptNumbering.Lock.Release();
        }

        var message = deliveries.Count > 0
            ? $"Factura unida: entrada {string.Join(", ", receiptNumbers)} y {deliveries.Count} salida(s). Ya cuentan en los reportes."
            : $"Factura unida: entrada {string.Join(", ", receiptNumbers)}. Ya cuenta en los reportes.";
        return Success(notes[0].ReceiptId ?? 0, message);
    }

    // Igual que ReceiptService.IncreaseStock: costo promedio ponderado; varias líneas del mismo
    // material acumulan sobre la misma fila.
    private async Task IncreaseStockAsync(Dictionary<long, Stock> stocks, long warehouseId, long productId, decimal quantity, decimal unitCost)
    {
        if (!stocks.TryGetValue(productId, out var stock))
        {
            stock = await _unitOfWork.Repository<Stock>().Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.ProductId == productId);
            if (stock is null)
            {
                stock = new Stock { WarehouseId = warehouseId, ProductId = productId, Quantity = 0, AverageCost = unitCost };
                await _unitOfWork.Repository<Stock>().AddAsync(stock);
            }
            stocks[productId] = stock;
        }

        var newQuantity = stock.Quantity + quantity;
        stock.AverageCost = newQuantity > 0
            ? ((stock.Quantity * stock.AverageCost) + (quantity * unitCost)) / newQuantity
            : unitCost;
        stock.Quantity = newQuantity;
    }

    private async Task<List<string>> LineErrorsAsync(List<PendingLineRequestDto> details, string what)
    {
        var errors = new List<string>();
        if (details.Count == 0)
        {
            errors.Add($"{what} debe tener al menos un material.");
        }
        if (details.Select(d => d.ProductId).Distinct().Count() != details.Count)
        {
            errors.Add("No se puede repetir el mismo material en más de una línea.");
        }
        if (details.Any(d => d.Quantity <= 0))
        {
            errors.Add("La cantidad debe ser mayor a cero.");
        }
        var productIds = details.Select(d => d.ProductId).Distinct().ToList();
        var found = await _unitOfWork.Repository<Product>().Query().CountAsync(p => productIds.Contains(p.ProductId));
        if (found != productIds.Count)
        {
            errors.Add("Algún material no existe.");
        }
        return errors;
    }

    // Lo que queda de cada material de la nota: lo recibido menos lo entregado (sin anuladas).
    private async Task<Dictionary<long, decimal>> RemainingAsync(long pendingReceiptId)
    {
        var received = await _unitOfWork.Repository<PendingReceiptDetail>().Query()
            .Where(d => d.PendingReceiptId == pendingReceiptId)
            .ToDictionaryAsync(d => d.ProductId, d => d.Quantity);
        var delivered = await (
            from l in _unitOfWork.Repository<PendingDeliveryDetail>().Query()
            join d in _unitOfWork.Repository<PendingDelivery>().Query() on l.PendingDeliveryId equals d.PendingDeliveryId
            where d.PendingReceiptId == pendingReceiptId && !d.IsVoided
            group l by l.ProductId into g
            select new { ProductId = g.Key, Quantity = g.Sum(l => l.Quantity) }).ToDictionaryAsync(x => x.ProductId, x => x.Quantity);
        return received.ToDictionary(r => r.Key, r => r.Value - delivered.GetValueOrDefault(r.Key));
    }

    private Task<Dictionary<long, string>> ProductNamesAsync(IEnumerable<long> productIds)
    {
        var ids = productIds.Distinct().ToList();
        return _unitOfWork.Repository<Product>().Query()
            .Where(p => ids.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => p.Name);
    }

    internal static IQueryable<PendingReceipt> Filtered(IUnitOfWork unitOfWork, PendingReceiptQueryFilter filter)
    {
        var query = unitOfWork.Repository<PendingReceipt>().Query();
        if (filter.WarehouseId.HasValue)
        {
            query = query.Where(n => n.WarehouseId == filter.WarehouseId.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(n => n.Status == filter.Status);
        }
        if (!string.IsNullOrWhiteSpace(filter.NoteNumber))
        {
            if (PendingReceiptNumbering.TryParse(filter.NoteNumber, out var number, out var month, out var year))
            {
                query = query.Where(n => n.MonthlyNumber == number && n.ReceivedDate.Month == month);
                if (year.HasValue)
                {
                    query = query.Where(n => n.ReceivedDate.Year == year.Value);
                }
            }
            else
            {
                query = query.Where(n => false);
            }
        }
        return query;
    }

    // Arma las notas con sus materiales (recibido, entregado, queda) y sus entregas. La usa
    // también el reporte de pendientes.
    internal static async Task<List<PendingReceiptDto>> LoadAsync(IUnitOfWork unitOfWork, List<PendingReceipt> notes)
    {
        var noteIds = notes.Select(n => n.PendingReceiptId).ToList();
        var details = await unitOfWork.Repository<PendingReceiptDetail>().Query()
            .Where(d => noteIds.Contains(d.PendingReceiptId))
            .OrderBy(d => d.PendingReceiptDetailId)
            .ToListAsync();
        var deliveries = await unitOfWork.Repository<PendingDelivery>().Query()
            .Where(d => noteIds.Contains(d.PendingReceiptId))
            .OrderBy(d => d.DeliveryDate).ThenBy(d => d.PendingDeliveryId)
            .ToListAsync();
        var deliveryIds = deliveries.Select(d => d.PendingDeliveryId).ToList();
        var deliveryLines = await unitOfWork.Repository<PendingDeliveryDetail>().Query()
            .Where(d => deliveryIds.Contains(d.PendingDeliveryId))
            .ToListAsync();

        var productIds = details.Select(d => d.ProductId).Distinct().ToList();
        var products = await (
            from p in unitOfWork.Repository<Product>().Query()
            join m in unitOfWork.Repository<MediaType>().Query() on p.MediaTypeId equals m.MediaTypeId into media
            from m in media.DefaultIfEmpty()
            join i in unitOfWork.Repository<ProductImage>().Query() on p.ProductId equals i.ProductId into images
            from i in images.DefaultIfEmpty()
            where productIds.Contains(p.ProductId)
            select new { p.ProductId, p.Code, p.Name, UnitMeasure = m != null ? m.Name : null, ImageVersion = i != null ? (long?)i.UpdatedAt.Ticks : null })
            .ToDictionaryAsync(p => p.ProductId);

        var warehouseIds = notes.Select(n => n.WarehouseId).Distinct().ToList();
        var warehouses = await unitOfWork.Repository<Warehouse>().Query()
            .Where(w => warehouseIds.Contains(w.WarehouseId))
            .ToDictionaryAsync(w => w.WarehouseId, w => w.Name);
        var supplierIds = notes.Where(n => n.SupplierId.HasValue).Select(n => n.SupplierId!.Value).Distinct().ToList();
        var suppliers = await (
            from s in unitOfWork.Repository<Supplier>().Query()
            join p in unitOfWork.Repository<Party>().Query() on s.PartyId equals p.PartyId
            where supplierIds.Contains(s.SupplierId)
            select new { s.SupplierId, Name = s.LegalName ?? p.Name }).ToDictionaryAsync(s => s.SupplierId, s => s.Name);
        var clientIds = deliveries.Where(d => d.ClientId.HasValue).Select(d => d.ClientId!.Value).Distinct().ToList();
        var clients = await (
            from c in unitOfWork.Repository<Client>().Query()
            join p in unitOfWork.Repository<Party>().Query() on c.PartyId equals p.PartyId
            where clientIds.Contains(c.ClientId)
            select new { c.ClientId, p.Name }).ToDictionaryAsync(c => c.ClientId, c => c.Name);
        var issueTypes = await unitOfWork.Repository<IssueType>().Query().ToDictionaryAsync(t => t.IssueTypeId, t => t.Name);
        var userIds = notes.SelectMany(n => new[] { n.CreatedById, n.ClosedById })
            .Concat(deliveries.Select(d => d.CreatedById))
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var users = await unitOfWork.Repository<AppUser>().Query()
            .Where(u => userIds.Contains(u.AppUserId))
            .ToDictionaryAsync(u => u.AppUserId, u => $"{u.FirstName} {u.LastName}".Trim());
        var receiptIds = notes.Where(n => n.ReceiptId.HasValue).Select(n => n.ReceiptId!.Value).ToList();
        var receipts = await unitOfWork.Repository<Receipt>().Query()
            .Where(r => receiptIds.Contains(r.ReceiptId))
            .ToDictionaryAsync(r => r.ReceiptId);
        var issueNumbers = await IssueNumbering.NumbersAsync(unitOfWork, deliveries.Where(d => d.IssueId.HasValue).Select(d => d.IssueId!.Value));

        string? UserName(long? id) => id.HasValue ? users.GetValueOrDefault(id.Value) : null;

        return notes.Select(note =>
        {
            var noteDeliveries = deliveries.Where(d => d.PendingReceiptId == note.PendingReceiptId).ToList();
            var activeIds = noteDeliveries.Where(d => !d.IsVoided).Select(d => d.PendingDeliveryId).ToHashSet();
            var receipt = note.ReceiptId.HasValue ? receipts.GetValueOrDefault(note.ReceiptId.Value) : null;
            return new PendingReceiptDto
            {
                PendingReceiptId = note.PendingReceiptId,
                NoteNumber = PendingReceiptNumbering.Format(note.MonthlyNumber, note.ReceivedDate),
                WarehouseId = note.WarehouseId,
                WarehouseName = warehouses.GetValueOrDefault(note.WarehouseId),
                SupplierId = note.SupplierId,
                SupplierName = note.SupplierId.HasValue ? suppliers.GetValueOrDefault(note.SupplierId.Value) : null,
                ReceivedDate = note.ReceivedDate,
                Description = note.Description,
                Status = note.Status,
                ReceiptId = note.ReceiptId,
                ReceiptNoteNumber = receipt is null ? null : ReceiptNumbering.Format(receipt.MonthlyNumber, receipt.IssueDate, receipt.ReceiptId),
                InvoiceNumber = receipt?.InvoiceNumber,
                CreatedByName = UserName(note.CreatedById),
                CreatedAt = note.CreatedAt,
                ClosedByName = UserName(note.ClosedById),
                ClosedAt = note.ClosedAt,
                Lines = details.Where(d => d.PendingReceiptId == note.PendingReceiptId).Select(d =>
                {
                    var product = products.GetValueOrDefault(d.ProductId);
                    var deliveredQuantity = deliveryLines
                        .Where(l => activeIds.Contains(l.PendingDeliveryId) && l.ProductId == d.ProductId)
                        .Sum(l => l.Quantity);
                    return new PendingReceiptLineDto
                    {
                        ProductId = d.ProductId,
                        Code = product?.Code,
                        Name = product?.Name ?? $"#{d.ProductId}",
                        UnitMeasure = product?.UnitMeasure,
                        ImageVersion = product?.ImageVersion,
                        Received = d.Quantity,
                        Delivered = deliveredQuantity,
                        Remaining = d.Quantity - deliveredQuantity
                    };
                }).ToList(),
                Deliveries = noteDeliveries.Select(d => new PendingDeliveryDto
                {
                    PendingDeliveryId = d.PendingDeliveryId,
                    DeliveryDate = d.DeliveryDate,
                    IssueTypeId = d.IssueTypeId,
                    IssueTypeName = d.IssueTypeId.HasValue ? issueTypes.GetValueOrDefault(d.IssueTypeId.Value) : null,
                    ClientId = d.ClientId,
                    ClientName = d.ClientId.HasValue ? clients.GetValueOrDefault(d.ClientId.Value) : null,
                    Description = d.Description,
                    SendToAccountsReceivable = d.SendToAccountsReceivable,
                    IsVoided = d.IsVoided,
                    IssueId = d.IssueId,
                    IssueNumber = d.IssueId.HasValue ? issueNumbers.GetValueOrDefault(d.IssueId.Value) : null,
                    CreatedByName = UserName(d.CreatedById),
                    Lines = deliveryLines.Where(l => l.PendingDeliveryId == d.PendingDeliveryId).Select(l => new PendingDeliveryLineDto
                    {
                        ProductId = l.ProductId,
                        Name = products.GetValueOrDefault(l.ProductId)?.Name ?? $"#{l.ProductId}",
                        Quantity = l.Quantity
                    }).ToList()
                }).ToList()
            };
        }).ToList();
    }

    private static ResponsePost Error(IEnumerable<string> errors) => new()
    {
        Id = 0,
        Messages = errors.Select(e => new Message { Type = MessageType.Error, Description = e }).ToArray(),
        StatusCode = HttpStatusCode.BadRequest
    };

    private static ResponsePost Success(long id, string message) => new()
    {
        Id = id,
        Messages = [new Message { Type = MessageType.Success, Description = message }],
        StatusCode = HttpStatusCode.Created
    };
}

// N° de la nota sin factura: correlativo dentro del mes de recepción, "NP 3-10-26".
internal static class PendingReceiptNumbering
{
    public static readonly SemaphoreSlim Lock = new(1, 1);

    public const string Prefix = "NP";

    public static async Task<int> NextAsync(IUnitOfWork unitOfWork, DateTime receivedDate)
    {
        var monthStart = new DateTime(receivedDate.Year, receivedDate.Month, 1);
        var nextMonth = monthStart.AddMonths(1);
        var last = await unitOfWork.Repository<PendingReceipt>().Query()
            .Where(n => n.ReceivedDate >= monthStart && n.ReceivedDate < nextMonth)
            .MaxAsync(n => (int?)n.MonthlyNumber);
        return (last ?? 0) + 1;
    }

    public static string Format(int monthlyNumber, DateTime receivedDate) =>
        $"{Prefix} {monthlyNumber}-{receivedDate.ToString("MM-yy", CultureInfo.InvariantCulture)}";

    // Acepta "NP 3-10-26", "3-10-26" o "3-10" (el mismo formato que las notas de ingreso).
    public static bool TryParse(string text, out int number, out int month, out int? year)
    {
        var clean = text.Trim();
        if (clean.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[Prefix.Length..].Trim();
        }
        return ReceiptNumbering.TryParse(clean, out number, out month, out year);
    }
}
