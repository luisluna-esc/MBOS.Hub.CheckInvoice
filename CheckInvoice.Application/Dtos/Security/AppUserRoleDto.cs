namespace CheckInvoice.Application.Dtos.Security;

public class AppUserRoleDto
{
    public long AppUserRoleId { get; set; }
    public long AppUserId { get; set; }
    public long RoleId { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedById { get; set; }
}