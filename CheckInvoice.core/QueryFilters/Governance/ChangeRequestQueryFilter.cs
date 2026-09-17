namespace CheckInvoice.core.QueryFilters.Governance;

public class ChangeRequestQueryFilter
{
    public long? ChangeRequestId { get; set; }
    public string? TableName { get; set; }
    public long? RecordId { get; set; }
    public string? Status { get; set; }
    public long? RequestedBy { get; set; }
}
