using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IChurchTypeService
{
    Task<ResponseGetObject> GetAllChurchTypes(PaginationQueryFilter paginationQueryFilter, ChurchTypeQueryFilter churchTypeQueryFilter);
    Task<ResponsePost> InsertChurchType(ChurchTypeDto churchTypeDto);
    Task<ResponsePost> UpdateChurchType(long id, ChurchTypeDto churchTypeDto);
    Task<ResponsePost> DeleteChurchType(long id);
}