using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubDepartmentsController : ControllerBase
{
    private readonly ISubDepartmentService _subDepartmentService;

    public SubDepartmentsController(ISubDepartmentService subDepartmentService)
    {
        _subDepartmentService = subDepartmentService;
    }

    [Authorize(Policy = "sub_department.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] SubDepartmentQueryFilter subDepartmentQueryFilter)
    {
        var result = await _subDepartmentService.GetAllSubDepartments(paginationQueryFilter, subDepartmentQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "sub_department.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] SubDepartmentDto subDepartmentDto)
    {
        var result = await _subDepartmentService.InsertSubDepartment(subDepartmentDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "sub_department.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] SubDepartmentDto subDepartmentDto)
    {
        var result = await _subDepartmentService.UpdateSubDepartment(id, subDepartmentDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "sub_department.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _subDepartmentService.DeleteSubDepartment(id);
        return StatusCode((int)result.StatusCode, result);
    }
}