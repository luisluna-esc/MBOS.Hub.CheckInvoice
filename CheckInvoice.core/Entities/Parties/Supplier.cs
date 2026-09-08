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
}
