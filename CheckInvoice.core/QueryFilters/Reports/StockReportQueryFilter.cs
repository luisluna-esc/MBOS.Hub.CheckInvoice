namespace CheckInvoice.core.QueryFilters.Reports;

public class StockReportQueryFilter
{
    public long? WarehouseId { get; set; }
    /// <summary>Fecha del saldo (sin fecha: hoy). Son saldos, así que no hay fecha de inicio.</summary>
    public DateOnly? DateTo { get; set; }
    /// <summary>Estado: "updated" (Actualizado) cuenta solo lo actualizado; sin valor (Desactualizado), todo.</summary>
    public string? ReviewState { get; set; }
}
