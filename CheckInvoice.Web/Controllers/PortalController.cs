using CheckInvoice.Application.Interfaces.Portal;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Portal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/portal")]
public class PortalController : ControllerBase
{
    private readonly IPortalService _portalService;

    public PortalController(IPortalService portalService)
    {
        _portalService = portalService;
    }

    [Authorize(Policy = "portal.account_receivable.view")]
    [HttpGet("account-receivables")]
    public async Task<IActionResult> GetMyAccountReceivables([FromQuery] PaginationQueryFilter paginationQueryFilter)
    {
        var result = await _portalService.GetMyAccountReceivables(paginationQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "portal.installment.view")]
    [HttpGet("installments")]
    public async Task<IActionResult> GetMyInstallments([FromQuery] PaginationQueryFilter paginationQueryFilter)
    {
        var result = await _portalService.GetMyInstallments(paginationQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "portal.payment.view")]
    [HttpGet("payments")]
    public async Task<IActionResult> GetMyPayments([FromQuery] PaginationQueryFilter paginationQueryFilter)
    {
        var result = await _portalService.GetMyPayments(paginationQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "portal.issue.view")]
    [HttpGet("issues")]
    public async Task<IActionResult> GetMyIssues(
        [FromQuery] PaginationQueryFilter paginationQueryFilter,
        [FromQuery] PortalDateRangeQueryFilter dateRangeQueryFilter)
    {
        var result = await _portalService.GetMyIssues(paginationQueryFilter, dateRangeQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }
}
