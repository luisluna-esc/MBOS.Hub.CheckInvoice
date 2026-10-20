namespace CheckInvoice.core.Entities.Movements;

public class Receipt
{
    public long ReceiptId { get; set; }
    public long? SupplierId { get; set; }
    public string? TaxId { get; set; }
    public long? WarehouseId { get; set; }
    public long? WarehousePeriodId { get; set; }
    public long? ReceiptTypeId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Description { get; set; }
    // Fecha de ingreso al almacén: la que usan el período, el Kardex y los reportes.
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    // Fecha de compra (de la factura o del recibo). Solo en Compras; puede ser anterior al ingreso.
    public DateTime? PurchaseDate { get; set; }
    // N° de nota dentro del mes de la fecha de ingreso: la nota 15 de enero se imprime "15-01".
    // Como se registra fuera de orden (una entrada de septiembre cargada en octubre), cada mes
    // lleva su propia cuenta.
    public int? MonthlyNumber { get; set; }
    public decimal? InvoiceTotal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedById { get; set; }
    public bool IsVoided { get; set; }
    public long? RelatedIssueId { get; set; }
    // Revisión: "digitado" -> "verificado" -> "aprobado"; reabrir vuelve a "digitado" y sube la versión.
    public string ReviewStatus { get; set; } = "digitado";
    public int ReviewVersion { get; set; } = 1;
}