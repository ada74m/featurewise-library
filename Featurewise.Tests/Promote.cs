namespace Featurewise.Tests;

// Takes Fire's arguments, which link to Fire; compiles, but RegisterFeature must reject it.
public class Promote : Command<Fire.Args, Fire.Result>
{
    public override Task<Fire.Result> Invoke(Fire.Args args, CancellationToken cancellationToken) => throw new NotImplementedException();
}
