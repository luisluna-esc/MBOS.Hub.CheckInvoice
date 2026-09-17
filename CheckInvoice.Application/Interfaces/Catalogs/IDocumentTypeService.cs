using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IDocumentTypeService
{
    Task<ResponseGetObject> GetAllDocumentTypes(PaginationQueryFilter paginationQueryFilter, DocumentTypeQueryFilter documentTypeQueryFilter);
    Task<ResponsePost> InsertDocumentType(DocumentTypeDto documentTypeDto);
    Task<ResponsePost> UpdateDocumentType(long id, DocumentTypeDto documentTypeDto);
    Task<ResponsePost> DeleteDocumentType(long id);
}