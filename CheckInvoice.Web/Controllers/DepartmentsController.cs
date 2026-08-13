using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [Authorize(Policy = "department.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] DepartmentQueryFilter departmentQueryFilter)
    {
        var result = await _departmentService.GetAllDepartments(paginationQueryFilter, departmentQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "department.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] DepartmentDto departmentDto)
    {
        var result = await _departmentService.InsertDepartment(departmentDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "department.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] DepartmentDto departmentDto)
    {
        var result = await _departmentService.UpdateDepartment(id, departmentDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "department.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _departmentService.DeleteDepartment(id);
        return StatusCode((int)result.StatusCode, result);
    }
}