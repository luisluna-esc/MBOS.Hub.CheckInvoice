namespace CheckInvoice.Application.Dtos.Reports;

/// <summary>
/// Una fila del reporte "Stock - Almacén" (sp_get_stock_report en
/// Scrips/storedProcedures.sql): movimiento neto de un producto en el período
/// filtrado, valorizado al costo promedio ponderado de sus Entradas del período.
/// </summary>
public class StockReportRowDto
{
    public long ProductId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? UnitMeasure { get; set; }
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public long? SubDepartmentId { get; set; }
    public string? SubDepartmentName { get; set; }
    public decimal NetQuantity { get; set; }
    public decimal UnitValue { get; set; }
    public decimal TotalValue { get; set; }
}
