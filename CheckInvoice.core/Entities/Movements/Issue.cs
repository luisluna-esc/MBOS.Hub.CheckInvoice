namespace CheckInvoice.core.Entities.Movements;

public class Issue
{
    public long IssueId { get; set; }
    public long? IssueTypeId { get; set; }
    public long? WarehouseId { get; set; }
    public long? WarehousePeriodId { get; set; }
    public long? ClientId { get; set; }
    public string? Complement { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public long? PrintTypeId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedById { get; set; }
    public bool IsVoided { get; set; }
}