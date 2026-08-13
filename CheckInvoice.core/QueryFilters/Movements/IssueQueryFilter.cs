namespace CheckInvoice.core.QueryFilters.Movements;

public class IssueQueryFilter
{
    public long? IssueId { get; set; }
    public long? ClientId { get; set; }
    public long? WarehouseId { get; set; }
    public long? IssueTypeId { get; set; }
}