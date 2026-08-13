using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Security;

namespace CheckInvoice.Application.Interfaces.Security;

public interface IMenuService
{
    Task<ResponseGetObject> GetAllMenus(PaginationQueryFilter paginationQueryFilter, MenuQueryFilter menuQueryFilter);
    Task<ResponsePost> InsertMenu(MenuDto menuDto);
    Task<ResponsePost> UpdateMenu(long id, MenuDto menuDto);
    Task<ResponsePost> DeleteMenu(long id);
    Task<ResponseGetObject> GetMyMenu();
}