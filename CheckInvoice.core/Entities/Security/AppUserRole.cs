namespace CheckInvoice.core.Entities.Security;

public class AppUserRole
{
    public long AppUserRoleId { get; set; }
    public long AppUserId { get; set; }
    public long RoleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedById { get; set; }
}