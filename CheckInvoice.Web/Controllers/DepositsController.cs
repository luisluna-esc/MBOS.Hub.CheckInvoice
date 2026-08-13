using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepositsController : ControllerBase
{
    private readonly IDepositService _depositService;

    public DepositsController(IDepositService depositService)
    {
        _depositService = depositService;
    }

    [Authorize(Policy = "deposit.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] DepositQueryFilter depositQueryFilter)
    {
        var result = await _depositService.GetAllDeposits(paginationQueryFilter, depositQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "deposit.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] DepositDto depositDto)
    {
        var result = await _depositService.InsertDeposit(depositDto);
        return StatusCode((int)result.StatusCode, result);
    }
}