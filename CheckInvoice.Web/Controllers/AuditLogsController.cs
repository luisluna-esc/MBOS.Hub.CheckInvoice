using CheckInvoice.Application.Interfaces.Audit;
using CheckInvoice.core.QueryFilters.Audit;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [Authorize(Policy = "audit_log.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] AuditLogQueryFilter auditLogQueryFilter)
    {
        var result = await _auditLogService.GetAllAuditLogs(paginationQueryFilter, auditLogQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }
}