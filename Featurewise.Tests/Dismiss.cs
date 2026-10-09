namespace Featurewise.Tests;

// Depends on Fire directly, which Features must reject.
public class Dismiss(Fire fire) : Feature<Dismiss, Dismiss.Args, Fire.Result>
{
    public record Args(string EmployeeId) : Link<Dismiss, Fire.Result>;

    public override Task<Fire.Result> Invoke(Args args, CancellationToken cancellationToken) =>
        fire.Invoke(new Fire.Args(args.EmployeeId), cancellationToken);
}
