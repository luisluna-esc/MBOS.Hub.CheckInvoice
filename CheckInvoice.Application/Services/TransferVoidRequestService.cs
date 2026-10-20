using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Entities.Warehouses;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Anulación de Transferencias, con el mismo circuito que las de Entradas y Salidas: se pide con
// un motivo y el Contador la aprueba o la rechaza. Al aprobar, los productos vuelven del almacén
// de destino al de origen con el mismo costo con el que salieron. Si en el destino ya se usaron,
// no se puede anular; mientras se decide, esas unidades quedan apartadas en el destino.
public class TransferVoidRequestService : ITransferVoidRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public TransferVoidRequestService(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAll(string? status, long? transferId)
    {
        var query = _unitOfWork.Repository<TransferVoidRequest>().Query();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(v => v.Status == status);
        }
        if (transferId.HasValue)
        {
            query = query.Where(v => v.TransferId == transferId.Value);
        }

        var rows = await (
            from v in query
            join t in _unitOfWork.Repository<Transfer>().Query() on v.TransferId equals t.TransferId
            join reason in _unitOfWork.Repository<VoidReason>().Query() on v.VoidReasonId equals reason.VoidReasonId into reasons
            from reason in reasons.DefaultIfEmpty()
            join ws in _unitOfWork.Repository<Warehouse>().Query() on t.SourceWarehouseId equals ws.WarehouseId into sources
            from ws in sources.DefaultIfEmpty()
            join wd in _unitOfWork.Repository<Warehouse>().Query() on t.DestinationWarehouseId equals wd.WarehouseId into destinations
            from wd in destinations.DefaultIfEmpty()
            join ru in _unitOfWork.Repository<AppUser>().Query() on v.RequestedBy equals ru.AppUserId into requesters
            from ru in requesters.DefaultIfEmpty()
            join vu in _unitOfWork.Repository<AppUser>().Query() on v.ReviewedBy equals vu.AppUserId into reviewers
            from vu in reviewers.DefaultIfEmpty()
            orderby v.RequestedAt descending
            select new TransferVoidRequestDto
            {
                TransferVoidRequestId = v.TransferVoidRequestId,
                TransferId = v.TransferId,
                TransferDate = t.TransferDate,
                SourceWarehouseName = ws != null ? ws.Name : null,
                DestinationWarehouseName = wd != null ? wd.Name : null,
                VoidReasonName = reason != null ? reason.Name : null,
                Detail = v.Detail,
                Status = v.Status,
                RequestedByName = ru != null ? ru.FirstName + " " + ru.LastName : null,
                RequestedAt = v.RequestedAt,
                ReviewedByName = vu != null ? vu.FirstName + " " + vu.LastName : null,
                ReviewedAt = v.ReviewedAt,
                ReviewNotes = v.ReviewNotes
            }).Take(200).ToListAsync();

        var ids = rows.Select(r => r.TransferId).Distinct().ToList();
        var totals = await _unitOfWork.Repository<TransferDetail>().Query()
            .Where(d => ids.Contains(d.TransferId))
            .GroupBy(d => d.TransferId)
            .Select(g => new { TransferId = g.Key, Total = g.Sum(d => d.TotalSalePrice ?? 0) })
            .ToDictionaryAsync(x => x.TransferId, x => x.Total);
        foreach (var row in rows)
        {
            row.Total = totals.GetValueOrDefault(row.TransferId);
        }

        return new ResponseGetObject { Data = rows, Messages = [], StatusCode = HttpStatusCode.OK };
    }

    public async Task<ResponsePost> RequestVoid(TransferVoidRequestCreateDto dto)
    {
        var transfer = await _unitOfWork.Repository<Transfer>().GetByIdAsync(dto.TransferId);
        if (transfer is null || transfer.IsVoided)
        {
            return Error(0, "La transferencia ya no existe o ya fue anulada.");
        }
        if (!transfer.SourceWarehouseId.HasValue || !transfer.DestinationWarehouseId.HasValue)
        {
            return Error(0, "Esta transferencia no tiene almacén de origen y destino: no se puede anular.");
        }
        if (!await _unitOfWork.Repository<VoidReason>().Query().AnyAsync(r => r.VoidReasonId == dto.VoidReasonId))
        {
            return Error(0, "Elige el motivo de la anulación.");
        }
        if (await _unitOfWork.Repository<TransferVoidRequest>().Query().AnyAsync(v => v.TransferId == dto.TransferId && v.Status == "pending"))
        {
            return Error(0, "Ya hay una solicitud de anulación pendiente para esta transferencia.");
        }

        var missing = await MissingInDestinationAsync(transfer, excludeTransferId: transfer.TransferId);
        if (missing.Count > 0)
        {
            return Error(0, $"No se puede anular: en el almacén de destino ya se usó {string.Join(", ", missing)} y no alcanza para devolverlo al origen.");
        }

        var request = new TransferVoidRequest
        {
            TransferId = dto.TransferId,
            VoidReasonId = dto.VoidReasonId,
            Detail = string.IsNullOrWhiteSpace(dto.Detail) ? null : dto.Detail.Trim(),
            Status = "pending",
            RequestedBy = _currentUserService.AppUserId!.Value,
            RequestedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<TransferVoidRequest>().AddAsync(request);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = request.TransferVoidRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Solicitud de anulación enviada. El Contador debe aprobarla." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> Approve(long id, string? notes)
    {
        var request = await _unitOfWork.Repository<TransferVoidRequest>().GetByIdAsync(id);
        if (request is null)
        {
            return Error(id, "No se encontró la solicitud.", HttpStatusCode.NotFound);
        }
        if (request.Status != "pending")
        {
            return Error(id, "La solicitud ya fue revisada.");
        }

        var transfer = await _unitOfWork.Repository<Transfer>().GetByIdAsync(request.TransferId);
        if (transfer is null || transfer.IsVoided || !transfer.SourceWarehouseId.HasValue || !transfer.DestinationWarehouseId.HasValue)
        {
            return Error(id, "La transferencia ya no existe o ya fue anulada.");
        }

        // Se vuelve a revisar: desde que se pidió pudo haber nuevas salidas en el destino.
        var missing = await MissingInDestinationAsync(transfer, excludeTransferId: transfer.TransferId);
        if (missing.Count > 0)
        {
            return Error(id, $"No se puede anular: en el almacén de destino ya se usó {string.Join(", ", missing)} y no alcanza para devolverlo al origen.");
        }

        var stockRepository = _unitOfWork.Repository<Stock>();
        var lines = await _unitOfWork.Repository<TransferDetail>().Query().Where(d => d.TransferId == transfer.TransferId).ToListAsync();
        foreach (var group in lines.GroupBy(l => l.ProductId))
        {
            var quantity = group.Sum(l => l.Quantity);
            var value = group.Sum(l => l.TotalSalePrice ?? 0);

            var destination = await stockRepository.Query()
                .FirstAsync(s => s.WarehouseId == transfer.DestinationWarehouseId && s.ProductId == group.Key);
            destination.Quantity -= quantity;
            stockRepository.Update(destination);

            // Vuelve al origen con el costo con el que salió (promedio ponderado con lo que hay).
            var source = await stockRepository.Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == transfer.SourceWarehouseId && s.ProductId == group.Key);
            if (source is null)
            {
                await stockRepository.AddAsync(new Stock
                {
                    WarehouseId = transfer.SourceWarehouseId.Value,
                    ProductId = group.Key,
                    Quantity = quantity,
                    AverageCost = quantity > 0 ? value / quantity : 0
                });
            }
            else
            {
                var newQuantity = source.Quantity + quantity;
                source.AverageCost = newQuantity > 0 ? (source.Quantity * source.AverageCost + value) / newQuantity : source.AverageCost;
                source.Quantity = newQuantity;
                stockRepository.Update(source);
            }
        }

        transfer.IsVoided = true;
        _unitOfWork.Repository<Transfer>().Update(transfer);

        request.Status = "approved";
        request.ReviewedBy = _currentUserService.AppUserId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        _unitOfWork.Repository<TransferVoidRequest>().Update(request);

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = id,
            Messages = [new Message { Type = MessageType.Success, Description = "Transferencia anulada. Los productos volvieron al almacén de origen." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> Reject(long id, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return Error(id, "Escribe el motivo del rechazo.");
        }
        var request = await _unitOfWork.Repository<TransferVoidRequest>().GetByIdAsync(id);
        if (request is null)
        {
            return Error(id, "No se encontró la solicitud.", HttpStatusCode.NotFound);
        }
        if (request.Status != "pending")
        {
            return Error(id, "La solicitud ya fue revisada.");
        }

        request.Status = "rejected";
        request.ReviewedBy = _currentUserService.AppUserId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNotes = notes.Trim();
        _unitOfWork.Repository<TransferVoidRequest>().Update(request);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = id,
            Messages = [new Message { Type = MessageType.Success, Description = "Solicitud de anulación rechazada." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    // Productos que ya no están completos en el destino (descontando lo apartado por otras
    // anulaciones o correcciones pendientes).
    private async Task<List<string>> MissingInDestinationAsync(Transfer transfer, long excludeTransferId)
    {
        var missing = new List<string>();
        var warehouseId = transfer.DestinationWarehouseId!.Value;
        var lines = await _unitOfWork.Repository<TransferDetail>().Query()
            .Where(d => d.TransferId == transfer.TransferId)
            .GroupBy(d => d.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(d => d.Quantity) })
            .ToListAsync();

        foreach (var line in lines)
        {
            var stock = await _unitOfWork.Repository<Stock>().Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.ProductId == line.ProductId);
            var reserved = await PendingReceiptVoidStock.GetReservedQuantityAsync(_unitOfWork, warehouseId, line.ProductId, excludeTransferId: excludeTransferId);
            if ((stock?.Quantity ?? 0) - reserved < line.Quantity)
            {
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(line.ProductId);
                missing.Add(product?.Name ?? $"#{line.ProductId}");
            }
        }
        return missing;
    }

    private static ResponsePost Error(long id, string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest) => new()
    {
        Id = id,
        Messages = [new Message { Type = MessageType.Error, Description = message }],
        StatusCode = statusCode
    };
}
