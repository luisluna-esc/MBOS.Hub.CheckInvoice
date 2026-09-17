namespace CheckInvoice.Application.Dtos.Catalogs;

public class ShipmentDto
{
    public long ShipmentId { get; set; }
    public int ShipmentNumber { get; set; }
    public DateOnly ShipmentDate { get; set; }
    public string? Description { get; set; }
}