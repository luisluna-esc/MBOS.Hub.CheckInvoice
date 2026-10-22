namespace CheckInvoice.core.QueryFilters.Reports;

public class KardexReportQueryFilter
{
    public long? WarehouseId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    /// <summary>Estado: "updated" (Actualizado) cuenta solo lo actualizado; sin valor (Desactualizado), todo.</summary>
    public string? ReviewState { get; set; }
}
