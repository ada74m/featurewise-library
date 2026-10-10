namespace SampleService.Tests;

// Records each message in place of sending it.
public class CapturingMessenger : IMessenger
{
    public List<(string Recipient, string Message)> Sent { get; } = [];

    public Task Send(string recipient, string message, CancellationToken cancellationToken)
    {
        Sent.Add((recipient, message));
        return Task.CompletedTask;
    }
}
