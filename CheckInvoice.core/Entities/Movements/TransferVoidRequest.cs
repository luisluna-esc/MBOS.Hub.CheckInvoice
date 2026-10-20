namespace CheckInvoice.core.Entities.Movements;

// Solicitud de anulación de una Transferencia (la aprueba o rechaza el Contador).
public class TransferVoidRequest
{
    public long TransferVoidRequestId { get; set; }
    public long TransferId { get; set; }
    public long VoidReasonId { get; set; }
    public string? Detail { get; set; }
    public string Status { get; set; } = "pending";
    public long RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public long? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}
