namespace CheckInvoice.Application.Dtos.Movements;

public class IssueDetailDto
{
    public long IssueDetailId { get; set; }
    public long IssueId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}