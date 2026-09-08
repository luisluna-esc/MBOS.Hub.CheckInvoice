namespace CheckInvoice.Application.Dtos.Catalogs;

public class WarehouseDto
{
    public long WarehouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; }
}