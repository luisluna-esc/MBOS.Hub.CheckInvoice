namespace CheckInvoice.Application.Dtos.Movements;

// Entrada por compra. El período de almacén no se envía: sale del mes de la Fecha de ingreso.
public class ReceiptRequestDto
{
    public long? SupplierId { get; set; }
    // NIT con factura, CI con retención.
    public string? TaxId { get; set; }
    public long WarehouseId { get; set; }
    public long? ReceiptTypeId { get; set; }
    // N° de factura (con factura, obligatorio) o N° de documento (con retención, opcional).
    public string? InvoiceNumber { get; set; }
    public string? Description { get; set; }
    // Valor total de la compra: debe coincidir con la suma de cantidad x precio de las líneas.
    public decimal? InvoiceTotal { get; set; }
    // Fecha de ingreso al almacén.
    public DateTime? IssueDate { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public List<ReceiptDetailRequestDto> Details { get; set; } = [];
}

public class ReceiptDetailRequestDto
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    // Precio de compra; el costo unitario lo calcula el servidor (ReceiptCosting).
    public decimal UnitPrice { get; set; }
}
