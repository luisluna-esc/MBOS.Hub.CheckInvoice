using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IssueTypesController : ControllerBase
{
    private readonly IIssueTypeService _issueTypeService;

    public IssueTypesController(IIssueTypeService issueTypeService)
    {
        _issueTypeService = issueTypeService;
    }

    [Authorize(Policy = "issue_type.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] IssueTypeQueryFilter issueTypeQueryFilter)
    {
        var result = await _issueTypeService.GetAllIssueTypes(paginationQueryFilter, issueTypeQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "issue_type.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] IssueTypeDto issueTypeDto)
    {
        var result = await _issueTypeService.InsertIssueType(issueTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "issue_type.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] IssueTypeDto issueTypeDto)
    {
        var result = await _issueTypeService.UpdateIssueType(id, issueTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "issue_type.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _issueTypeService.DeleteIssueType(id);
        return StatusCode((int)result.StatusCode, result);
    }
}