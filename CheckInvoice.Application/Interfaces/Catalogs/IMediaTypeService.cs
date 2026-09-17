using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IMediaTypeService
{
    Task<ResponseGetObject> GetAllMediaTypes(PaginationQueryFilter paginationQueryFilter, MediaTypeQueryFilter mediaTypeQueryFilter);
    Task<ResponsePost> InsertMediaType(MediaTypeDto mediaTypeDto);
    Task<ResponsePost> UpdateMediaType(long id, MediaTypeDto mediaTypeDto);
    Task<ResponsePost> DeleteMediaType(long id);
}