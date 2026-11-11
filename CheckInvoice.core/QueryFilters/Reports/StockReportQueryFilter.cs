namespace CheckInvoice.core.QueryFilters.Reports;

public class StockReportQueryFilter
{
    public long? WarehouseId { get; set; }
    /// <summary>Opcional: sin fecha de inicio es el saldo acumulado; con ella, el movimiento neto del periodo.</summary>
    public DateOnly? DateFrom { get; set; }
    /// <summary>Fecha fin (sin fecha: hoy).</summary>
    public DateOnly? DateTo { get; set; }
    /// <summary>Estado: "updated" (Actualizado) cuenta solo lo actualizado; sin valor (Desactualizado), todo.</summary>
    public string? ReviewState { get; set; }
}
