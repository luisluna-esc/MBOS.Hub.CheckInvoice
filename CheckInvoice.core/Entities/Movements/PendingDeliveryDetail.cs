namespace CheckInvoice.core.Entities.Movements;

public class PendingDeliveryDetail
{
    public long PendingDeliveryDetailId { get; set; }
    public long PendingDeliveryId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
}
