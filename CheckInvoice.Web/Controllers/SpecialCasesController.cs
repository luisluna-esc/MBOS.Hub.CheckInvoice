using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpecialCasesController : ControllerBase
{
    private readonly ISpecialCaseService _specialCaseService;

    public SpecialCasesController(ISpecialCaseService specialCaseService)
    {
        _specialCaseService = specialCaseService;
    }

    [Authorize(Policy = "special_case.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] SpecialCaseQueryFilter specialCaseQueryFilter)
    {
        var result = await _specialCaseService.GetAllSpecialCases(paginationQueryFilter, specialCaseQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "special_case.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] SpecialCaseDto specialCaseDto)
    {
        var result = await _specialCaseService.InsertSpecialCase(specialCaseDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "special_case.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] SpecialCaseDto specialCaseDto)
    {
        var result = await _specialCaseService.UpdateSpecialCase(id, specialCaseDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "special_case.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _specialCaseService.DeleteSpecialCase(id);
        return StatusCode((int)result.StatusCode, result);
    }
}
