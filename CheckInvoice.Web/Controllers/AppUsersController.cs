using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppUsersController : ControllerBase
{
    private readonly IAppUserService _appUserService;
    private readonly IAppUserRoleService _appUserRoleService;

    public AppUsersController(IAppUserService appUserService, IAppUserRoleService appUserRoleService)
    {
        _appUserService = appUserService;
        _appUserRoleService = appUserRoleService;
    }

    [Authorize(Policy = "appuser.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] AppUserQueryFilter appUserQueryFilter)
    {
        var result = await _appUserService.GetAllAppUsers(paginationQueryFilter, appUserQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "appuser.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] AppUserDto appUserDto)
    {
        var result = await _appUserService.InsertAppUser(appUserDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "appuser.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] AppUserDto appUserDto)
    {
        var result = await _appUserService.UpdateAppUser(id, appUserDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "appuser.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _appUserService.DeleteAppUser(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "appuser.view")]
    [HttpGet("{id}/roles")]
    public async Task<IActionResult> GetRoles(long id)
    {
        var result = await _appUserRoleService.GetRolesByAppUser(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "appuser.manage")]
    [HttpPost("{id}/roles/{roleId}")]
    public async Task<IActionResult> AssignRole(long id, long roleId)
    {
        var result = await _appUserRoleService.AssignRole(id, roleId);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "appuser.manage")]
    [HttpDelete("{id}/roles/{roleId}")]
    public async Task<IActionResult> RemoveRole(long id, long roleId)
    {
        var result = await _appUserRoleService.RemoveRole(id, roleId);
        return StatusCode((int)result.StatusCode, result);
    }
}