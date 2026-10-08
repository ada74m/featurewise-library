using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Has a result with a sealed case, so tests can see how sealed cases are serialized.
[AllowAnonymous]
public class TossCoin : Feature<TossCoin, TossCoin.Args, TossCoin.Result>
{
    public record Args : Link<TossCoin, Result>;

    public abstract record Result
    {
        private Result()
        {
        }

        public sealed record Heads : Result;

        public record Tails : Result;
    }

    public override Task<Result> Invoke(Args args, CancellationToken cancellationToken) =>
        Task.FromResult<Result>(new Result.Heads());
}
