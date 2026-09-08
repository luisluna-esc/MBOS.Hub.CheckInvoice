namespace CheckInvoice.core.QueryFilters.Reports;

public class PastorFieldReportQueryFilter
{
    public long ClientId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}
