namespace CheckInvoice.Application.Dtos.Movements;

public class InventoryCountDetailDto
{
    public long InventoryCountDetailId { get; set; }
    public long InventoryCountId { get; set; }
    public long ProductId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal? UnitValue { get; set; }
    public decimal? PhysicalQuantity { get; set; }
    public decimal? Difference { get; set; }
    public string? Notes { get; set; }
}