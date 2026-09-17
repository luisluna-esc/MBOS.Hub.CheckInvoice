namespace CheckInvoice.Application.Dtos.Movements;

public class ReceiptDto
{
    public long ReceiptId { get; set; }
    public long? SupplierId { get; set; }
    public string? TaxId { get; set; }
    public long? WarehouseId { get; set; }
    public long? WarehousePeriodId { get; set; }
    public long? ReceiptTypeId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Description { get; set; }
    public DateTime IssueDate { get; set; }
    public decimal? InvoiceTotal { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedById { get; set; }
    public string? CreatedByFullName { get; set; }
    public bool HasPendingChangeRequest { get; set; }
    public bool IsVoided { get; set; }
    public bool HasPendingVoidRequest { get; set; }
    public string? VoidReasonName { get; set; }
    public string? VoidDetail { get; set; }
    public long? RelatedIssueId { get; set; }
}