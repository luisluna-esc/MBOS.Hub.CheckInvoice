using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaTypesController : ControllerBase
{
    private readonly IMediaTypeService _mediaTypeService;

    public MediaTypesController(IMediaTypeService mediaTypeService)
    {
        _mediaTypeService = mediaTypeService;
    }

    [Authorize(Policy = "media_type.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] MediaTypeQueryFilter mediaTypeQueryFilter)
    {
        var result = await _mediaTypeService.GetAllMediaTypes(paginationQueryFilter, mediaTypeQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "media_type.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] MediaTypeDto mediaTypeDto)
    {
        var result = await _mediaTypeService.InsertMediaType(mediaTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "media_type.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] MediaTypeDto mediaTypeDto)
    {
        var result = await _mediaTypeService.UpdateMediaType(id, mediaTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "media_type.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _mediaTypeService.DeleteMediaType(id);
        return StatusCode((int)result.StatusCode, result);
    }
}