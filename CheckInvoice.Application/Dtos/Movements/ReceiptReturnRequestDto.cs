namespace CheckInvoice.Application.Dtos.Movements;

public class ReceiptReturnRequestDto
{
    public long IssueId { get; set; }
    public long ReceiptTypeId { get; set; }
    public string? Description { get; set; }
    public List<ReceiptReturnLineRequestDto> Lines { get; set; } = [];
}

public class ReceiptReturnLineRequestDto
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
}
