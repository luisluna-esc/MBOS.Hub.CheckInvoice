using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReceiptVoidRequestsController : ControllerBase
{
    private readonly IReceiptVoidRequestService _receiptVoidRequestService;

    public ReceiptVoidRequestsController(IReceiptVoidRequestService receiptVoidRequestService)
    {
        _receiptVoidRequestService = receiptVoidRequestService;
    }

    [Authorize(Policy = "receipt.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ReceiptVoidRequestQueryFilter receiptVoidRequestQueryFilter)
    {
        var result = await _receiptVoidRequestService.GetAllVoidRequests(paginationQueryFilter, receiptVoidRequestQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt.create")]
    [HttpPost]
    public async Task<IActionResult> RequestVoid([FromBody] ReceiptVoidRequestCreateDto receiptVoidRequestCreateDto)
    {
        var result = await _receiptVoidRequestService.RequestVoid(receiptVoidRequestCreateDto);
        return StatusCode((int)result.StatusCode, result);
    }

    // Solo Contador o M-BOS pueden aprobar/rechazar: mismo criterio que IssueVoidRequestsController
    // (por rol directo, no por el sistema de políticas visita/trabajo, que no distingue "quién
    // pide" de "quién aprueba").
    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPut("{id}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ReceiptVoidRequestReviewDto receiptVoidRequestReviewDto)
    {
        var result = await _receiptVoidRequestService.ApproveVoidRequest(id, receiptVoidRequestReviewDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = "Contador,M-BOS")]
    [HttpPut("{id}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] ReceiptVoidRequestReviewDto receiptVoidRequestReviewDto)
    {
        var result = await _receiptVoidRequestService.RejectVoidRequest(id, receiptVoidRequestReviewDto);
        return StatusCode((int)result.StatusCode, result);
    }
}
