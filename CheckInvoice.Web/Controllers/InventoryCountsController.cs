using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryCountsController : ControllerBase
{
    private readonly IInventoryCountService _inventoryCountService;

    public InventoryCountsController(IInventoryCountService inventoryCountService)
    {
        _inventoryCountService = inventoryCountService;
    }

    [Authorize(Policy = "inventory_count.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] InventoryCountQueryFilter inventoryCountQueryFilter)
    {
        var result = await _inventoryCountService.GetAllInventoryCounts(paginationQueryFilter, inventoryCountQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "inventory_count.view")]
    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetDetails(long id)
    {
        var result = await _inventoryCountService.GetInventoryCountDetails(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "inventory_count.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] InventoryCountRequestDto inventoryCountRequestDto)
    {
        var result = await _inventoryCountService.InsertInventoryCount(inventoryCountRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }
}