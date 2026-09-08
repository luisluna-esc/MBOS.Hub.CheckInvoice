using Microsoft.AspNetCore.Authorization;

namespace CheckInvoice.Infrastructure.Authorization;

// Every controller endpoint still declares a specific policy (e.g. "receipt.view",
// "client.manage") via [Authorize(Policy = "...")], but the permission catalog was
// simplified (2026-08-25) to just two grantable codes:
//   - "visit": read-only, satisfies any policy ending in ".view".
//   - "work" : full access, satisfies every policy.
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private const string ViewOnlyClaim = "visita";
    private const string FullAccessClaim = "trabajo";

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var isViewPolicy = requirement.Code.EndsWith(".view", StringComparison.Ordinal);

        if (context.User.HasClaim("permission", FullAccessClaim) ||
            (isViewPolicy && context.User.HasClaim("permission", ViewOnlyClaim)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
