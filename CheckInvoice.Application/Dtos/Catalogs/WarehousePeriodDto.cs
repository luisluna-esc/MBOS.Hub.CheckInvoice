namespace CheckInvoice.Application.Dtos.Catalogs;

public class WarehousePeriodDto
{
    public long WarehousePeriodId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
    public long? ClosedById { get; set; }
}