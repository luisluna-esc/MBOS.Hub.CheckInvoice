using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehousesController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehousesController(IWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    [Authorize(Policy = "warehouse.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] WarehouseQueryFilter warehouseQueryFilter)
    {
        var result = await _warehouseService.GetAllWarehouses(paginationQueryFilter, warehouseQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "warehouse.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] WarehouseDto warehouseDto)
    {
        var result = await _warehouseService.InsertWarehouse(warehouseDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "warehouse.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] WarehouseDto warehouseDto)
    {
        var result = await _warehouseService.UpdateWarehouse(id, warehouseDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "warehouse.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _warehouseService.DeleteWarehouse(id);
        return StatusCode((int)result.StatusCode, result);
    }
}