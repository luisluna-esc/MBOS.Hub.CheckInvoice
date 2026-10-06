namespace CheckInvoice.Application.Dtos.Finance;

// Lo que muestra el diálogo de Caja al registrar un depósito: cada producto de la salida con
// cuánto vale, cuánto ya se pagó y cuánto falta.
public class PaymentLinesDto
{
    public long AccountReceivableId { get; set; }
    public decimal OutstandingBalance { get; set; }
    // false cuando la cuenta se creó a mano, sin salida: ahí el depósito es un monto general.
    public bool HasProducts { get; set; }
    public List<PaymentLineDto> Lines { get; set; } = [];
}

public class PaymentLineDto
{
    public long IssueDetailId { get; set; }
    public long ProductId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
}
