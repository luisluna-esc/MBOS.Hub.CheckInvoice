namespace CheckInvoice.Application.Dtos.Parties;

public class SupplierDto
{
    public long SupplierId { get; set; }
    public string? Code { get; set; }
    public string? LegalName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public long? CountryId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? MobilePhone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}