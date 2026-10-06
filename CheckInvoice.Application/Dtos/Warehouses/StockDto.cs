namespace CheckInvoice.Application.Dtos.Warehouses;

public class StockDto
{
    public long StockId { get; set; }
    public long WarehouseId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    // Unidades apartadas por entradas con anulación pendiente: no se pueden sacar hasta que se resuelva.
    public decimal ReservedQuantity { get; set; }
}