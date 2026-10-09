using Featurewise;

namespace SampleService;

// Reminds managers of their reports' reviews due within a week. A schedule runs it daily, so a
// manager hears about a review each day until it's done.
public class SendReviewReminders(IEmployeeStore employees, INotifier notifier, TimeProvider time)
    : Feature<SendReviewReminders, SendReviewReminders.Args, SendReviewReminders.Result>
{
    public record Args : Link<SendReviewReminders, Result>;

    public record Result(int Reminded);

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        var horizon = Departures.Today(time).AddDays(7);
        var due = (await employees.All(cancellationToken))
            .Where(e => e.Status == EmploymentStatus.Active && e.ManagerId is not null && e.ReviewDue <= horizon)
            .ToList();

        foreach (var employee in due)
            await notifier.Notify(employee.ManagerId!, $"{employee.Name}'s review is due on {employee.ReviewDue:O}", cancellationToken);

        return new Result(due.Count);
    }
}
