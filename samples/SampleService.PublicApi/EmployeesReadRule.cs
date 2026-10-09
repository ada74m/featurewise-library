using Featurewise;
using Microsoft.AspNetCore.Authorization;

namespace SampleService.PublicApi;

// Clients whose token grants hr/employees.read may read employees through the public API. API Gateway
// checks the scope too; this keeps a misconfigured route from opening the feature.
public class EmployeesReadRule : AuthorizationHandler<InvokeRequirement, EmployeeDetails.Args>
{
    public const string Scope = "hr/employees.read";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InvokeRequirement requirement, EmployeeDetails.Args link)
    {
        if (context.User.HasClaim(ClientCaller.ScopeClaim, Scope))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
