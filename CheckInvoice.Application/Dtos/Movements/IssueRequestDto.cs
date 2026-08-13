namespace CheckInvoice.Application.Dtos.Movements;

public class IssueRequestDto
{
    public long? IssueTypeId { get; set; }
    public long WarehouseId { get; set; }
    public long? WarehousePeriodId { get; set; }
    public long? ClientId { get; set; }
    public string? Complement { get; set; }
    public long? PrintTypeId { get; set; }
    public string? Description { get; set; }
    public bool SendToAccountsReceivable { get; set; }
    public string? PaymentType { get; set; }
    public DateOnly? DueDate { get; set; }
    public List<IssueDetailRequestDto> Details { get; set; } = [];
}

public class IssueDetailRequestDto
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
}