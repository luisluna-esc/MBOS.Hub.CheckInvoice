namespace CheckInvoice.core.QueryFilters.Reports;

public class KardexByProductReportQueryFilter
{
    /// <summary>Sin producto: todos los productos del almacén (switch "Todos los productos").</summary>
    public long? ProductId { get; set; }
    public long WarehouseId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}
