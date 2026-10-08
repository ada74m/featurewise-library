namespace Featurewise.Tests;

// Names Fire as itself; compiles, but RegisterFeature must reject it.
public class Promote : Feature<Fire, Fire.Args, Fire.Result>
{
    public override Task<Fire.Result> Invoke(Fire.Args args, CancellationToken cancellationToken) => throw new NotImplementedException();
}
