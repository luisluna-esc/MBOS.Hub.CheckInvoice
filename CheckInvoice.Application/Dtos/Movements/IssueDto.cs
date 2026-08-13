namespace CheckInvoice.Application.Dtos.Movements;

public class IssueDto
{
    public long IssueId { get; set; }
    public long? IssueTypeId { get; set; }
    public long? WarehouseId { get; set; }
    public long? WarehousePeriodId { get; set; }
    public long? ClientId { get; set; }
    public string? Complement { get; set; }
    public DateTime IssueDate { get; set; }
    public long? PrintTypeId { get; set; }
    public string? Description { get; set; }
}