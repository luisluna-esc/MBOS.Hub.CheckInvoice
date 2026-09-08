using CheckInvoice.Application.Dtos.Organization;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Organization;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Organization;

public interface IChurchService
{
    Task<ResponseGetObject> GetAllChurches(PaginationQueryFilter paginationQueryFilter, ChurchQueryFilter churchQueryFilter);
    Task<ResponsePost> InsertChurch(ChurchDto churchDto);
    Task<ResponsePost> UpdateChurch(long id, ChurchDto churchDto);
    Task<ResponsePost> DeleteChurch(long id);
}