using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.Application.Interfaces.Parties;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Parties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [Authorize(Policy = "supplier.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] SupplierQueryFilter supplierQueryFilter)
    {
        var result = await _supplierService.GetAllSuppliers(paginationQueryFilter, supplierQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "supplier.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] SupplierDto supplierDto)
    {
        var result = await _supplierService.InsertSupplier(supplierDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "supplier.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] SupplierDto supplierDto)
    {
        var result = await _supplierService.UpdateSupplier(id, supplierDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "supplier.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _supplierService.DeleteSupplier(id);
        return StatusCode((int)result.StatusCode, result);
    }
}