using Featurewise;

namespace SampleService;

// Sends a manager one message listing their reports' reviews due within a week. SendReviewReminders
// queues one for each manager. A redelivery sends the message again, which does no harm.
public class RemindManagerOfReviews(IEmployeeStore employees, IMessenger messenger, TimeProvider time)
    : Feature<RemindManagerOfReviews, RemindManagerOfReviews.Args, RemindManagerOfReviews.Result>
{
    public record Args(string ManagerId) : Link<RemindManagerOfReviews, Result>;

    public record Result(int Reviews);

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        var due = (await employees.All(cancellationToken))
            .Where(e => e.ManagerId == args.ManagerId && IsDue(e, time))
            .OrderBy(e => e.ReviewDue)
            .ToList();

        // Reviews may have been done since the reminder was queued.
        if (due.Count > 0)
            await messenger.Send(
                args.ManagerId,
                "Reviews due: " + string.Join(", ", due.Select(e => $"{e.Name} on {e.ReviewDue:O}")),
                cancellationToken);

        return new Result(due.Count);
    }

    public static bool IsDue(Employee employee, TimeProvider time) =>
        employee.Status == EmploymentStatus.Active && employee.ManagerId is not null
        && employee.ReviewDue <= time.Today().AddDays(7);
}
