using Microsoft.AspNetCore.Authorization;

namespace CheckInvoice.Infrastructure.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.Code))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}