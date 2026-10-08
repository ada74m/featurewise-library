using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Throws OperationCanceledException without its caller cancelling, as a timeout of its own would.
[AllowAnonymous]
public class GiveUp : Feature<GiveUp, GiveUp.Args, bool>
{
    public record Args : Link<GiveUp, bool>;

    public override Task<bool> Invoke(Args args, CancellationToken cancellationToken) =>
        throw new OperationCanceledException("gave up waiting");
}
