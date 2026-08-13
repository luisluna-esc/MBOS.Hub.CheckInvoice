namespace CheckInvoice.core.QueryFilters.Movements;

public class DepositQueryFilter
{
    public long? DepositId { get; set; }
    public long? ClientId { get; set; }
    public long? ProductId { get; set; }
    public long? ShipmentId { get; set; }
    public string? ReceiptNumber { get; set; }
}