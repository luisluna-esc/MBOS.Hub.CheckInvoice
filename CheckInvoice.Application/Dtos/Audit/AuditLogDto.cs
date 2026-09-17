namespace CheckInvoice.Application.Dtos.Audit;

public class AuditLogDto
{
    public long AuditLogId { get; set; }
    public long? AppUserId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public long? RecordId { get; set; }
    public DateTime LogDate { get; set; }
    public string Action { get; set; } = string.Empty;
}