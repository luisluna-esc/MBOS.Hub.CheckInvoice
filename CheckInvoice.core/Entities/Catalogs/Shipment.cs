namespace CheckInvoice.core.Entities.Catalogs;

public class Shipment
{
    public long ShipmentId { get; set; }
    public int ShipmentNumber { get; set; }
    public DateOnly ShipmentDate { get; set; }
    public string? Description { get; set; }
}