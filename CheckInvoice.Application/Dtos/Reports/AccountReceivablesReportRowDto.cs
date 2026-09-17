namespace CheckInvoice.Application.Dtos.Reports;

public class AccountReceivablesReportRowDto
{
    public long AccountReceivableId { get; set; }
    public long? IssueId { get; set; }
    public DateTime? IssueDate { get; set; }
    public long? ClientId { get; set; }
    public string? ClientName { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
