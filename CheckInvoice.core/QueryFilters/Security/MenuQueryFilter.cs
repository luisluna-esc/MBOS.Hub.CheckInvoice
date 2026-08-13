namespace CheckInvoice.core.QueryFilters.Security;

public class MenuQueryFilter
{
    public long? MenuId { get; set; }
    public long? ParentMenuId { get; set; }
    public long? PermissionId { get; set; }
    public bool? IsActive { get; set; }
}