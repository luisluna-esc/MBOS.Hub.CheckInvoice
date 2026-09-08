namespace CheckInvoice.core.Entities.Parties;

public class Client
{
    public long ClientId { get; set; }
    public long PartyId { get; set; }
    public long? AppUserId { get; set; }
    public long? DocumentTypeId { get; set; }
    public long? DistrictId { get; set; }
    public long? ChurchId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Complement { get; set; }
    public long? SpecialCaseId { get; set; }
    public bool IsPastor { get; set; }
}
