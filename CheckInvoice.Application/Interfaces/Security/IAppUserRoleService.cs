using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;

namespace CheckInvoice.Application.Interfaces.Security;

public interface IAppUserRoleService
{
    Task<ResponseGetObject> GetRolesByAppUser(long appUserId);
    Task<ResponsePost> AssignRole(long appUserId, long roleId);
    Task<ResponsePost> RemoveRole(long appUserId, long roleId);
}