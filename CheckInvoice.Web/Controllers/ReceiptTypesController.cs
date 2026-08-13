using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReceiptTypesController : ControllerBase
{
    private readonly IReceiptTypeService _receiptTypeService;

    public ReceiptTypesController(IReceiptTypeService receiptTypeService)
    {
        _receiptTypeService = receiptTypeService;
    }

    [Authorize(Policy = "receipt_type.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ReceiptTypeQueryFilter receiptTypeQueryFilter)
    {
        var result = await _receiptTypeService.GetAllReceiptTypes(paginationQueryFilter, receiptTypeQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt_type.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ReceiptTypeDto receiptTypeDto)
    {
        var result = await _receiptTypeService.InsertReceiptType(receiptTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt_type.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] ReceiptTypeDto receiptTypeDto)
    {
        var result = await _receiptTypeService.UpdateReceiptType(id, receiptTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt_type.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _receiptTypeService.DeleteReceiptType(id);
        return StatusCode((int)result.StatusCode, result);
    }
}