using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Managers may fire anyone but themselves, so the rule depends on the link's arguments as well as the caller.
public class FireAuthorization : AuthorizationHandler<InvokeRequirement, Fire.Args>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InvokeRequirement requirement, Fire.Args link)
    {
        if (context.User.IsInRole("manager") && link.EmployeeId != context.User.Identity?.Name)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
