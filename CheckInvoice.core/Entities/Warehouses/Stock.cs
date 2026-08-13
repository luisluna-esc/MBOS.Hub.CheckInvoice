namespace CheckInvoice.core.Entities.Warehouses;

public class Stock
{
    public long StockId { get; set; }
    public long WarehouseId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
}