namespace CheckInvoice.core.Entities.Movements;

public class IssueDetail
{
    public long IssueDetailId { get; set; }
    public long IssueId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}