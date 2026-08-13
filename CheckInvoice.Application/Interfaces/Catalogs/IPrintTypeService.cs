using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IPrintTypeService
{
    Task<ResponseGetObject> GetAllPrintTypes(PaginationQueryFilter paginationQueryFilter, PrintTypeQueryFilter printTypeQueryFilter);
    Task<ResponsePost> InsertPrintType(PrintTypeDto printTypeDto);
    Task<ResponsePost> UpdatePrintType(long id, PrintTypeDto printTypeDto);
    Task<ResponsePost> DeletePrintType(long id);
}