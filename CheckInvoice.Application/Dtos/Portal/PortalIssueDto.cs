namespace CheckInvoice.Application.Dtos.Portal;

public class PortalIssueDto
{
    public long IssueId { get; set; }
    public DateTime IssueDate { get; set; }
    public decimal Total { get; set; }
}
