namespace Featurewise.Tests;

// Claims Fire's name, which RegisterFeature must reject alongside Fire.
[FeatureName("Fire")]
public class Impostor : Command<Impostor.Args, bool>
{
    public record Args : Link<Impostor, bool>;

    public override Task<bool> Invoke(Args args, CancellationToken cancellationToken) => Task.FromResult(true);
}
