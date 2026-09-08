using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationsController : ControllerBase
{
    private readonly IConfigurationService _configurationService;

    public ConfigurationsController(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    [Authorize(Policy = "configuration.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ConfigurationQueryFilter configurationQueryFilter)
    {
        var result = await _configurationService.GetAllConfigurations(paginationQueryFilter, configurationQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "configuration.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ConfigurationDto configurationDto)
    {
        var result = await _configurationService.InsertConfiguration(configurationDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "configuration.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] ConfigurationDto configurationDto)
    {
        var result = await _configurationService.UpdateConfiguration(id, configurationDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "configuration.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _configurationService.DeleteConfiguration(id);
        return StatusCode((int)result.StatusCode, result);
    }
}