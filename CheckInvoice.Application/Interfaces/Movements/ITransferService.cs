using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Movements;

public interface ITransferService
{
    Task<ResponseGetObject> GetAllTransfers(PaginationQueryFilter paginationQueryFilter, TransferQueryFilter transferQueryFilter);
    Task<ResponseGetObject> GetTransferDetails(long transferId);
    Task<ResponsePost> InsertTransfer(TransferRequestDto transferRequestDto);
}