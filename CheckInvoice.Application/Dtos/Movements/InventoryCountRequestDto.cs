namespace CheckInvoice.Application.Dtos.Movements;

public class InventoryCountRequestDto
{
    public long WarehouseId { get; set; }
    public long? SourceCountId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<InventoryCountDetailRequestDto> Details { get; set; } = [];
}

public class InventoryCountDetailRequestDto
{
    public long ProductId { get; set; }
    public decimal PhysicalQuantity { get; set; }
    public string? Notes { get; set; }
}
