namespace CheckInvoice.Application.Dtos.Movements;

public class InventoryCountDto
{
    public long InventoryCountId { get; set; }
    public long WarehouseId { get; set; }
    public long? SourceCountId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CountDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? ClosedById { get; set; }
    public DateTime? ClosedAt { get; set; }
}