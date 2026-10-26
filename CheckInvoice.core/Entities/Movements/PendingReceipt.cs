namespace CheckInvoice.core.Entities.Movements;

// Nota de ingreso sin factura: registra lo que llegó al almacén mientras la factura no llega. No
// toca el stock ni ningún reporte (por eso va en su propia tabla); al unirle la factura se
// convierte en una entrada real ("Compras") con la fecha de recepción. N° por mes ("NP 3-10-26").
public class PendingReceipt
{
    public long PendingReceiptId { get; set; }
    public int MonthlyNumber { get; set; }
    public long WarehouseId { get; set; }
    // Quién lo envió (opcional): el proveedor definitivo es el de la factura.
    public long? SupplierId { get; set; }
    // Fecha en que llegó el material: es la fecha con la que entra al validarse.
    public DateTime ReceivedDate { get; set; }
    public string? Description { get; set; }
    // "pendiente" -> "validado" (con factura) o "anulado".
    public string Status { get; set; } = PendingReceiptStatuses.Pending;
    // Entrada real creada al validar.
    public long? ReceiptId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedById { get; set; }
    // Cuándo y quién la validó o la anuló.
    public DateTime? ClosedAt { get; set; }
    public long? ClosedById { get; set; }
}

public static class PendingReceiptStatuses
{
    public const string Pending = "pendiente";
    public const string Validated = "validado";
    public const string Voided = "anulado";
}
