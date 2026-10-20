namespace CheckInvoice.core.QueryFilters.Reports;

public class TransfersReportQueryFilter
{
    public long? SourceWarehouseId { get; set; }
    public long? DestinationWarehouseId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}
