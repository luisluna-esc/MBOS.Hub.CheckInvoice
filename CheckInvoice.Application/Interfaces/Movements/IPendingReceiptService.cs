using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Movements;

public interface IPendingReceiptService
{
    Task<ResponseGetObject> GetAll(PaginationQueryFilter paginationQueryFilter, PendingReceiptQueryFilter filter);
    Task<ResponseGetObject> GetById(long pendingReceiptId);
    Task<ResponsePost> Insert(PendingReceiptRequestDto request);
    Task<ResponsePost> Void(long pendingReceiptId);
    Task<ResponsePost> InsertDelivery(long pendingReceiptId, PendingDeliveryRequestDto request);
    Task<ResponsePost> VoidDelivery(long pendingDeliveryId);
    Task<ResponsePost> Validate(PendingReceiptValidateRequestDto request);
}
