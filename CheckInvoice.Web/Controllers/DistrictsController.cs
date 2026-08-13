using CheckInvoice.Application.Dtos.Organization;
using CheckInvoice.Application.Interfaces.Organization;
using CheckInvoice.core.QueryFilters.Organization;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DistrictsController : ControllerBase
{
    private readonly IDistrictService _districtService;

    public DistrictsController(IDistrictService districtService)
    {
        _districtService = districtService;
    }

    [Authorize(Policy = "district.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] DistrictQueryFilter districtQueryFilter)
    {
        var result = await _districtService.GetAllDistricts(paginationQueryFilter, districtQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "district.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] DistrictDto districtDto)
    {
        var result = await _districtService.InsertDistrict(districtDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "district.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] DistrictDto districtDto)
    {
        var result = await _districtService.UpdateDistrict(id, districtDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "district.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _districtService.DeleteDistrict(id);
        return StatusCode((int)result.StatusCode, result);
    }
}