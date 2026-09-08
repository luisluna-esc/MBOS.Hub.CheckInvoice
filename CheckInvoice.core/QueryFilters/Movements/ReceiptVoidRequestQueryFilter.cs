namespace CheckInvoice.core.QueryFilters.Movements;

public class ReceiptVoidRequestQueryFilter
{
    public long? ReceiptVoidRequestId { get; set; }
    public long? ReceiptId { get; set; }
    public string? Status { get; set; }
    public long? RequestedBy { get; set; }
}
