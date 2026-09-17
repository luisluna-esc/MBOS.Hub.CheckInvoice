namespace CheckInvoice.core.Entities.Security;

public class MenuRole
{
    public long MenuRoleId { get; set; }
    public long MenuId { get; set; }
    public long RoleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
