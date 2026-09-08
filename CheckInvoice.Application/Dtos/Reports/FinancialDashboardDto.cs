namespace CheckInvoice.Application.Dtos.Reports;

public class FinancialDashboardSummaryDto
{
    public decimal TotalIssuesThisMonth { get; set; }
    public decimal TotalReceiptsThisMonth { get; set; }
    public decimal TotalPendingReceivable { get; set; }
    public decimal TotalPaymentsThisMonth { get; set; }
}

public class MonthlyTotalDto
{
    public string MonthLabel { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class ReceivableStatusSummaryDto
{
    public decimal PendingTotal { get; set; }
    public decimal PaidTotal { get; set; }
}

/// <summary>
/// Misma información que el PDF (GenerateFinancialDashboardPdf), pero como datos crudos en
/// JSON: el frontend dibuja los gráficos con la paleta de colores del sistema en vez de
/// mostrar una imagen generada en el backend.
/// </summary>
public class FinancialDashboardDataDto
{
    public FinancialDashboardSummaryDto Summary { get; set; } = new();
    public List<MonthlyTotalDto> IssuesByMonth { get; set; } = [];
    public ReceivableStatusSummaryDto ReceivableStatus { get; set; } = new();
}
