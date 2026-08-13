namespace CheckInvoice.Application.Dtos.Parties;

public class ClientDto
{
    public long ClientId { get; set; }
    public long? AppUserId { get; set; }
    public long? DocumentTypeId { get; set; }
    public string? TaxId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? MobilePhone { get; set; }
    public long? DistrictId { get; set; }
    public long? ChurchId { get; set; }
    public bool IsActive { get; set; }
    public string? Complement { get; set; }
}