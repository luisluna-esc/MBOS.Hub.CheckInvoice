namespace CheckInvoice.core.QueryFilters.Finance;

public class AccountReceivableQueryFilter
{
    public long? AccountReceivableId { get; set; }
    public long? ClientId { get; set; }
    public string? PaymentType { get; set; }
    public string? Status { get; set; }
}