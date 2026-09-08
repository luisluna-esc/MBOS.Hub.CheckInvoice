using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface ISpecialCaseService
{
    Task<ResponseGetObject> GetAllSpecialCases(PaginationQueryFilter paginationQueryFilter, SpecialCaseQueryFilter specialCaseQueryFilter);
    Task<ResponsePost> InsertSpecialCase(SpecialCaseDto specialCaseDto);
    Task<ResponsePost> UpdateSpecialCase(long id, SpecialCaseDto specialCaseDto);
    Task<ResponsePost> DeleteSpecialCase(long id);
}
