using CheckInvoice.Application.Dtos.Organization;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Organization;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Organization;

public interface IDistrictService
{
    Task<ResponseGetObject> GetAllDistricts(PaginationQueryFilter paginationQueryFilter, DistrictQueryFilter districtQueryFilter);
    Task<ResponsePost> InsertDistrict(DistrictDto districtDto);
    Task<ResponsePost> UpdateDistrict(long id, DistrictDto districtDto);
    Task<ResponsePost> DeleteDistrict(long id);
}