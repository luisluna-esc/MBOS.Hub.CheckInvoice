using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Movements;

public interface IInventoryCountService
{
    Task<ResponseGetObject> GetAllInventoryCounts(PaginationQueryFilter paginationQueryFilter, InventoryCountQueryFilter inventoryCountQueryFilter);
    Task<ResponseGetObject> GetInventoryCountDetails(long inventoryCountId);
    Task<ResponsePost> InsertInventoryCount(InventoryCountRequestDto inventoryCountRequestDto);
}
