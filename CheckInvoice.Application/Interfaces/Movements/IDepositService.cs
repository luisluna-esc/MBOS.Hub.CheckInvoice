using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Movements;

public interface IDepositService
{
    Task<ResponseGetObject> GetAllDeposits(PaginationQueryFilter paginationQueryFilter, DepositQueryFilter depositQueryFilter);
    Task<ResponsePost> InsertDeposit(DepositDto depositDto);
}