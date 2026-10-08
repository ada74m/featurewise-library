using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Returns what it knows about its invocation, so tests can see Invocation filled in.
[AllowAnonymous]
public class DescribeInvocation(Invocation invocation) : Feature<DescribeInvocation, DescribeInvocation.Args, DescribeInvocation.Result>
{
    public record Args : Link<DescribeInvocation, Result>;

    public record Result(string? Caller, string? IdempotencyKey, string? Id, string? CausedBy, bool CanBeCancelled);

    public override Task<Result> Invoke(Args args, CancellationToken cancellationToken) =>
        Task.FromResult(new Result(
            invocation.Caller.Identity?.Name,
            invocation.IdempotencyKey,
            invocation.Id,
            invocation.CausedBy,
            invocation.CancellationToken.CanBeCanceled));
}
