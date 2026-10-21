using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

// Salida por lote: una salida por pastor con los mismos datos, agrupadas en un lote.
[ApiController]
[Route("api/[controller]")]
public class IssueBatchesController : ControllerBase
{
    private readonly IIssueService _issueService;

    public IssueBatchesController(IIssueService issueService)
    {
        _issueService = issueService;
    }

    [Authorize(Policy = "issue.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] IssueBatchRequestDto request)
    {
        var result = await _issueService.InsertIssueBatch(request);
        return StatusCode((int)result.StatusCode, result);
    }
}
