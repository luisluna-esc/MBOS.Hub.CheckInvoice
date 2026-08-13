namespace CheckInvoice.core.Entities.Movements;

public class TransferDetail
{
    public long TransferDetailId { get; set; }
    public long TransferId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalSalePrice { get; set; }
}