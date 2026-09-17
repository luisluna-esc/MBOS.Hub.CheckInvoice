using CheckInvoice.Application.Dtos.Governance;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Governance;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Governance;

public interface IChangeRequestService
{
    Task<ResponseGetObject> GetAllChangeRequests(PaginationQueryFilter paginationQueryFilter, ChangeRequestQueryFilter changeRequestQueryFilter);
    Task<ResponseGetObject> GetChangeRequestById(long id);
    Task<ResponsePost> RequestChange(ChangeRequestCreateDto changeRequestCreateDto);
    Task<ResponsePost> ApproveChangeRequest(long id, ChangeRequestReviewDto changeRequestReviewDto);
    Task<ResponsePost> RejectChangeRequest(long id, ChangeRequestReviewDto changeRequestReviewDto);
}
