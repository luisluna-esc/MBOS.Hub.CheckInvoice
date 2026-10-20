using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

// Correcciones de Salidas: igual que las de Entradas, las pide quien trabaja en almacén y las
// aprueba o rechaza el Contador.
[ApiController]
[Route("api/[controller]")]
public class IssueEditsController : ControllerBase
{
    private readonly IIssueEditService _issueEditService;

    public IssueEditsController(IIssueEditService issueEditService)
    {
        _issueEditService = issueEditService;
    }

    [Authorize(Policy = "issue.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] long? issueId)
    {
        var result = await _issueEditService.GetEdits(status, issueId);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "issue.create")]
    [HttpPost]
    public async Task<IActionResult> CreateRequest([FromBody] IssueEditRequestDto request)
    {
        var result = await _issueEditService.RequestEdit(request);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPut("{id}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ReceiptEditReviewDto body)
    {
        var result = await _issueEditService.Approve(id, body.ReviewNotes);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPut("{id}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] ReceiptEditReviewDto body)
    {
        var result = await _issueEditService.Reject(id, body.ReviewNotes);
        return StatusCode((int)result.StatusCode, result);
    }
}
