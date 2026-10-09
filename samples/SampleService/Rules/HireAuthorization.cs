using Featurewise;
using Microsoft.AspNetCore.Authorization;

namespace SampleService;

// Managers may hire.
public class HireAuthorization : AuthorizationHandler<InvokeRequirement, Hire.Args>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InvokeRequirement requirement, Hire.Args link)
    {
        if (context.User.IsInRole("manager"))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
