using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Movements;

public interface IIssueVoidRequestService
{
    Task<ResponseGetObject> GetAllVoidRequests(PaginationQueryFilter paginationQueryFilter, IssueVoidRequestQueryFilter issueVoidRequestQueryFilter);
    Task<ResponsePost> RequestVoid(IssueVoidRequestCreateDto issueVoidRequestCreateDto);
    Task<ResponsePost> ApproveVoidRequest(long id, IssueVoidRequestReviewDto issueVoidRequestReviewDto);
    Task<ResponsePost> RejectVoidRequest(long id, IssueVoidRequestReviewDto issueVoidRequestReviewDto);
}
