namespace CheckInvoice.core.QueryFilters.Reports;

public class StockByDepartmentReportQueryFilter
{
    public long? WarehouseId { get; set; }
    public long? DepartmentId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}
