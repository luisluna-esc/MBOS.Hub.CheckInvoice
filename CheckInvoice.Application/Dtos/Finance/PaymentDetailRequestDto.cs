namespace CheckInvoice.Application.Dtos.Finance;

// Monto del depósito asignado a un producto (línea de la salida) de la cuenta por cobrar.
public class PaymentDetailRequestDto
{
    public long IssueDetailId { get; set; }
    public decimal Amount { get; set; }
}
