using CheckInvoice.Application.Dtos.Governance;
using CheckInvoice.Application.Interfaces.Governance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

// Buzón del usuario: pendientes por rol y notificaciones (sin leer y vistas).
[ApiController]
[Route("api/[controller]")]
public class InboxController : ControllerBase
{
    private readonly IInboxService _inboxService;

    public InboxController(IInboxService inboxService)
    {
        _inboxService = inboxService;
    }

    [Authorize]
    [HttpGet("counts")]
    public async Task<IActionResult> GetCounts()
    {
        var result = await _inboxService.GetCounts();
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetItems([FromQuery] InboxQueryDto query)
    {
        var result = await _inboxService.GetItems(query);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize]
    [HttpPost("seen")]
    public async Task<IActionResult> MarkSeen([FromBody] InboxSeenDto body)
    {
        var result = await _inboxService.MarkSeen(body.Keys is { Count: > 0 } ? body.Keys : null);
        return StatusCode((int)result.StatusCode, result);
    }
}
