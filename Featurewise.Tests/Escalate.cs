using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Invokes Fire through a Features it was given, which Features must reject.
[AllowAnonymous]
public class Escalate(Features features, Invocation invocation) : Feature<Escalate, Escalate.Args, Fire.Result>
{
    public record Args(string EmployeeId) : Link<Escalate, Fire.Result>;

    public override Task<Fire.Result> Invoke(Args args, CancellationToken cancellationToken) =>
        features.Invoke(new Fire.Args(args.EmployeeId), invocation.Caller, cancellationToken);
}
