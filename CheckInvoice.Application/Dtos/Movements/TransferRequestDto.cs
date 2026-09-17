namespace CheckInvoice.Application.Dtos.Movements;

public class TransferRequestDto
{
    public long? SourceWarehouseId { get; set; }
    public long DestinationWarehouseId { get; set; }
    public long? SenderUserId { get; set; }
    public long? ReceiverUserId { get; set; }
    public long? ReceiverClientId { get; set; }
    public string? Notes { get; set; }
    public List<TransferDetailRequestDto> Details { get; set; } = [];
}

public class TransferDetailRequestDto
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
}