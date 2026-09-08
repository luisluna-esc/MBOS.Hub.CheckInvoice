namespace CheckInvoice.core.QueryFilters.Movements;

public class TransferQueryFilter
{
    public long? TransferId { get; set; }
    public long? SourceWarehouseId { get; set; }
    public long? DestinationWarehouseId { get; set; }
    public long? SenderUserId { get; set; }
    public long? ReceiverUserId { get; set; }
    public long? ReceiverClientId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public bool? IsApproved { get; set; }
}