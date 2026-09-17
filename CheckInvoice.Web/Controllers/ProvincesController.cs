using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProvincesController : ControllerBase
{
    private readonly IProvinceService _provinceService;

    public ProvincesController(IProvinceService provinceService)
    {
        _provinceService = provinceService;
    }

    [Authorize(Policy = "province.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ProvinceQueryFilter provinceQueryFilter)
    {
        var result = await _provinceService.GetAllProvinces(paginationQueryFilter, provinceQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "province.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ProvinceDto provinceDto)
    {
        var result = await _provinceService.InsertProvince(provinceDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "province.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] ProvinceDto provinceDto)
    {
        var result = await _provinceService.UpdateProvince(id, provinceDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "province.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _provinceService.DeleteProvince(id);
        return StatusCode((int)result.StatusCode, result);
    }
}