using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.Application.Interfaces.Finance;
using CheckInvoice.core.QueryFilters.Finance;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [Authorize(Policy = "payment.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] PaymentQueryFilter paymentQueryFilter)
    {
        var result = await _paymentService.GetAllPayments(paginationQueryFilter, paymentQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "payment.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] PaymentDto paymentDto)
    {
        var result = await _paymentService.InsertPayment(paymentDto);
        return StatusCode((int)result.StatusCode, result);
    }
}