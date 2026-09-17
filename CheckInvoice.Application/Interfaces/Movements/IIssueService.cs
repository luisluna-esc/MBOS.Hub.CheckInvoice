using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Movements;

public interface IIssueService
{
    Task<ResponseGetObject> GetAllIssues(PaginationQueryFilter paginationQueryFilter, IssueQueryFilter issueQueryFilter);
    Task<ResponseGetObject> GetIssueDetails(long issueId);
    Task<ResponsePost> InsertIssue(IssueRequestDto issueRequestDto);
}