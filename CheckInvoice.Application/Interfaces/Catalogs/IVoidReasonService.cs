using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IVoidReasonService
{
    Task<ResponseGetObject> GetAllVoidReasons(PaginationQueryFilter paginationQueryFilter, VoidReasonQueryFilter voidReasonQueryFilter);
}
