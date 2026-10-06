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
    // Reparto por producto. Si la cuenta tiene salida es obligatorio y Amount se calcula como su
    // suma; si la cuenta se creó a mano (sin salida) va vacío y se usa Amount.
    public List<PaymentDetailRequestDto> Details { get; set; } = [];
}