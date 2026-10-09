namespace SampleService;

// Tells someone something: a manager that a review is due, payroll that someone has left.
public interface INotifier
{
    Task Notify(string recipient, string message, CancellationToken cancellationToken);
}
