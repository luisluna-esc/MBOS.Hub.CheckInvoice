namespace CheckInvoice.core.Entities.Movements;

public class Discount
{
    public long DiscountId { get; set; }
    public long? ClientId { get; set; }
    public string SourceTable { get; set; } = string.Empty;
    public long SourceId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public DateOnly DiscountDate { get; set; }
    public string Status { get; set; } = "pending";
}