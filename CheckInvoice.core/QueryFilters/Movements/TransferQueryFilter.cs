namespace CheckInvoice.core.QueryFilters.Movements;

public class TransferQueryFilter
{
    public long? TransferId { get; set; }
    public long? SourceWarehouseId { get; set; }
    public long? DestinationWarehouseId { get; set; }
}