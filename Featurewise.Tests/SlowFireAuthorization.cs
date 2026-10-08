using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Waits until the invocation is cancelled, as a rule would on a slow query given the invocation's token.
public class SlowFireAuthorization(Invocation invocation) : AuthorizationHandler<InvokeRequirement, Fire.Args>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InvokeRequirement requirement, Fire.Args link) =>
        Task.Delay(Timeout.Infinite, invocation.CancellationToken);
}
