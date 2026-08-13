namespace CheckInvoice.core.Entities.Organization;

public class District
{
    public long DistrictId { get; set; }
    public string? Code { get; set; }
    public string DistrictName { get; set; } = string.Empty;
    public long? MissionId { get; set; }
    public long? ProvinceId { get; set; }
    public long? AppUserId { get; set; }
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}