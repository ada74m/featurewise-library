namespace SampleService.Tests;

// Records each notification in place of sending it.
public class CapturingNotifier : INotifier
{
    public List<(string Recipient, string Message)> Sent { get; } = [];

    public Task Notify(string recipient, string message, CancellationToken cancellationToken)
    {
        Sent.Add((recipient, message));
        return Task.CompletedTask;
    }
}
