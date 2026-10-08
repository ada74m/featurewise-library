using Microsoft.Extensions.Logging;

namespace Featurewise.Tests;

// Fires anyone except "nobody", who isn't found, so tests can see a result with cases.
public class Fire(ILogger<Fire> logger) : Feature<Fire, Fire.Args, Fire.Result>
{
    public const string Nobody = "nobody";

    public record Args(string EmployeeId) : Link<Fire, Result>;

    public abstract record Result
    {
        private Result()
        {
        }

        public record Fired : Result;

        public record NotFound(string EmployeeId) : Result;
    }

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        logger.LogInformation("Firing {EmployeeId}", args.EmployeeId);
        await Task.Delay(100, cancellationToken);
        return args.EmployeeId == Nobody ? new Result.NotFound(args.EmployeeId) : new Result.Fired();
    }
}
