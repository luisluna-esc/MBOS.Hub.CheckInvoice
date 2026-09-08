namespace CheckInvoice.core.Entities.Movements;

public class Transfer
{
    public long TransferId { get; set; }
    public long? SourceWarehouseId { get; set; }
    public long? DestinationWarehouseId { get; set; }
    public long? SenderUserId { get; set; }
    public long? ReceiverUserId { get; set; }
    public long? ReceiverClientId { get; set; }
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public bool IsApproved { get; set; } = true;
    public long? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
}