using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;

namespace CheckInvoice.Application.Interfaces.Movements;

public interface ITransferVoidRequestService
{
    Task<ResponseGetObject> GetAll(string? status, long? transferId);
    Task<ResponsePost> RequestVoid(TransferVoidRequestCreateDto dto);
    Task<ResponsePost> Approve(long id, string? notes);
    Task<ResponsePost> Reject(long id, string? notes);
}
