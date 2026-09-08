namespace CheckInvoice.core.QueryFilters.Finance;

public class PaymentQueryFilter
{
    public long? PaymentId { get; set; }
    public long? AccountReceivableId { get; set; }
    public string? PaymentMethod { get; set; }
}