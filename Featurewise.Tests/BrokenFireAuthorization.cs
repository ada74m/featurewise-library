using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Fails as a rule would if the store it reads were down.
public class BrokenFireAuthorization : AuthorizationHandler<InvokeRequirement, Fire.Args>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InvokeRequirement requirement, Fire.Args link) =>
        throw new InvalidOperationException("employee store unavailable");
}
