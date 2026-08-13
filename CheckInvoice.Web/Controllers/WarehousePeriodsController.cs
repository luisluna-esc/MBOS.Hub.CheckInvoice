using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehousePeriodsController : ControllerBase
{
    private readonly IWarehousePeriodService _warehousePeriodService;

    public WarehousePeriodsController(IWarehousePeriodService warehousePeriodService)
    {
        _warehousePeriodService = warehousePeriodService;
    }

    [Authorize(Policy = "warehouse_period.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] WarehousePeriodQueryFilter warehousePeriodQueryFilter)
    {
        var result = await _warehousePeriodService.GetAllWarehousePeriods(paginationQueryFilter, warehousePeriodQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "warehouse_period.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] WarehousePeriodDto warehousePeriodDto)
    {
        var result = await _warehousePeriodService.InsertWarehousePeriod(warehousePeriodDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "warehouse_period.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] WarehousePeriodDto warehousePeriodDto)
    {
        var result = await _warehousePeriodService.UpdateWarehousePeriod(id, warehousePeriodDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "warehouse_period.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _warehousePeriodService.DeleteWarehousePeriod(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "warehouse_period.manage")]
    [HttpPut("{id}/close")]
    public async Task<IActionResult> Close(long id)
    {
        var result = await _warehousePeriodService.CloseWarehousePeriod(id);
        return StatusCode((int)result.StatusCode, result);
    }
}