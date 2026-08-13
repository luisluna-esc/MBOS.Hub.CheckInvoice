namespace CheckInvoice.core.Entities.Movements;

public class Deposit
{
    public long DepositId { get; set; }
    public long? ClientId { get; set; }
    public long? ProductId { get; set; }
    public long? ShipmentId { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateOnly DepositDate { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}