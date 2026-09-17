using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IWarehouseService
{
    Task<ResponseGetObject> GetAllWarehouses(PaginationQueryFilter paginationQueryFilter, WarehouseQueryFilter warehouseQueryFilter);
    Task<ResponsePost> InsertWarehouse(WarehouseDto warehouseDto);
    Task<ResponsePost> UpdateWarehouse(long id, WarehouseDto warehouseDto);
    Task<ResponsePost> DeleteWarehouse(long id);
}