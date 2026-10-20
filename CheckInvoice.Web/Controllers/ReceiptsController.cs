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

    // La abre el QR impreso en la nota de ingreso (con sesión iniciada).
    [Authorize(Policy = "receipt.view")]
    [HttpGet("{id}/summary")]
    public async Task<IActionResult> GetSummary(long id)
    {
        var result = await _receiptService.GetReceiptSummary(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ReceiptRequestDto receiptRequestDto)
    {
        var result = await _receiptService.InsertReceipt(receiptRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    // Inventario inicial desde Excel: lo carga M-BOS una sola vez por almacén.
    [Authorize(Roles = "M-BOS")]
    [HttpGet("initial-inventory/template")]
    public async Task<IActionResult> GetInitialInventoryTemplate()
    {
        var bytes = await _receiptService.GetInitialInventoryTemplate();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "plantilla-inventario-inicial.xlsx");
    }

    // confirm=false solo revisa el archivo; confirm=true lo carga.
    [Authorize(Roles = "M-BOS")]
    [HttpPost("initial-inventory")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ImportInitialInventory(IFormFile file, [FromForm] long warehouseId, [FromForm] bool confirm)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;
        var result = await _receiptService.ImportInitialInventory(stream, warehouseId, confirm);
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