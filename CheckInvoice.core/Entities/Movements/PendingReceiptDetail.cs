namespace CheckInvoice.core.Entities.Movements;

public class PendingReceiptDetail
{
    public long PendingReceiptDetailId { get; set; }
    public long PendingReceiptId { get; set; }
    public long ProductId { get; set; }
    // Cantidad recibida; al validar se corrige con la de la factura si hace falta.
    public decimal Quantity { get; set; }
}
