namespace CheckInvoice.core.QueryFilters.Reports;

public class AccountReceivablesReportQueryFilter
{
    public long? ClientId { get; set; }
    public string? Status { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}
