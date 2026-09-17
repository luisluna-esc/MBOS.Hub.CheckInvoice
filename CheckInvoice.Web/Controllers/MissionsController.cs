using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MissionsController : ControllerBase
{
    private readonly IMissionService _missionService;

    public MissionsController(IMissionService missionService)
    {
        _missionService = missionService;
    }

    [Authorize(Policy = "mission.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] MissionQueryFilter missionQueryFilter)
    {
        var result = await _missionService.GetAllMissions(paginationQueryFilter, missionQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "mission.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] MissionDto missionDto)
    {
        var result = await _missionService.InsertMission(missionDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "mission.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] MissionDto missionDto)
    {
        var result = await _missionService.UpdateMission(id, missionDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "mission.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _missionService.DeleteMission(id);
        return StatusCode((int)result.StatusCode, result);
    }
}