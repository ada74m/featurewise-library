using Microsoft.Extensions.Logging;

namespace SampleService;

// Stands in for email or chat: logs each notification.
public class LoggingNotifier(ILogger<LoggingNotifier> logger) : INotifier
{
    public Task Notify(string recipient, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Notifying {Recipient}: {Message}", recipient, message);
        return Task.CompletedTask;
    }
}
