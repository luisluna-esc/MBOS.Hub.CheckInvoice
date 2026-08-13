using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionsController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    [Authorize(Policy = "permission.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] PermissionQueryFilter permissionQueryFilter)
    {
        var result = await _permissionService.GetAllPermissions(paginationQueryFilter, permissionQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "permission.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] PermissionDto permissionDto)
    {
        var result = await _permissionService.InsertPermission(permissionDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "permission.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] PermissionDto permissionDto)
    {
        var result = await _permissionService.UpdatePermission(id, permissionDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "permission.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _permissionService.DeletePermission(id);
        return StatusCode((int)result.StatusCode, result);
    }
}