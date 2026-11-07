namespace Featurewise.Tests;

public class Probe : Query<Probe.Args, ScopeProbe>
{
    public record Args : Link<Probe, ScopeProbe>;

    private readonly ScopeProbe _scopeProbe;

    public Probe(ScopeProbe scopeProbe)
    {
        _scopeProbe = scopeProbe;
    }

    public override Task<ScopeProbe> Invoke(Args args, CancellationToken cancellationToken) => Task.FromResult(_scopeProbe);
}
