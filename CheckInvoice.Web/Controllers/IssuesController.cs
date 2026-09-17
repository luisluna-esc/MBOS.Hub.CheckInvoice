using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IssuesController : ControllerBase
{
    private readonly IIssueService _issueService;

    public IssuesController(IIssueService issueService)
    {
        _issueService = issueService;
    }

    [Authorize(Policy = "issue.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] IssueQueryFilter issueQueryFilter)
    {
        var result = await _issueService.GetAllIssues(paginationQueryFilter, issueQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "issue.view")]
    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetDetails(long id)
    {
        var result = await _issueService.GetIssueDetails(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "issue.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] IssueRequestDto issueRequestDto)
    {
        var result = await _issueService.InsertIssue(issueRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }
}