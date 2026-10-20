using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

// Correcciones de Entradas: igual que las anulaciones, las pide quien trabaja en almacén y las
// aprueba o rechaza el Contador.
[ApiController]
[Route("api/[controller]")]
public class ReceiptEditsController : ControllerBase
{
    private readonly IReceiptEditService _receiptEditService;

    public ReceiptEditsController(IReceiptEditService receiptEditService)
    {
        _receiptEditService = receiptEditService;
    }

    [Authorize(Policy = "receipt.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] long? receiptId)
    {
        var result = await _receiptEditService.GetEdits(status, receiptId);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt.create")]
    [HttpPost]
    public async Task<IActionResult> CreateRequest([FromBody] ReceiptEditRequestDto request)
    {
        var result = await _receiptEditService.RequestEdit(request);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPut("{id}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ReceiptEditReviewDto body)
    {
        var result = await _receiptEditService.Approve(id, body.ReviewNotes);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPut("{id}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] ReceiptEditReviewDto body)
    {
        var result = await _receiptEditService.Reject(id, body.ReviewNotes);
        return StatusCode((int)result.StatusCode, result);
    }
}
