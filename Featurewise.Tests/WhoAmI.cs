using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Returns its caller's name, so tests can see a feature take its caller as a dependency.
[AllowAnonymous]
public class WhoAmI(Invocation invocation) : Feature<WhoAmI, WhoAmI.Args, string?>
{
    public record Args : Link<WhoAmI, string?>;

    public override Task<string?> Invoke(Args args, CancellationToken cancellationToken) =>
        Task.FromResult(invocation.Caller.Identity?.Name);
}
