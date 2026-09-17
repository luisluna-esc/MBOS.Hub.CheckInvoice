using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChurchTypesController : ControllerBase
{
    private readonly IChurchTypeService _churchTypeService;

    public ChurchTypesController(IChurchTypeService churchTypeService)
    {
        _churchTypeService = churchTypeService;
    }

    [Authorize(Policy = "church_type.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ChurchTypeQueryFilter churchTypeQueryFilter)
    {
        var result = await _churchTypeService.GetAllChurchTypes(paginationQueryFilter, churchTypeQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "church_type.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ChurchTypeDto churchTypeDto)
    {
        var result = await _churchTypeService.InsertChurchType(churchTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "church_type.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] ChurchTypeDto churchTypeDto)
    {
        var result = await _churchTypeService.UpdateChurchType(id, churchTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "church_type.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _churchTypeService.DeleteChurchType(id);
        return StatusCode((int)result.StatusCode, result);
    }
}