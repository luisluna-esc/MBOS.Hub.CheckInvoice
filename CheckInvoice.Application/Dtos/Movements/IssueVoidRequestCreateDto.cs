namespace CheckInvoice.Application.Dtos.Movements;

public class IssueVoidRequestCreateDto
{
    public long IssueId { get; set; }
    public long VoidReasonId { get; set; }
    public string? Detail { get; set; }
}
