using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VoidReasonsController : ControllerBase
{
    private readonly IVoidReasonService _voidReasonService;

    public VoidReasonsController(IVoidReasonService voidReasonService)
    {
        _voidReasonService = voidReasonService;
    }

    [Authorize(Policy = "issue.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] VoidReasonQueryFilter voidReasonQueryFilter)
    {
        var result = await _voidReasonService.GetAllVoidReasons(paginationQueryFilter, voidReasonQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }
}
