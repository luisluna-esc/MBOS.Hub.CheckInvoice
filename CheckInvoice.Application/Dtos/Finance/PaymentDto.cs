namespace CheckInvoice.Application.Dtos.Finance;

public class PaymentDto
{
    public long PaymentId { get; set; }
    public long AccountReceivableId { get; set; }
    public long? InstallmentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public long? CreatedById { get; set; }
}