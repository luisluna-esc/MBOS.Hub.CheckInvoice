using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IRolePermissionService _rolePermissionService;

    public RolesController(IRoleService roleService, IRolePermissionService rolePermissionService)
    {
        _roleService = roleService;
        _rolePermissionService = rolePermissionService;
    }

    [Authorize(Policy = "role.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] RoleQueryFilter roleQueryFilter)
    {
        var result = await _roleService.GetAllRoles(paginationQueryFilter, roleQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "role.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] RoleDto roleDto)
    {
        var result = await _roleService.InsertRole(roleDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "role.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] RoleDto roleDto)
    {
        var result = await _roleService.UpdateRole(id, roleDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "role.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _roleService.DeleteRole(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "role.view")]
    [HttpGet("{id}/permissions")]
    public async Task<IActionResult> GetPermissions(long id)
    {
        var result = await _rolePermissionService.GetPermissionsByRole(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "role.manage")]
    [HttpPost("{id}/permissions/{permissionId}")]
    public async Task<IActionResult> AssignPermission(long id, long permissionId)
    {
        var result = await _rolePermissionService.AssignPermission(id, permissionId);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "role.manage")]
    [HttpDelete("{id}/permissions/{permissionId}")]
    public async Task<IActionResult> RemovePermission(long id, long permissionId)
    {
        var result = await _rolePermissionService.RemovePermission(id, permissionId);
        return StatusCode((int)result.StatusCode, result);
    }
}