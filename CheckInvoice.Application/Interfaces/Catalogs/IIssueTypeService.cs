using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IIssueTypeService
{
    Task<ResponseGetObject> GetAllIssueTypes(PaginationQueryFilter paginationQueryFilter, IssueTypeQueryFilter issueTypeQueryFilter);
    Task<ResponsePost> InsertIssueType(IssueTypeDto issueTypeDto);
    Task<ResponsePost> UpdateIssueType(long id, IssueTypeDto issueTypeDto);
    Task<ResponsePost> DeleteIssueType(long id);
}