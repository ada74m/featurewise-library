using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// An event handler, which has no caller waiting for an answer and so returns Nil.
[AllowAnonymous]
public class HandleEmployeeLeft : Feature<HandleEmployeeLeft, HandleEmployeeLeft.Args, Nil>
{
    public record Args(string EmployeeId) : Link<HandleEmployeeLeft, Nil>;

    public override Task<Nil> Invoke(Args args, CancellationToken cancellationToken) =>
        Task.FromResult(Nil.Value);
}
