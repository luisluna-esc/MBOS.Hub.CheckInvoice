namespace CheckInvoice.core.Entities.Governance;

// Historial de revisión de un documento de almacén (Entrada, Salida o Transferencia), como el
// "Historial de Actualizaciones" de los asientos contables: quién lo digitó, quién lo verificó,
// quién lo aprobó y quién lo reabrió y por qué. Cada reapertura empieza una versión nueva.
public class DocumentReview
{
    public long DocumentReviewId { get; set; }
    // "receipt", "issue" o "transfer".
    public string DocumentType { get; set; } = string.Empty;
    public long DocumentId { get; set; }
    public int Version { get; set; } = 1;
    // "digitado", "verificado", "aprobado", "rechazado" o "reabierto".
    public string Action { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
    // Huella del contenido firmado (SHA-256). Null en "digitado": esa fila no es una firma.
    public string? ContentHash { get; set; }
}
