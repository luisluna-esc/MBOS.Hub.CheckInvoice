namespace CheckInvoice.core.Entities.Movements;

public class ReceiptDetail
{
    public long ReceiptDetailId { get; set; }
    public long ReceiptId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    // Precio de compra por unidad. Con retención el costo es menor (ver ReceiptCosting); con
    // factura, y en devoluciones y ajustes, precio y costo son iguales.
    public decimal? UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? WorkOrder { get; set; }
    public string? Detail { get; set; }
}