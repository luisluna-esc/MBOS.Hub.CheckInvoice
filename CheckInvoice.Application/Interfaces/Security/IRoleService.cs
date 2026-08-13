using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Security;

namespace CheckInvoice.Application.Interfaces.Security;

public interface IRoleService
{
    Task<ResponseGetObject> GetAllRoles(PaginationQueryFilter paginationQueryFilter, RoleQueryFilter roleQueryFilter);
    Task<ResponsePost> InsertRole(RoleDto roleDto);
    Task<ResponsePost> UpdateRole(long id, RoleDto roleDto);
    Task<ResponsePost> DeleteRole(long id);
}