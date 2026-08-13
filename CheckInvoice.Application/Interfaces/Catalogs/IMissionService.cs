using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IMissionService
{
    Task<ResponseGetObject> GetAllMissions(PaginationQueryFilter paginationQueryFilter, MissionQueryFilter missionQueryFilter);
    Task<ResponsePost> InsertMission(MissionDto missionDto);
    Task<ResponsePost> UpdateMission(long id, MissionDto missionDto);
    Task<ResponsePost> DeleteMission(long id);
}