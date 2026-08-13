using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IReceiptTypeService
{
    Task<ResponseGetObject> GetAllReceiptTypes(PaginationQueryFilter paginationQueryFilter, ReceiptTypeQueryFilter receiptTypeQueryFilter);
    Task<ResponsePost> InsertReceiptType(ReceiptTypeDto receiptTypeDto);
    Task<ResponsePost> UpdateReceiptType(long id, ReceiptTypeDto receiptTypeDto);
    Task<ResponsePost> DeleteReceiptType(long id);
}