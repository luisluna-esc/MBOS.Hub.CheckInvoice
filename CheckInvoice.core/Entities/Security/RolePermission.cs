namespace CheckInvoice.core.Entities.Security;

public class RolePermission
{
    public long RolePermissionId { get; set; }
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedById { get; set; }
}