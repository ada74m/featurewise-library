using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Emits a link to Fire if the caller may follow it, so tests can see a link record the invocation that
// created it, and a feature offer only links its caller may follow.
[FeatureName("review-employee")]
[AllowAnonymous]
public class Review(LinkPermissions permissions) : Feature<Review, Review.Args, Review.Result>
{
    public record Args(string EmployeeId) : Link<Review, Review.Result>;

    public record Result(string Comments, Fire.Args? LinkToFire);

    public override async Task<Review.Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        var fire = new Fire.Args(args.EmployeeId);
        return new Result("you may want to let them go", await permissions.MayInvoke(fire) ? fire : null);
    }
}
