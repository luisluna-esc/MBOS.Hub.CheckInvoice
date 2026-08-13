namespace CheckInvoice.core.Entities.Movements;

public class InventoryCount
{
    public long InventoryCountId { get; set; }
    public long WarehouseId { get; set; }
    public long? SourceCountId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CountDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "closed";
    public long? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? ClosedById { get; set; }
    public DateTime? ClosedAt { get; set; }
}