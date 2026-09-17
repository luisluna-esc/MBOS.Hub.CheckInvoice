using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.Application.Interfaces.Finance;
using CheckInvoice.core.QueryFilters.Finance;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountReceivablesController : ControllerBase
{
    private readonly IAccountReceivableService _accountReceivableService;

    public AccountReceivablesController(IAccountReceivableService accountReceivableService)
    {
        _accountReceivableService = accountReceivableService;
    }

    [Authorize(Policy = "account_receivable.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] AccountReceivableQueryFilter accountReceivableQueryFilter)
    {
        var result = await _accountReceivableService.GetAllAccountReceivables(paginationQueryFilter, accountReceivableQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "account_receivable.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] AccountReceivableDto accountReceivableDto)
    {
        var result = await _accountReceivableService.InsertAccountReceivable(accountReceivableDto);
        return StatusCode((int)result.StatusCode, result);
    }
}