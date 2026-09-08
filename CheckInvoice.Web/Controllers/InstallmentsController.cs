using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.Application.Interfaces.Finance;
using CheckInvoice.core.QueryFilters.Finance;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstallmentsController : ControllerBase
{
    private readonly IInstallmentService _installmentService;

    public InstallmentsController(IInstallmentService installmentService)
    {
        _installmentService = installmentService;
    }

    [Authorize(Policy = "installment.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] InstallmentQueryFilter installmentQueryFilter)
    {
        var result = await _installmentService.GetAllInstallments(paginationQueryFilter, installmentQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "installment.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] InstallmentDto installmentDto)
    {
        var result = await _installmentService.InsertInstallment(installmentDto);
        return StatusCode((int)result.StatusCode, result);
    }
}