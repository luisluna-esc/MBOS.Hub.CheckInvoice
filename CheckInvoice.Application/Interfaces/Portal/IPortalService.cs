using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Portal;

public interface IPortalService
{
    Task<ResponseGetObject> GetMyAccountReceivables(PaginationQueryFilter paginationQueryFilter);
    Task<ResponseGetObject> GetMyInstallments(PaginationQueryFilter paginationQueryFilter);
    Task<ResponseGetObject> GetMyPayments(PaginationQueryFilter paginationQueryFilter);
    Task<ResponseGetObject> GetMyDeposits(PaginationQueryFilter paginationQueryFilter);
}