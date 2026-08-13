namespace CheckInvoice.Application.Dtos.Security;

public class RolePermissionDto
{
    public long RolePermissionId { get; set; }
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedById { get; set; }
}