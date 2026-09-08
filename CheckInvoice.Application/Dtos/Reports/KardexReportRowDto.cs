namespace CheckInvoice.Application.Dtos.Reports;

/// <summary>
/// Una fila del reporte "Kardex Físico Valorado" (sp_get_kardex_report en
/// Scrips/storedProcedures.sql): saldo inicial, entradas, salidas y saldo final de un
/// producto en el período filtrado, valorizado al costo promedio ponderado.
/// </summary>
public class KardexReportRowDto
{
    public long ProductId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? SubDepartmentId { get; set; }
    public string? SubDepartmentName { get; set; }
    public decimal OpeningQuantity { get; set; }
    public decimal OpeningValue { get; set; }
    public decimal ReceiptsQuantity { get; set; }
    public decimal ReceiptsValue { get; set; }
    public decimal IssuesQuantity { get; set; }
    public decimal IssuesValue { get; set; }
    public decimal ClosingQuantity { get; set; }
    public decimal ClosingValue { get; set; }
}
