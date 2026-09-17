using System.Security.Claims;
using CheckInvoice.Application.Interfaces.Security;
using Microsoft.AspNetCore.Http;

namespace CheckInvoice.Infrastructure.Security;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long? AppUserId
    {
        get
        {
            var claimValue = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(claimValue, out var appUserId) ? appUserId : null;
        }
    }

    public bool HasPermission(string code) =>
        _httpContextAccessor.HttpContext?.User?.HasClaim("permission", code) ?? false;
}