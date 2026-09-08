namespace CheckInvoice.Application.Dtos.Reports;

/// <summary>
/// Una fila del reporte "Levantamiento de Inventario" (sp_get_inventory_count_report en
/// Scrips/storedProcedures.sql): cantidad de sistema vs. física de un producto en el
/// levantamiento vigente (más reciente de su cadena) dentro del almacén y rango de
/// fechas filtrado.
/// </summary>
public class InventoryCountReportRowDto
{
    public long ProductId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? UnitMeasure { get; set; }
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public long? SubDepartmentId { get; set; }
    public string? SubDepartmentName { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitValue { get; set; }
    public decimal TotalValue { get; set; }
    public decimal? PhysicalQuantity { get; set; }
    public decimal? Difference { get; set; }
}
