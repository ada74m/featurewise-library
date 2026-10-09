using Featurewise;
using Microsoft.AspNetCore.Authorization;

namespace SampleService;

// Opens a feature to the service's own jobs and workers, which invoke as a SystemCaller, and to nobody else.
public class AnySystemCaller<TArgs> : AuthorizationHandler<InvokeRequirement, TArgs> where TArgs : Link
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InvokeRequirement requirement, TArgs link)
    {
        if (SystemCaller.IsSystem(context.User))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
