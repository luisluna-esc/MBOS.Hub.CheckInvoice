namespace CheckInvoice.core.QueryFilters.Movements;

public class IssueQueryFilter
{
    public long? IssueId { get; set; }
    // Búsqueda por N° de salida tal como sale impreso ("S 15-10-26", "15-10" o el N° interno).
    public string? NoteNumber { get; set; }
    public long? ClientId { get; set; }
    public long? WarehouseId { get; set; }
    public long? IssueTypeId { get; set; }
    public string? ClientName { get; set; }
    /// <summary>"updated": con la firma de aprobación del Contador; "outdated": sin ella.</summary>
    public string? ReviewState { get; set; }
}
