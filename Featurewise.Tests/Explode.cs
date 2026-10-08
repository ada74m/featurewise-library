using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Always fails, so tests can see how Features handles a failing feature.
[AllowAnonymous]
public class Explode : Feature<Explode, Explode.Args, bool>
{
    public record Args : Link<Explode, bool>;

    public override Task<bool> Invoke(Args args, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
}
