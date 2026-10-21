namespace CheckInvoice.Application.Dtos.Movements;

public class IssueDto
{
    public long IssueId { get; set; }
    // N° de salida del mes ("S 15-01-26"); en salidas anteriores a la numeración, el N° interno.
    public string? NoteNumber { get; set; }
    public long? IssueTypeId { get; set; }
    public long? WarehouseId { get; set; }
    public long? WarehousePeriodId { get; set; }
    public long? ClientId { get; set; }
    public string? Complement { get; set; }
    public DateTime IssueDate { get; set; }
    public long? PrintTypeId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedById { get; set; }
    public string? CreatedByFullName { get; set; }
    public bool HasPendingChangeRequest { get; set; }
    public decimal Total { get; set; }
    public bool IsVoided { get; set; }
    public bool HasPendingVoidRequest { get; set; }
    public string? VoidReasonName { get; set; }
    public string? VoidDetail { get; set; }
    // Revisión: digitado, verificado o aprobado.
    public string? ReviewStatus { get; set; }
    /// <summary>Salida por lote: el lote al que pertenece (null si es una salida suelta).</summary>
    public long? IssueBatchId { get; set; }
}