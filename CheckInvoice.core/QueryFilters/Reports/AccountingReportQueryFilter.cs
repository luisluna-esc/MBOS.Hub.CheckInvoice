namespace CheckInvoice.core.QueryFilters.Reports;

public class AccountingReportQueryFilter
{
    public long? WarehouseId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    /// <summary>Solo en entradas: compras, donaciones, devoluciones, ajustes...</summary>
    public long? ReceiptTypeId { get; set; }
}
