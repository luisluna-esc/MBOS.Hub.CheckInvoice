using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Security;

namespace CheckInvoice.Application.Interfaces.Security;

public interface IAppUserService
{
    Task<ResponseGetObject> GetAllAppUsers(PaginationQueryFilter paginationQueryFilter, AppUserQueryFilter appUserQueryFilter);
    Task<ResponsePost> InsertAppUser(AppUserDto appUserDto);
    Task<ResponsePost> UpdateAppUser(long id, AppUserDto appUserDto);
    Task<ResponsePost> DeleteAppUser(long id);
}