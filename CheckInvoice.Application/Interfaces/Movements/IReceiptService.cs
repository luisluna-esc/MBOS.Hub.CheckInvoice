using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Movements;

public interface IReceiptService
{
    Task<ResponseGetObject> GetAllReceipts(PaginationQueryFilter paginationQueryFilter, ReceiptQueryFilter receiptQueryFilter);
    Task<ResponseGetObject> GetReceiptDetails(long receiptId);
    Task<ResponsePost> InsertReceipt(ReceiptRequestDto receiptRequestDto);
    Task<ResponseGetObject> GetReturnableIssueLines(long issueId);
    Task<ResponsePost> InsertReturn(ReceiptReturnRequestDto receiptReturnRequestDto);
}