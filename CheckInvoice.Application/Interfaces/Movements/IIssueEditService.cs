using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;

namespace CheckInvoice.Application.Interfaces.Movements;

public interface IIssueEditService
{
    Task<ResponsePost> RequestEdit(IssueEditRequestDto request);
    Task<ResponsePost> Approve(long changeRequestId, string? notes);
    Task<ResponsePost> Reject(long changeRequestId, string? notes);
    Task<ResponseGetObject> GetEdits(string? status, long? issueId);
}
