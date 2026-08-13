namespace CheckInvoice.Application.Dtos.Warehouses;

public class StockDto
{
    public long StockId { get; set; }
    public long WarehouseId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
}