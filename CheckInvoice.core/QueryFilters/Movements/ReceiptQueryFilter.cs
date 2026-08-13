namespace CheckInvoice.core.QueryFilters.Movements;

public class ReceiptQueryFilter
{
    public long? ReceiptId { get; set; }
    public long? SupplierId { get; set; }
    public long? WarehouseId { get; set; }
    public long? ReceiptTypeId { get; set; }
    public string? InvoiceNumber { get; set; }
}