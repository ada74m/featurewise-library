using System.Security.Cryptography;
using System.Text;
using Featurewise;
using Microsoft.AspNetCore.Authorization;

namespace SampleService;

// Hires someone, and publishes that they've joined. With an idempotency key, a retried request hires them
// once, since the key decides their id, and publishes the same event again, which subscribers drop as a
// duplicate. Publishing again also recovers an event a failed attempt didn't publish.
public class Hire(IEmployeeStore employees, IEventPublisher events, Invocation invocation, TimeProvider time) : Feature<Hire, Hire.Args, Hire.Result>
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

    // Managers may hire.
    public class Rule : AuthorizationHandler<InvokeRequirement, Args>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context, InvokeRequirement requirement, Args link)
        {
            if (context.User.IsInRole("manager"))
                context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        if (args.ManagerId is not null
            && await employees.Find(args.ManagerId, cancellationToken) is not { Status: EmploymentStatus.Active })
            return new Result.ManagerNotFound(args.ManagerId);

        var id = invocation.IdempotencyKey is { } key ? IdFrom(key) : Guid.NewGuid().ToString("N");
        var hiredOn = time.Today();
        if (await employees.Find(id, cancellationToken) is null)
            await employees.Save(
                new Employee(id, args.Name, args.ManagerId, EmploymentStatus.Active, hiredOn.AddMonths(3)), cancellationToken);

        await events.Publish(new EmployeeHired(id, args.Name, args.ManagerId, hiredOn), cancellationToken);

        return new Result.Hired(id);
    }

    private static string IdFrom(string idempotencyKey) =>
        new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey))[..16]).ToString("N");
}
