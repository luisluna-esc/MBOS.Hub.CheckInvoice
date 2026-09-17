using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentTypesController : ControllerBase
{
    private readonly IDocumentTypeService _documentTypeService;

    public DocumentTypesController(IDocumentTypeService documentTypeService)
    {
        _documentTypeService = documentTypeService;
    }

    [Authorize(Policy = "document_type.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] DocumentTypeQueryFilter documentTypeQueryFilter)
    {
        var result = await _documentTypeService.GetAllDocumentTypes(paginationQueryFilter, documentTypeQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "document_type.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] DocumentTypeDto documentTypeDto)
    {
        var result = await _documentTypeService.InsertDocumentType(documentTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "document_type.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] DocumentTypeDto documentTypeDto)
    {
        var result = await _documentTypeService.UpdateDocumentType(id, documentTypeDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "document_type.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _documentTypeService.DeleteDocumentType(id);
        return StatusCode((int)result.StatusCode, result);
    }
}