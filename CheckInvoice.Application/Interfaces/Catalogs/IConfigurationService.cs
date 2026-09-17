using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IConfigurationService
{
    Task<ResponseGetObject> GetAllConfigurations(PaginationQueryFilter paginationQueryFilter, ConfigurationQueryFilter configurationQueryFilter);
    Task<ResponsePost> InsertConfiguration(ConfigurationDto configurationDto);
    Task<ResponsePost> UpdateConfiguration(long id, ConfigurationDto configurationDto);
    Task<ResponsePost> DeleteConfiguration(long id);
}