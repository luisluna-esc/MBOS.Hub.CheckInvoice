using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IProvinceService
{
    Task<ResponseGetObject> GetAllProvinces(PaginationQueryFilter paginationQueryFilter, ProvinceQueryFilter provinceQueryFilter);
    Task<ResponsePost> InsertProvince(ProvinceDto provinceDto);
    Task<ResponsePost> UpdateProvince(long id, ProvinceDto provinceDto);
    Task<ResponsePost> DeleteProvince(long id);
}