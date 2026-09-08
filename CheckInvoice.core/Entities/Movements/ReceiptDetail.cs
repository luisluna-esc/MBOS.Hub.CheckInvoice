namespace CheckInvoice.core.Entities.Movements;

public class ReceiptDetail
{
    public long ReceiptDetailId { get; set; }
    public long ReceiptId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? WorkOrder { get; set; }
    public string? Detail { get; set; }
}