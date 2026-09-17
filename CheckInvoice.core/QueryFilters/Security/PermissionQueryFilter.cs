namespace CheckInvoice.core.QueryFilters.Security;

public class PermissionQueryFilter
{
    public long? PermissionId { get; set; }
    public string? Code { get; set; }
    public string? Module { get; set; }
    public bool? IsActive { get; set; }
}