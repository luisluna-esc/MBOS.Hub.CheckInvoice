namespace CheckInvoice.core.QueryFilters.Reports;

public class TransfersReportQueryFilter
{
    public long? SourceWarehouseId { get; set; }
    public long? DestinationWarehouseId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    /// <summary>"updated": con la firma de aprobación del Contador; "outdated": sin ella.</summary>
    public string? ReviewState { get; set; }
}
