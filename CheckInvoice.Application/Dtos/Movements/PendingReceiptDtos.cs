namespace CheckInvoice.Application.Dtos.Movements;

// Nota de ingreso sin factura (ver PendingReceipt).
public class PendingReceiptRequestDto
{
    public long WarehouseId { get; set; }
    public long? SupplierId { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public string? Description { get; set; }
    public List<PendingLineRequestDto> Details { get; set; } = [];
}

public class PendingLineRequestDto
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
}

// Entrega de material de una nota pendiente.
public class PendingDeliveryRequestDto
{
    public DateTime? DeliveryDate { get; set; }
    public long? IssueTypeId { get; set; }
    public long? ClientId { get; set; }
    public string? Description { get; set; }
    public bool SendToAccountsReceivable { get; set; }
    public string? PaymentType { get; set; }
    public string? PaymentDetail { get; set; }
    public DateOnly? DueDate { get; set; }
    public List<PendingLineRequestDto> Details { get; set; } = [];
}

// Unir la factura: una o varias notas del mismo almacén con los datos de la compra y el precio
// (y la cantidad corregida, si la factura dice otra) de cada línea.
public class PendingReceiptValidateRequestDto
{
    public List<long> PendingReceiptIds { get; set; } = [];
    public long? ReceiptTypeId { get; set; }
    public long? SupplierId { get; set; }
    public string? TaxId { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? InvoiceTotal { get; set; }
    public List<PendingInvoiceLineDto> Lines { get; set; } = [];
}

public class PendingInvoiceLineDto
{
    public long PendingReceiptId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class PendingReceiptDto
{
    public long PendingReceiptId { get; set; }
    public string NoteNumber { get; set; } = string.Empty;
    public long WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public DateTime ReceivedDate { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? ReceiptId { get; set; }
    public string? ReceiptNoteNumber { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ClosedByName { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<PendingReceiptLineDto> Lines { get; set; } = [];
    public List<PendingDeliveryDto> Deliveries { get; set; } = [];
}

public class PendingReceiptLineDto
{
    public long ProductId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? UnitMeasure { get; set; }
    public long? ImageVersion { get; set; }
    public decimal Received { get; set; }
    // Entregado en salidas pendientes (sin las anuladas) y lo que queda en el almacén.
    public decimal Delivered { get; set; }
    public decimal Remaining { get; set; }
}

public class PendingDeliveryDto
{
    public long PendingDeliveryId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public long? IssueTypeId { get; set; }
    public string? IssueTypeName { get; set; }
    public long? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? Description { get; set; }
    public bool SendToAccountsReceivable { get; set; }
    public bool IsVoided { get; set; }
    public long? IssueId { get; set; }
    public string? IssueNumber { get; set; }
    public string? CreatedByName { get; set; }
    public List<PendingDeliveryLineDto> Lines { get; set; } = [];
}

public class PendingDeliveryLineDto
{
    public long ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}
