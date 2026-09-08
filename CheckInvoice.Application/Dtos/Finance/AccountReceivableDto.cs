namespace CheckInvoice.Application.Dtos.Finance;

public class AccountReceivableDto
{
    public long AccountReceivableId { get; set; }
    public long? IssueId { get; set; }
    public DateTime? IssueDate { get; set; }
    public long? ClientId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string? PaymentDetail { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long? CreatedById { get; set; }
}