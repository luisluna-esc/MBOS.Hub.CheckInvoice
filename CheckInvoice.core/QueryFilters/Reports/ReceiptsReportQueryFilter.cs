namespace CheckInvoice.core.QueryFilters.Reports;

public class ReceiptsReportQueryFilter
{
    public long? WarehouseId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    /// <summary>"updated" (verificado o aprobado) u "outdated" (sin verificar).</summary>
    public string? ReviewState { get; set; }
}
