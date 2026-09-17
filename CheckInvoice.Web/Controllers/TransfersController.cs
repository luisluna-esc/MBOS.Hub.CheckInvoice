using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _transferService;

    public TransfersController(ITransferService transferService)
    {
        _transferService = transferService;
    }

    [Authorize(Policy = "transfer.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] TransferQueryFilter transferQueryFilter)
    {
        var result = await _transferService.GetAllTransfers(paginationQueryFilter, transferQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "transfer.view")]
    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetDetails(long id)
    {
        var result = await _transferService.GetTransferDetails(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "transfer.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] TransferRequestDto transferRequestDto)
    {
        var result = await _transferService.InsertTransfer(transferRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "transfer.create")]
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(long id)
    {
        var result = await _transferService.ApproveTransfer(id);
        return StatusCode((int)result.StatusCode, result);
    }
}