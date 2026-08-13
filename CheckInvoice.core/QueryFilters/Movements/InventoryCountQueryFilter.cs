namespace CheckInvoice.core.QueryFilters.Movements;

public class InventoryCountQueryFilter
{
    public long? InventoryCountId { get; set; }
    public long? WarehouseId { get; set; }
    public string? Status { get; set; }
}