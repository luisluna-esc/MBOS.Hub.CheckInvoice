namespace CheckInvoice.Application.Dtos.Movements;

public class ReceiptVoidRequestDto
{
    public long ReceiptVoidRequestId { get; set; }
    public long ReceiptId { get; set; }
    public DateTime ReceiptDate { get; set; }
    public string? SupplierName { get; set; }
    public string? WarehouseName { get; set; }
    public decimal ReceiptTotal { get; set; }
    public long VoidReasonId { get; set; }
    public string VoidReasonName { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string Status { get; set; } = string.Empty;
    public long RequestedBy { get; set; }
    public string? RequestedByFullName { get; set; }
    public DateTime RequestedAt { get; set; }
    public long? ReviewedBy { get; set; }
    public string? ReviewedByFullName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}
