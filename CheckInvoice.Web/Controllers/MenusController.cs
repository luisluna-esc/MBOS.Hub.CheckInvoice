using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenusController : ControllerBase
{
    private readonly IMenuService _menuService;

    public MenusController(IMenuService menuService)
    {
        _menuService = menuService;
    }

    [Authorize(Policy = "menu.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] MenuQueryFilter menuQueryFilter)
    {
        var result = await _menuService.GetAllMenus(paginationQueryFilter, menuQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize]
    [HttpGet("my-menu")]
    public async Task<IActionResult> GetMyMenu()
    {
        var result = await _menuService.GetMyMenu();
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "menu.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] MenuDto menuDto)
    {
        var result = await _menuService.InsertMenu(menuDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "menu.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] MenuDto menuDto)
    {
        var result = await _menuService.UpdateMenu(id, menuDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "menu.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _menuService.DeleteMenu(id);
        return StatusCode((int)result.StatusCode, result);
    }
}