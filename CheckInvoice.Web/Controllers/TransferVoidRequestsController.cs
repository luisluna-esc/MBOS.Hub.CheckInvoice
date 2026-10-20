using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

// Anulaciones de Transferencias: se piden con motivo y las aprueba o rechaza el Contador.
[ApiController]
[Route("api/[controller]")]
public class TransferVoidRequestsController : ControllerBase
{
    private readonly ITransferVoidRequestService _service;

    public TransferVoidRequestsController(ITransferVoidRequestService service)
    {
        _service = service;
    }

    [Authorize(Policy = "transfer.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] long? transferId)
    {
        var result = await _service.GetAll(status, transferId);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "transfer.create")]
    [HttpPost]
    public async Task<IActionResult> CreateRequest([FromBody] TransferVoidRequestCreateDto dto)
    {
        var result = await _service.RequestVoid(dto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPut("{id}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] TransferVoidReviewDto body)
    {
        var result = await _service.Approve(id, body.ReviewNotes);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPut("{id}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] TransferVoidReviewDto body)
    {
        var result = await _service.Reject(id, body.ReviewNotes);
        return StatusCode((int)result.StatusCode, result);
    }
}
