using CheckInvoice.Application.Dtos.Organization;
using CheckInvoice.Application.Interfaces.Organization;
using CheckInvoice.core.QueryFilters.Organization;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChurchesController : ControllerBase
{
    private readonly IChurchService _churchService;

    public ChurchesController(IChurchService churchService)
    {
        _churchService = churchService;
    }

    [Authorize(Policy = "church.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ChurchQueryFilter churchQueryFilter)
    {
        var result = await _churchService.GetAllChurches(paginationQueryFilter, churchQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "church.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ChurchDto churchDto)
    {
        var result = await _churchService.InsertChurch(churchDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "church.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] ChurchDto churchDto)
    {
        var result = await _churchService.UpdateChurch(id, churchDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "church.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _churchService.DeleteChurch(id);
        return StatusCode((int)result.StatusCode, result);
    }
}