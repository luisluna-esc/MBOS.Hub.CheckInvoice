namespace CheckInvoice.core.Entities.Movements;

public class ReceiptVoidRequest
{
    public long ReceiptVoidRequestId { get; set; }
    public long ReceiptId { get; set; }
    public long VoidReasonId { get; set; }
    public string? Detail { get; set; }
    public string Status { get; set; } = "pending";
    public long RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public long? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}
