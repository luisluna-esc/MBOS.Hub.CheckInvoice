namespace CheckInvoice.core.QueryFilters.Finance;

public class InstallmentQueryFilter
{
    public long? InstallmentId { get; set; }
    public long? AccountReceivableId { get; set; }
    public string? Status { get; set; }
}