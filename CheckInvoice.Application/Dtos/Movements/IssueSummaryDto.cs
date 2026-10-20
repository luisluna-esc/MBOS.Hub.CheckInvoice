namespace CheckInvoice.Application.Dtos.Movements;

// Una salida con sus productos y fotos, para la página "Ver con fotos" (la abre el QR del
// comprobante) y para cargar la corrección. Igual que el resumen de la nota de ingreso.
public class IssueSummaryDto
{
    public long IssueId { get; set; }
    public string NoteNumber { get; set; } = string.Empty;
    public long? IssueTypeId { get; set; }
    public string? IssueTypeName { get; set; }
    public long? ClientId { get; set; }
    public string? ClientName { get; set; }
    public long? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public DateTime IssueDate { get; set; }
    public string? Description { get; set; }
    public bool IsVoided { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ReceiptSummaryLineDto> Lines { get; set; } = [];
}
