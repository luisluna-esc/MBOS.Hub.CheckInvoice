namespace CheckInvoice.core.QueryFilters.Parties;

public class ClientQueryFilter
{
    public long? ClientId { get; set; }
    public long? AppUserId { get; set; }
    public string? TaxId { get; set; }
    public long? DistrictId { get; set; }
    public long? ChurchId { get; set; }
    public bool? IsActive { get; set; }
}