using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface ICountryService
{
    Task<ResponseGetObject> GetAllCountries(PaginationQueryFilter paginationQueryFilter, CountryQueryFilter countryQueryFilter);
    Task<ResponsePost> InsertCountry(CountryDto countryDto);
    Task<ResponsePost> UpdateCountry(long id, CountryDto countryDto);
    Task<ResponsePost> DeleteCountry(long id);
}