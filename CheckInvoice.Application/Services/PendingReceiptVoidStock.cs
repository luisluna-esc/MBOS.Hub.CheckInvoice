using CheckInvoice.core.Entities.Governance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Una Entrada con anulación pendiente aparta las unidades que sumó, una corrección pendiente
// aparta lo que baja y una Transferencia con anulación pendiente aparta en el destino lo que debe
// volver al origen: si una Salida o Transferencia las consumiera mientras el Contador revisa, ya
// no se podría aprobar. Por eso el stock disponible para sacar es Quantity menos lo apartado aquí.
internal static class PendingReceiptVoidStock
{
    public static async Task<decimal> GetReservedQuantityAsync(
        IUnitOfWork unitOfWork, long warehouseId, long productId, long? excludeReceiptId = null, long? excludeChangeRequestId = null,
        long? excludeTransferId = null)
    {
        var pendingReceiptIds = unitOfWork.Repository<ReceiptVoidRequest>().Query()
            .Where(r => r.Status == "pending")
            .Select(r => r.ReceiptId);

        var receipts = unitOfWork.Repository<Receipt>().Query()
            .Where(r => r.WarehouseId == warehouseId && !r.IsVoided && pendingReceiptIds.Contains(r.ReceiptId));

        if (excludeReceiptId.HasValue)
        {
            receipts = receipts.Where(r => r.ReceiptId != excludeReceiptId.Value);
        }

        var reserved = await unitOfWork.Repository<ReceiptDetail>().Query()
            .Where(d => d.ProductId == productId)
            .Join(receipts, d => d.ReceiptId, r => r.ReceiptId, (d, r) => (decimal?)d.Quantity)
            .SumAsync() ?? 0;

        var pendingEdits = await unitOfWork.Repository<ChangeRequest>().Query()
            .Where(c => c.TableName == ReceiptEditService.TableName && c.Status == "pending"
                        && (excludeChangeRequestId == null || c.ChangeRequestId != excludeChangeRequestId))
            .Select(c => c.ProposedData)
            .ToListAsync();
        foreach (var json in pendingEdits)
        {
            var proposal = ReceiptEditService.ReadProposal(json);
            if (proposal?.WarehouseId == warehouseId)
            {
                reserved += proposal.Decreases.Where(d => d.ProductId == productId).Sum(d => d.Quantity);
            }
        }

        // Anulación de transferencia pendiente: las unidades deben volver al origen.
        var pendingTransferIds = unitOfWork.Repository<TransferVoidRequest>().Query()
            .Where(v => v.Status == "pending" && (excludeTransferId == null || v.TransferId != excludeTransferId))
            .Select(v => v.TransferId);
        reserved += await unitOfWork.Repository<TransferDetail>().Query()
            .Where(d => d.ProductId == productId && pendingTransferIds.Contains(d.TransferId))
            .Join(unitOfWork.Repository<Transfer>().Query().Where(t => t.DestinationWarehouseId == warehouseId && !t.IsVoided),
                d => d.TransferId, t => t.TransferId, (d, t) => (decimal?)d.Quantity)
            .SumAsync() ?? 0;

        return reserved;
    }
}
