using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

[AllowAnonymous]
public class Probe : Feature<Probe, Probe.Args, ScopeProbe>
{
    public record Args : Link<Probe, ScopeProbe>;

    private readonly ScopeProbe _scopeProbe;

    public Probe(ScopeProbe scopeProbe)
    {
        _scopeProbe = scopeProbe;
    }

    public override Task<ScopeProbe> Invoke(Args args, CancellationToken cancellationToken) => Task.FromResult(_scopeProbe);
}
