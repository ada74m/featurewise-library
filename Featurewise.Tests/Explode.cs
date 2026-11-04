namespace Featurewise.Tests;

// Always fails, so tests can see how Features handles a failing feature.
public class Explode : Command<Explode, Explode.Args, bool>
{
    public record Args : Link<Explode, bool>;

    public override Task<bool> Invoke(Args args, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
}
