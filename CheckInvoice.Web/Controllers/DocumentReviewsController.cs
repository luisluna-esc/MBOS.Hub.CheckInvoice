using CheckInvoice.Application.Dtos.Governance;
using CheckInvoice.Application.Interfaces.Governance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

// Revisión y firma digital de documentos de almacén. {type}: receipt, issue o transfer.
// Verifican y firman el Contador y el Tesorero (el Auxiliar Contador no); aprueba, rechaza o
// reabre el Contador.
[ApiController]
[Route("api/[controller]/{type}/{id:long}")]
public class DocumentReviewsController : ControllerBase
{
    private readonly IDocumentReviewService _documentReviewService;

    public DocumentReviewsController(IDocumentReviewService documentReviewService)
    {
        _documentReviewService = documentReviewService;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Get(string type, long id)
    {
        var result = await _documentReviewService.GetReview(type, id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,Tesorero,M-BOS")]
    [HttpPost("verify")]
    public async Task<IActionResult> Verify(string type, long id, [FromBody] DocumentSignDto body)
    {
        var result = await _documentReviewService.Verify(type, id, body.Password);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPost("approve")]
    public async Task<IActionResult> Approve(string type, long id, [FromBody] DocumentSignDto body)
    {
        var result = await _documentReviewService.Approve(type, id, body.Password, body.Reason);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPost("reject")]
    public async Task<IActionResult> Reject(string type, long id, [FromBody] DocumentSignDto body)
    {
        var result = await _documentReviewService.Reject(type, id, body.Password, body.Reason ?? string.Empty);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPost("reopen")]
    public async Task<IActionResult> Reopen(string type, long id, [FromBody] DocumentSignDto body)
    {
        var result = await _documentReviewService.Reopen(type, id, body.Password, body.Reason ?? string.Empty);
        return StatusCode((int)result.StatusCode, result);
    }
}
