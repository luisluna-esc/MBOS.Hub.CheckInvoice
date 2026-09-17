using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Warehouses;

namespace CheckInvoice.Application.Interfaces.Warehouses;

public interface IStockService
{
    Task<ResponseGetObject> GetAllStocks(PaginationQueryFilter paginationQueryFilter, StockQueryFilter stockQueryFilter);
}