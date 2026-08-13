using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;

namespace CheckInvoice.Application.Interfaces.Security;

public interface IRolePermissionService
{
    Task<ResponseGetObject> GetPermissionsByRole(long roleId);
    Task<ResponsePost> AssignPermission(long roleId, long permissionId);
    Task<ResponsePost> RemovePermission(long roleId, long permissionId);
}