namespace CheckInvoice.core.QueryFilters.Movements;

public class ReceiptQueryFilter
{
    public long? ReceiptId { get; set; }
    public long? SupplierId { get; set; }
    public long? WarehouseId { get; set; }
    public long? ReceiptTypeId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SupplierName { get; set; }
    // N° de nota tal como sale impreso: "15-01" o "15-01-2026".
    public string? NoteNumber { get; set; }
    /// <summary>"updated": con la firma de aprobación del Contador; "outdated": sin ella.</summary>
    public string? ReviewState { get; set; }
}
