namespace CheckInvoice.core.QueryFilters.Movements;

public class IssueVoidRequestQueryFilter
{
    public long? IssueVoidRequestId { get; set; }
    public long? IssueId { get; set; }
    public string? Status { get; set; }
    public long? RequestedBy { get; set; }
}
