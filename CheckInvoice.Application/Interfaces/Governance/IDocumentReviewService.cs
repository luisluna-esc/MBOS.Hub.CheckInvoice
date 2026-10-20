using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;

namespace CheckInvoice.Application.Interfaces.Governance;

public interface IDocumentReviewService
{
    Task<ResponseGetObject> GetReview(string documentType, long documentId);
    Task<ResponsePost> Verify(string documentType, long documentId, string password);
    Task<ResponsePost> Approve(string documentType, long documentId, string password, string? notes);
    Task<ResponsePost> Reject(string documentType, long documentId, string password, string reason);
    Task<ResponsePost> Reopen(string documentType, long documentId, string password, string reason);
}
