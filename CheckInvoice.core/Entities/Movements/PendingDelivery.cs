namespace CheckInvoice.core.Entities.Movements;

// Entrega de material de una nota sin factura: tampoco toca el stock ni los reportes. Al validar
// la nota se convierte en una salida real con su fecha, tipo, cliente y cuenta por cobrar.
public class PendingDelivery
{
    public long PendingDeliveryId { get; set; }
    public long PendingReceiptId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public long? IssueTypeId { get; set; }
    public long? ClientId { get; set; }
    public string? Description { get; set; }
    public bool SendToAccountsReceivable { get; set; }
    public string? PaymentType { get; set; }
    public string? PaymentDetail { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsVoided { get; set; }
    // Salida real creada al validar la nota.
    public long? IssueId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedById { get; set; }
}
