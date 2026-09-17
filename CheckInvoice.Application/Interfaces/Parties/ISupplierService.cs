using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Parties;

namespace CheckInvoice.Application.Interfaces.Parties;

public interface ISupplierService
{
    Task<ResponseGetObject> GetAllSuppliers(PaginationQueryFilter paginationQueryFilter, SupplierQueryFilter supplierQueryFilter);
    Task<ResponsePost> InsertSupplier(SupplierDto supplierDto);
    Task<ResponsePost> UpdateSupplier(long id, SupplierDto supplierDto);
    Task<ResponsePost> DeleteSupplier(long id);
}