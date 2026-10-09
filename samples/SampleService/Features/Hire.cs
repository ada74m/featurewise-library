using System.Security.Cryptography;
using System.Text;
using Featurewise;

namespace SampleService;

// Hires someone. With an idempotency key, a retried request hires them once: the key decides their id.
public class Hire(IEmployeeStore employees, Invocation invocation, TimeProvider time) : Feature<Hire, Hire.Args, Hire.Result>
{
    public record Args(string Name, string? ManagerId) : Link<Hire, Result>;

    public abstract record Result
    {
        private Result()
        {
        }

        public record Hired(string EmployeeId) : Result;

        public record ManagerNotFound(string ManagerId) : Result;
    }

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        if (args.ManagerId is not null
            && await employees.Find(args.ManagerId, cancellationToken) is not { Status: EmploymentStatus.Active })
            return new Result.ManagerNotFound(args.ManagerId);

        var id = invocation.IdempotencyKey is { } key ? IdFrom(key) : Guid.NewGuid().ToString("N");
        if (await employees.Find(id, cancellationToken) is null)
        {
            var firstReview = Departures.Today(time).AddMonths(3);
            await employees.Save(new Employee(id, args.Name, args.ManagerId, EmploymentStatus.Active, firstReview), cancellationToken);
        }

        return new Result.Hired(id);
    }

    private static string IdFrom(string idempotencyKey) =>
        new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey))[..16]).ToString("N");
}
