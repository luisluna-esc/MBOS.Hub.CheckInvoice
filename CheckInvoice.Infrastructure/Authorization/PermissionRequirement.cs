using Microsoft.AspNetCore.Authorization;

namespace CheckInvoice.Infrastructure.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Code { get; }

    public PermissionRequirement(string code)
    {
        Code = code;
    }
}