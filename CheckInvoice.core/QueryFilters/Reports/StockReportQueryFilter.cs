namespace CheckInvoice.core.QueryFilters.Reports;

public class StockReportQueryFilter
{
    public long? WarehouseId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}
