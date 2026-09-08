using CheckInvoice.Application.Interfaces.Warehouses;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Warehouses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StocksController : ControllerBase
{
    private readonly IStockService _stockService;

    public StocksController(IStockService stockService)
    {
        _stockService = stockService;
    }

    [Authorize(Policy = "stock.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] StockQueryFilter stockQueryFilter)
    {
        var result = await _stockService.GetAllStocks(paginationQueryFilter, stockQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }
}