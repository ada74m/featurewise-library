using Microsoft.AspNetCore.Authorization;

namespace Featurewise.Tests;

// Sends a reminder once per request, recognising redeliveries by their idempotency key, as a
// repeatable command or event handler would.
[AllowAnonymous]
public class Remind(SentReminders sent, Invocation invocation) : Feature<Remind, Remind.Args, Remind.Result>
{
    public record Args(string EmployeeId) : Link<Remind, Result>;

    public abstract record Result
    {
        private Result()
        {
        }

        public record Sent(string? IdempotencyKey) : Result;

        public record AlreadySent : Result;
    }

    public override Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        if (invocation.IdempotencyKey is not null && !sent.ByKey.TryAdd(invocation.IdempotencyKey, args.EmployeeId))
            return Task.FromResult<Result>(new Result.AlreadySent());

        return Task.FromResult<Result>(new Result.Sent(invocation.IdempotencyKey));
    }
}
