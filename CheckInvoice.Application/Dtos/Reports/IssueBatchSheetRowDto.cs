namespace CheckInvoice.Application.Dtos.Reports;

/// <summary>Una fila de sp_get_issue_batch_sheet: un material que saca un pastor en una salida por lote.</summary>
public class IssueBatchSheetRowDto
{
    public long IssueBatchId { get; set; }
    public int MonthlyNumber { get; set; }
    public DateTime BatchDate { get; set; }
    public string? WarehouseName { get; set; }
    public string? Description { get; set; }
    public string? CreatedByName { get; set; }
    public long IssueId { get; set; }
    public bool IsVoided { get; set; }
    public string? ClientName { get; set; }
    public string? TaxId { get; set; }
    public string? DistrictName { get; set; }
    public long ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public decimal Quantity { get; set; }
}
