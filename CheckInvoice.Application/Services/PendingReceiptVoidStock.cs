using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Una Entrada con anulación pendiente aparta las unidades que sumó: si una Salida o
// Transferencia las consumiera mientras el Contador revisa, la anulación ya no se podría
// aprobar. Por eso el stock disponible para sacar es Quantity menos lo apartado aquí.
internal static class PendingReceiptVoidStock
{
    public static async Task<decimal> GetReservedQuantityAsync(
        IUnitOfWork unitOfWork, long warehouseId, long productId, long? excludeReceiptId = null)
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
            .SumAsync();

        return reserved ?? 0;
    }
}
