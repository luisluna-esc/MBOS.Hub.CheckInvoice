namespace CheckInvoice.Application.Dtos.Movements;

// Nota de ingreso completa para la página que abre el QR.
public class ReceiptSummaryDto
{
    public long ReceiptId { get; set; }
    public string NoteNumber { get; set; } = string.Empty;
    public long? ReceiptTypeId { get; set; }
    public long? SupplierId { get; set; }
    public long? WarehouseId { get; set; }
    public long? RelatedIssueId { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? ReceiptTypeName { get; set; }
    public string? WarehouseName { get; set; }
    public string? SupplierName { get; set; }
    public string? TaxId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Description { get; set; }
    public decimal? InvoiceTotal { get; set; }
    public bool IsVoided { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ReceiptSummaryLineDto> Lines { get; set; } = [];
}

public class ReceiptSummaryLineDto
{
    public long ProductId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? UnitMeasure { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    // Con foto: versión para armar la URL de la imagen. Sin foto: null.
    public long? ImageVersion { get; set; }
}
