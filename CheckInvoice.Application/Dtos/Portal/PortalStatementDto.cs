using CheckInvoice.Application.Dtos.Finance;

namespace CheckInvoice.Application.Dtos.Portal;

// Estado de cuenta del Pastor (Mi Cuenta): cada cuenta por cobrar con sus productos (cuánto
// vale, cuánto pagó y cuánto debe de cada uno) y los depósitos que Caja registró.
public class PortalStatementAccountDto
{
    public long AccountReceivableId { get; set; }
    public long? IssueId { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public List<PaymentLineDto> Lines { get; set; } = [];
    public List<PortalStatementPaymentDto> Payments { get; set; } = [];
}

public class PortalStatementPaymentDto
{
    public long PaymentId { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}
