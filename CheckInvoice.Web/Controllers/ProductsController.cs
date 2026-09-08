using CheckInvoice.Application.Dtos.Products;
using CheckInvoice.Application.Interfaces.Products;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [Authorize(Policy = "product.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ProductQueryFilter productQueryFilter)
    {
        var result = await _productService.GetAllProducts(paginationQueryFilter, productQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "product.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ProductDto productDto)
    {
        var result = await _productService.InsertProduct(productDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "product.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] ProductDto productDto)
    {
        var result = await _productService.UpdateProduct(id, productDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "product.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _productService.DeleteProduct(id);
        return StatusCode((int)result.StatusCode, result);
    }
}