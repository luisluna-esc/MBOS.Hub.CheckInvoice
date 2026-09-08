using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;

namespace CheckInvoice.Application.Interfaces.Security;

public interface IRoleMenuService
{
    Task<ResponseGetObject> GetMenusByRole(long roleId);
    Task<ResponsePost> AssignMenu(long roleId, long menuId);
    Task<ResponsePost> RemoveMenu(long roleId, long menuId);
}
