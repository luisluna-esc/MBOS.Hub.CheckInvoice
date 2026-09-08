namespace CheckInvoice.core.Entities.Movements;

public class Receipt
{
    public long ReceiptId { get; set; }
    public long? SupplierId { get; set; }
    public string? TaxId { get; set; }
    public long? WarehouseId { get; set; }
    public long? WarehousePeriodId { get; set; }
    public long? ReceiptTypeId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Description { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public decimal? InvoiceTotal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedById { get; set; }
    public bool IsVoided { get; set; }
}