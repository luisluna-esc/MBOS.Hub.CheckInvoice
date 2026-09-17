namespace CheckInvoice.Application.Dtos.Movements;

public class IssueVoidRequestDto
{
    public long IssueVoidRequestId { get; set; }
    public long IssueId { get; set; }
    public DateTime IssueDate { get; set; }
    public string? ClientName { get; set; }
    public string? WarehouseName { get; set; }
    public decimal IssueTotal { get; set; }
    public long VoidReasonId { get; set; }
    public string VoidReasonName { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string Status { get; set; } = string.Empty;
    public long RequestedBy { get; set; }
    public string? RequestedByFullName { get; set; }
    public DateTime RequestedAt { get; set; }
    public long? ReviewedBy { get; set; }
    public string? ReviewedByFullName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}
