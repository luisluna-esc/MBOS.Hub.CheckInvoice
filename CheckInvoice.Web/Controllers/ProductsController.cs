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

    // Sin sesión: la foto se pide desde una etiqueta <img> (que no manda el token) y la ve la
    // página del QR de la nota. Es la foto del producto, no un dato privado. La URL lleva la
    // versión (?v=), así que el navegador puede guardarla en caché sin mostrar una vieja.
    [AllowAnonymous]
    [HttpGet("{id}/image")]
    public async Task<IActionResult> GetImage(long id)
    {
        var image = await _productService.GetProductImage(id);
        if (image is null)
        {
            return NotFound();
        }
        Response.Headers.CacheControl = "public, max-age=604800";
        return File(image.Value.Content, image.Value.ContentType);
    }

    [Authorize(Policy = "product.manage")]
    [HttpPut("{id}/image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> SetImage(long id, IFormFile file)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var result = await _productService.SetProductImage(id, stream.ToArray(), file.ContentType);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "product.manage")]
    [HttpDelete("{id}/image")]
    public async Task<IActionResult> DeleteImage(long id)
    {
        var result = await _productService.DeleteProductImage(id);
        return StatusCode((int)result.StatusCode, result);
    }
}