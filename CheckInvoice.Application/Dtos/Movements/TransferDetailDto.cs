namespace CheckInvoice.Application.Dtos.Movements;

public class TransferDetailDto
{
    public long TransferDetailId { get; set; }
    public long TransferId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalSalePrice { get; set; }
}