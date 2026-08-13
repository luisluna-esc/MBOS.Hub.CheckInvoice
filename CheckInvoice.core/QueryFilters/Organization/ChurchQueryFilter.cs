namespace CheckInvoice.core.QueryFilters.Organization;

public class ChurchQueryFilter
{
    public long? ChurchId { get; set; }
    public string? Code { get; set; }
    public string? ChurchName { get; set; }
    public long? DistrictId { get; set; }
    public long? ChurchTypeId { get; set; }
    public bool? IsActive { get; set; }
}