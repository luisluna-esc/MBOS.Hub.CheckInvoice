using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReceiptsController : ControllerBase
{
    private readonly IReceiptService _receiptService;

    public ReceiptsController(IReceiptService receiptService)
    {
        _receiptService = receiptService;
    }

    [Authorize(Policy = "receipt.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ReceiptQueryFilter receiptQueryFilter)
    {
        var result = await _receiptService.GetAllReceipts(paginationQueryFilter, receiptQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt.view")]
    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetDetails(long id)
    {
        var result = await _receiptService.GetReceiptDetails(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ReceiptRequestDto receiptRequestDto)
    {
        var result = await _receiptService.InsertReceipt(receiptRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt.view")]
    [HttpGet("returnable-lines/{issueId}")]
    public async Task<IActionResult> GetReturnableLines(long issueId)
    {
        var result = await _receiptService.GetReturnableIssueLines(issueId);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt.create")]
    [HttpPost("returns")]
    public async Task<IActionResult> InsertReturn([FromBody] ReceiptReturnRequestDto receiptReturnRequestDto)
    {
        var result = await _receiptService.InsertReturn(receiptReturnRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }
}