using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrintTypesController : ControllerBase
{
    private readonly IPrintTypeService _printTypeService;

    public PrintTypesController(IPrintTypeService printTypeService)
    {
        _printTypeService = printTypeService;
    }

    [Authorize(Policy = "print_type.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] PrintTypeQueryFilter printTypeQueryFilter)
    {
        var result = await _printTypeService.GetAllPrintTypes(paginationQueryFilter, printTypeQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "print_type.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] PrintTypeDto printTypeDto)
    {
        var result = await _printTypeService.InsertPrintType(printTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "print_type.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] PrintTypeDto printTypeDto)
    {
        var result = await _printTypeService.UpdatePrintType(id, printTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "print_type.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _printTypeService.DeletePrintType(id);
        return StatusCode((int)result.StatusCode, result);
    }
}