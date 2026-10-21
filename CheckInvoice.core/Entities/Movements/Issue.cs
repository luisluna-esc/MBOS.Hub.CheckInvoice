namespace CheckInvoice.core.Entities.Movements;

public class Issue
{
    public long IssueId { get; set; }
    public long? IssueTypeId { get; set; }
    public long? WarehouseId { get; set; }
    public long? WarehousePeriodId { get; set; }
    public long? ClientId { get; set; }
    public string? Complement { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    // N° de salida dentro del mes de la fecha ("S 15-01-26"), como la nota de ingreso.
    public int? MonthlyNumber { get; set; }
    public long? PrintTypeId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedById { get; set; }
    public bool IsVoided { get; set; }
    // Revisión: "digitado" -> "verificado" -> "aprobado"; reabrir vuelve a "digitado" y sube la versión.
    public string ReviewStatus { get; set; } = "digitado";
    public int ReviewVersion { get; set; } = 1;
    // Salida por lote: el lote al que pertenece (null si es una salida suelta).
    public long? IssueBatchId { get; set; }
}