namespace CheckInvoice.core.QueryFilters.Organization;

public class DistrictQueryFilter
{
    public long? DistrictId { get; set; }
    public string? Code { get; set; }
    public string? DistrictName { get; set; }
    public long? MissionId { get; set; }
    public long? ProvinceId { get; set; }
    public bool? IsActive { get; set; }
}