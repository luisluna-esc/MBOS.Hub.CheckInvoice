namespace CheckInvoice.core.Entities.Organization;

public class Church
{
    public long ChurchId { get; set; }
    public string? Code { get; set; }
    public string ChurchName { get; set; } = string.Empty;
    public long? DistrictId { get; set; }
    public long? ChurchTypeId { get; set; }
    public bool IsActive { get; set; } = true;
}