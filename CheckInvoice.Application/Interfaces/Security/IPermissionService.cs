using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Security;

namespace CheckInvoice.Application.Interfaces.Security;

public interface IPermissionService
{
    Task<ResponseGetObject> GetAllPermissions(PaginationQueryFilter paginationQueryFilter, PermissionQueryFilter permissionQueryFilter);
    Task<ResponsePost> InsertPermission(PermissionDto permissionDto);
    Task<ResponsePost> UpdatePermission(long id, PermissionDto permissionDto);
    Task<ResponsePost> DeletePermission(long id);
}