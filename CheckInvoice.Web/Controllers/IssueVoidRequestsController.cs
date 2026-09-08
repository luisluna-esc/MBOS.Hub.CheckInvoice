using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IssueVoidRequestsController : ControllerBase
{
    private readonly IIssueVoidRequestService _issueVoidRequestService;

    public IssueVoidRequestsController(IIssueVoidRequestService issueVoidRequestService)
    {
        _issueVoidRequestService = issueVoidRequestService;
    }

    [Authorize(Policy = "issue.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] IssueVoidRequestQueryFilter issueVoidRequestQueryFilter)
    {
        var result = await _issueVoidRequestService.GetAllVoidRequests(paginationQueryFilter, issueVoidRequestQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "issue.create")]
    [HttpPost]
    public async Task<IActionResult> RequestVoid([FromBody] IssueVoidRequestCreateDto issueVoidRequestCreateDto)
    {
        var result = await _issueVoidRequestService.RequestVoid(issueVoidRequestCreateDto);
        return StatusCode((int)result.StatusCode, result);
    }

    // Solo Contador o M-BOS pueden aprobar/rechazar: por rol directo (nativo de ASP.NET,
    // ya viene en el JWT como ClaimTypes.Role), no por el sistema de políticas
    // visita/trabajo, que no distingue "quién pide" de "quién aprueba".
    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPut("{id}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] IssueVoidRequestReviewDto issueVoidRequestReviewDto)
    {
        var result = await _issueVoidRequestService.ApproveVoidRequest(id, issueVoidRequestReviewDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPut("{id}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] IssueVoidRequestReviewDto issueVoidRequestReviewDto)
    {
        var result = await _issueVoidRequestService.RejectVoidRequest(id, issueVoidRequestReviewDto);
        return StatusCode((int)result.StatusCode, result);
    }
}
