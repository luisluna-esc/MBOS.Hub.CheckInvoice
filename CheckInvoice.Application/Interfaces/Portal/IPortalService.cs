using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Portal;

namespace CheckInvoice.Application.Interfaces.Portal;

public interface IPortalService
{
    Task<ResponseGetObject> GetMyAccountReceivables(PaginationQueryFilter paginationQueryFilter);
    Task<ResponseGetObject> GetMyInstallments(PaginationQueryFilter paginationQueryFilter);
    Task<ResponseGetObject> GetMyPayments(PaginationQueryFilter paginationQueryFilter);
    Task<ResponseGetObject> GetMyIssues(PaginationQueryFilter paginationQueryFilter, PortalDateRangeQueryFilter dateRangeQueryFilter);
}
