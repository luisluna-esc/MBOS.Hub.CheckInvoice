using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.Application.Interfaces.Parties;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Parties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;

    public ClientsController(IClientService clientService)
    {
        _clientService = clientService;
    }

    [Authorize(Policy = "client.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ClientQueryFilter clientQueryFilter)
    {
        var result = await _clientService.GetAllClients(paginationQueryFilter, clientQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "client.manage")]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ClientDto clientDto)
    {
        var result = await _clientService.InsertClient(clientDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "client.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] ClientDto clientDto)
    {
        var result = await _clientService.UpdateClient(id, clientDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "client.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _clientService.DeleteClient(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Policy = "client.manage")]
    [HttpPost("{id}/grant-portal-access")]
    public async Task<IActionResult> GrantPortalAccess(long id)
    {
        var result = await _clientService.GrantPortalAccess(id);
        return StatusCode((int)result.StatusCode, result);
    }
}