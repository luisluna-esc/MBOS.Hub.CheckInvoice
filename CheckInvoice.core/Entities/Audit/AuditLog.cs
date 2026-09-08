namespace CheckInvoice.core.Entities.Audit;

public class AuditLog
{
    public long AuditLogId { get; set; }
    public long? AppUserId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public long? RecordId { get; set; }
    public DateTime LogDate { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
}