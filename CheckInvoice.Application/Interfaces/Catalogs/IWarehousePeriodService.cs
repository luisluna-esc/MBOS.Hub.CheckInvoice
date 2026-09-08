using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IWarehousePeriodService
{
    Task<ResponseGetObject> GetAllWarehousePeriods(PaginationQueryFilter paginationQueryFilter, WarehousePeriodQueryFilter warehousePeriodQueryFilter);
    Task<ResponsePost> InsertWarehousePeriod(WarehousePeriodDto warehousePeriodDto);
    Task<ResponsePost> UpdateWarehousePeriod(long id, WarehousePeriodDto warehousePeriodDto);
    Task<ResponsePost> DeleteWarehousePeriod(long id);
    Task<ResponsePost> CloseWarehousePeriod(long id);
}