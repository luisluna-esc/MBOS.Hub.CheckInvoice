using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShipmentsController : ControllerBase
{
    private readonly IShipmentService _shipmentService;

    public ShipmentsController(IShipmentService shipmentService)
    {
        _shipmentService = shipmentService;
    }

    [Authorize(Policy = "shipment.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ShipmentQueryFilter shipmentQueryFilter)
    {
        var result = await _shipmentService.GetAllShipments(paginationQueryFilter, shipmentQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "shipment.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ShipmentDto shipmentDto)
    {
        var result = await _shipmentService.InsertShipment(shipmentDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "shipment.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] ShipmentDto shipmentDto)
    {
        var result = await _shipmentService.UpdateShipment(id, shipmentDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "shipment.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _shipmentService.DeleteShipment(id);
        return StatusCode((int)result.StatusCode, result);
    }
}