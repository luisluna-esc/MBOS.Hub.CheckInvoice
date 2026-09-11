namespace CheckInvoice.Application.Dtos.Movements;

public class ReturnableIssueLineDto
{
    public long ProductId { get; set; }
    public decimal QuantityIssued { get; set; }
    public decimal QuantityAlreadyReturned { get; set; }
    public decimal QuantityReturnable { get; set; }
    public decimal UnitCost { get; set; }
}
