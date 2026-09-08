namespace CheckInvoice.Application.Dtos.Reports;

/// <summary>
/// Una fila del reporte "Ingresos de Almacén" (sp_get_receipts_report en
/// Scrips/storedProcedures.sql): una línea de una Entrada individual en el período
/// filtrado. A diferencia de Kardex/Stock, no se agrupa por producto — el mismo
/// producto puede repetirse una vez por cada Entrada en la que aparece.
/// </summary>
public class ReceiptsReportRowDto
{
    public long ReceiptId { get; set; }
    public DateTime ReceiptDate { get; set; }
    public long ProductId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? UnitMeasure { get; set; }
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public long? SubDepartmentId { get; set; }
    public string? SubDepartmentName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}
