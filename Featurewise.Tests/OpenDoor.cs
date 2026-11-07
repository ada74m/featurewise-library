using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Has [AllowAnonymous], which Featurewise doesn't honour, so RegisterFeature must reject it.
[AllowAnonymous]
public class OpenDoor : Command<OpenDoor.Args, bool>
{
    public record Args : Link<OpenDoor, bool>;

    public override Task<bool> Invoke(Args args, CancellationToken cancellationToken) => Task.FromResult(true);
}
