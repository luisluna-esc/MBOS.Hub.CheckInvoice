using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiscountsController : ControllerBase
{
    private readonly IDiscountService _discountService;

    public DiscountsController(IDiscountService discountService)
    {
        _discountService = discountService;
    }

    [Authorize(Policy = "discount.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] DiscountQueryFilter discountQueryFilter)
    {
        var result = await _discountService.GetAllDiscounts(paginationQueryFilter, discountQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "discount.create")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] DiscountDto discountDto)
    {
        var result = await _discountService.InsertDiscount(discountDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "discount.manage")]
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] DiscountStatusUpdateDto discountStatusUpdateDto)
    {
        var result = await _discountService.UpdateDiscountStatus(id, discountStatusUpdateDto);
        return StatusCode((int)result.StatusCode, result);
    }
}