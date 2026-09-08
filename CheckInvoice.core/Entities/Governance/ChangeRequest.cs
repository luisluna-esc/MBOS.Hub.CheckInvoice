namespace CheckInvoice.core.Entities.Governance;

public class ChangeRequest
{
    public long ChangeRequestId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public long RecordId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? CurrentData { get; set; }
    public string? ProposedData { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public long RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public long? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}
