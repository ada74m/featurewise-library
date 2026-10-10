using Featurewise;

namespace SampleService;

// Queues a reminder for each manager with reports' reviews due within a week, for the queue worker to
// send. A schedule runs it daily, so a manager hears each day until the reviews are done, and one
// manager's failed reminder is retried alone.
public class SendReviewReminders(IEmployeeStore employees, IWorkQueue queue, TimeProvider time)
    : Feature<SendReviewReminders, SendReviewReminders.Args, SendReviewReminders.Result>
{
    public record Args : Link<SendReviewReminders, Result>;

    public record Result(int Managers);

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        var managers = (await employees.All(cancellationToken))
            .Where(e => RemindManagerOfReviews.IsDue(e, time))
            .Select(e => e.ManagerId!)
            .Distinct()
            .ToList();

        foreach (var manager in managers)
            await queue.Send(new RemindManagerOfReviews.Args(manager), cancellationToken);

        return new Result(managers.Count);
    }
}
