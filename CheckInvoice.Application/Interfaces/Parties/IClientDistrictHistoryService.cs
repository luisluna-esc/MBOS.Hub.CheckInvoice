using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Parties;

namespace CheckInvoice.Application.Interfaces.Parties;

public interface IClientDistrictHistoryService
{
    Task<ResponseGetObject> GetAllClientDistrictHistories(PaginationQueryFilter paginationQueryFilter, ClientDistrictHistoryQueryFilter clientDistrictHistoryQueryFilter);
    Task<ResponsePost> InsertClientDistrictHistory(ClientDistrictHistoryDto clientDistrictHistoryDto);
    Task<ResponsePost> UpdateClientDistrictHistory(long id, ClientDistrictHistoryDto clientDistrictHistoryDto);
    Task<ResponsePost> DeleteClientDistrictHistory(long id);
}