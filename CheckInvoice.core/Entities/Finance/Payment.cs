namespace CheckInvoice.core.Entities.Finance;

public class Payment
{
    public long PaymentId { get; set; }
    public long AccountReceivableId { get; set; }
    public long? InstallmentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public long? CreatedById { get; set; }
}