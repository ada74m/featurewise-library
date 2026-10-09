using Featurewise;
using Microsoft.AspNetCore.Authorization;

namespace SampleService;

// Managers may fire anyone but themselves.
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
