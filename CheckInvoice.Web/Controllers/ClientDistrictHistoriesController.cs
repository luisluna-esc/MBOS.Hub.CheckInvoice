using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.Application.Interfaces.Parties;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Parties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientDistrictHistoriesController : ControllerBase
{
    private readonly IClientDistrictHistoryService _clientDistrictHistoryService;

    public ClientDistrictHistoriesController(IClientDistrictHistoryService clientDistrictHistoryService)
    {
        _clientDistrictHistoryService = clientDistrictHistoryService;
    }

    [Authorize(Policy = "client_district_history.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ClientDistrictHistoryQueryFilter clientDistrictHistoryQueryFilter)
    {
        var result = await _clientDistrictHistoryService.GetAllClientDistrictHistories(paginationQueryFilter, clientDistrictHistoryQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "client_district_history.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ClientDistrictHistoryDto clientDistrictHistoryDto)
    {
        var result = await _clientDistrictHistoryService.InsertClientDistrictHistory(clientDistrictHistoryDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "client_district_history.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] ClientDistrictHistoryDto clientDistrictHistoryDto)
    {
        var result = await _clientDistrictHistoryService.UpdateClientDistrictHistory(id, clientDistrictHistoryDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "client_district_history.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _clientDistrictHistoryService.DeleteClientDistrictHistory(id);
        return StatusCode((int)result.StatusCode, result);
    }
}