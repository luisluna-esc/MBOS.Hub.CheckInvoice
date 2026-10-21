namespace CheckInvoice.core.Entities.Movements;

// Salida por lote: agrupa una salida por pastor, todas con la misma fecha, almacén, tipo y
// condiciones de cobro. N° de lote por mes ("L 3-10-26").
public class IssueBatch
{
    public long IssueBatchId { get; set; }
    public int MonthlyNumber { get; set; }
    public DateTime BatchDate { get; set; }
    public long WarehouseId { get; set; }
    public long? IssueTypeId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedById { get; set; }
}
