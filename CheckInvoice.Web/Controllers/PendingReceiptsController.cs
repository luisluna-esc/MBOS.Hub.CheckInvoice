using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Reports;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

// Notas de ingreso sin factura y sus entregas: no tocan el stock ni los reportes hasta unir la factura.
[ApiController]
[Route("api/[controller]")]
public class PendingReceiptsController : ControllerBase
{
    private readonly IPendingReceiptService _service;
    private readonly IReportService _reportService;

    public PendingReceiptsController(IPendingReceiptService service, IReportService reportService)
    {
        _service = service;
        _reportService = reportService;
    }

    [Authorize(Policy = "receipt.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] PendingReceiptQueryFilter filter)
    {
        var result = await _service.GetAll(paginationQueryFilter, filter);
        return StatusCode((int)result.StatusCode, result);
    }

    // Reporte de pendientes (PDF) con los mismos filtros de la lista.
    [Authorize(Policy = "receipt.view")]
    [HttpGet("pdf")]
    public async Task<IActionResult> GetPdf([FromQuery] PendingReceiptQueryFilter filter)
    {
        var bytes = await _reportService.GeneratePendingReceiptsPdf(filter);
        return File(bytes, "application/pdf", "ingresos-pendientes-de-factura.pdf");
    }

    [Authorize(Policy = "receipt.view")]
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetById(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] PendingReceiptRequestDto request)
    {
        var result = await _service.Insert(request);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "receipt.create")]
    [HttpPut("{id}/void")]
    public async Task<IActionResult> Void(long id)
    {
        var result = await _service.Void(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "issue.create")]
    [HttpPost("{id}/deliveries")]
    public async Task<IActionResult> InsertDelivery(long id, [FromBody] PendingDeliveryRequestDto request)
    {
        var result = await _service.InsertDelivery(id, request);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "issue.create")]
    [HttpPut("deliveries/{deliveryId}/void")]
    public async Task<IActionResult> VoidDelivery(long deliveryId)
    {
        var result = await _service.VoidDelivery(deliveryId);
        return StatusCode((int)result.StatusCode, result);
    }

    // Unir la factura: las notas pasan a ser entradas y sus entregas, salidas.
    [Authorize(Policy = "receipt.create")]
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] PendingReceiptValidateRequestDto request)
    {
        var result = await _service.Validate(request);
        return StatusCode((int)result.StatusCode, result);
    }
}
