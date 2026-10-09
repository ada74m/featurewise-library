using Featurewise;

namespace SampleService;

// The caller's reports whose reviews fall due within the given number of days, soonest first.
public class ReviewsDue(IEmployeeStore employees, Invocation invocation, TimeProvider time)
    : Feature<ReviewsDue, ReviewsDue.Args, ReviewsDue.Result>
{
    public record Args(int WithinDays = 30) : Link<ReviewsDue, Result>;

    public record Result(IReadOnlyList<Employee> Employees);

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        var horizon = Departures.Today(time).AddDays(args.WithinDays);
        var manager = invocation.Caller.Identity?.Name;
        var due = (await employees.All(cancellationToken))
            .Where(e => e.Status == EmploymentStatus.Active && e.ManagerId == manager && e.ReviewDue <= horizon)
            .OrderBy(e => e.ReviewDue)
            .ToList();
        return new Result(due);
    }
}
