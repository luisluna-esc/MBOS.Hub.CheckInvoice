using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Parties;

namespace CheckInvoice.Application.Interfaces.Parties;

public interface IClientService
{
    Task<ResponseGetObject> GetAllClients(PaginationQueryFilter paginationQueryFilter, ClientQueryFilter clientQueryFilter);
    Task<ResponsePost> InsertClient(ClientDto clientDto);
    Task<ResponsePost> UpdateClient(long id, ClientDto clientDto);
    Task<ResponsePost> DeleteClient(long id);
    Task<ResponseGetObject> GrantPortalAccess(long id);
}