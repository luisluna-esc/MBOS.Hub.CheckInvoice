namespace CheckInvoice.core.Entities.Finance;

// Parte de un depósito asignada a un producto (línea) de la salida de la cuenta por cobrar.
public class PaymentDetail
{
    public long PaymentDetailId { get; set; }
    public long PaymentId { get; set; }
    public long IssueDetailId { get; set; }
    public long ProductId { get; set; }
    public decimal Amount { get; set; }
}
