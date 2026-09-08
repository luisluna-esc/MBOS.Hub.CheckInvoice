using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CountriesController : ControllerBase
{
    private readonly ICountryService _countryService;

    public CountriesController(ICountryService countryService)
    {
        _countryService = countryService;
    }

    [Authorize(Policy = "country.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] CountryQueryFilter countryQueryFilter)
    {
        var result = await _countryService.GetAllCountries(paginationQueryFilter, countryQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "country.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] CountryDto countryDto)
    {
        var result = await _countryService.InsertCountry(countryDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "country.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] CountryDto countryDto)
    {
        var result = await _countryService.UpdateCountry(id, countryDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "country.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _countryService.DeleteCountry(id);
        return StatusCode((int)result.StatusCode, result);
    }
}