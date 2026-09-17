using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Movements;

public interface IReceiptVoidRequestService
{
    Task<ResponseGetObject> GetAllVoidRequests(PaginationQueryFilter paginationQueryFilter, ReceiptVoidRequestQueryFilter receiptVoidRequestQueryFilter);
    Task<ResponsePost> RequestVoid(ReceiptVoidRequestCreateDto receiptVoidRequestCreateDto);
    Task<ResponsePost> ApproveVoidRequest(long id, ReceiptVoidRequestReviewDto receiptVoidRequestReviewDto);
    Task<ResponsePost> RejectVoidRequest(long id, ReceiptVoidRequestReviewDto receiptVoidRequestReviewDto);
}
