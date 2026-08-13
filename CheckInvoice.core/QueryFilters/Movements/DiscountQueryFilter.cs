namespace CheckInvoice.core.QueryFilters.Movements;

public class DiscountQueryFilter
{
    public long? DiscountId { get; set; }
    public long? ClientId { get; set; }
    public string? SourceTable { get; set; }
    public string? Status { get; set; }
}