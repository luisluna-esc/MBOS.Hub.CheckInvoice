using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Finance;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Finance;

public interface IAccountReceivableService
{
    Task<ResponseGetObject> GetAllAccountReceivables(PaginationQueryFilter paginationQueryFilter, AccountReceivableQueryFilter accountReceivableQueryFilter);
    Task<ResponsePost> InsertAccountReceivable(AccountReceivableDto accountReceivableDto);
}