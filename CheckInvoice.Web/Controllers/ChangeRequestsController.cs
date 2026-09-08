using CheckInvoice.Application.Dtos.Governance;
using CheckInvoice.Application.Interfaces.Governance;
using CheckInvoice.core.QueryFilters.Governance;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChangeRequestsController : ControllerBase
{
    private readonly IChangeRequestService _changeRequestService;

    public ChangeRequestsController(IChangeRequestService changeRequestService)
    {
        _changeRequestService = changeRequestService;
    }

    [Authorize(Policy = "change_request.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ChangeRequestQueryFilter changeRequestQueryFilter)
    {
        var result = await _changeRequestService.GetAllChangeRequests(paginationQueryFilter, changeRequestQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "change_request.view")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _changeRequestService.GetChangeRequestById(id);
        return StatusCode((int)result.StatusCode, result);
    }

    // Permission is resource-specific (e.g. "receipt.request_change") and depends on
    // TableName in the body, so it is enforced inside the service rather than declaratively here.
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> RequestChange([FromBody] ChangeRequestCreateDto changeRequestCreateDto)
    {
        var result = await _changeRequestService.RequestChange(changeRequestCreateDto);
        return StatusCode((int)result.StatusCode, result);
    }

    // Permission is resource-specific (e.g. "receipt.approve_change") and depends on the
    // change request's own TableName, so it is enforced inside the service rather than declaratively here.
    [Authorize]
    [HttpPut("{id}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ChangeRequestReviewDto changeRequestReviewDto)
    {
        var result = await _changeRequestService.ApproveChangeRequest(id, changeRequestReviewDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize]
    [HttpPut("{id}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] ChangeRequestReviewDto changeRequestReviewDto)
    {
        var result = await _changeRequestService.RejectChangeRequest(id, changeRequestReviewDto);
        return StatusCode((int)result.StatusCode, result);
    }
}
