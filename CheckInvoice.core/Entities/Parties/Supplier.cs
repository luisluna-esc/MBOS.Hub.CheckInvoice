namespace CheckInvoice.core.Entities.Parties;

public class Supplier
{
    public long SupplierId { get; set; }
    public long PartyId { get; set; }
    public string? Code { get; set; }
    public string? LegalName { get; set; }
    public long? CountryId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    // Proveedor adventista: su distrito e iglesia (sin esto es null). Es dato del proveedor,
    // aunque no esté registrado también como cliente.
    public long? DistrictId { get; set; }
    public long? ChurchId { get; set; }
}
