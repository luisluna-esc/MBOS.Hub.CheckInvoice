using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Movements;

public interface IDiscountService
{
    Task<ResponseGetObject> GetAllDiscounts(PaginationQueryFilter paginationQueryFilter, DiscountQueryFilter discountQueryFilter);
    Task<ResponsePost> InsertDiscount(DiscountDto discountDto);
    Task<ResponsePost> UpdateDiscountStatus(long id, DiscountStatusUpdateDto discountStatusUpdateDto);
}