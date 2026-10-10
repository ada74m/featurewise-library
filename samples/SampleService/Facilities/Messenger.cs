using Microsoft.Extensions.Logging;

namespace SampleService;

// Sends a message to a person, such as a manager's review reminder. Unlike an event, it's addressed to
// someone and written for them to read.
public interface IMessenger
{
    Task Send(string recipient, string message, CancellationToken cancellationToken);
}

// Stands in for email or chat: logs each message.
public class LoggingMessenger(ILogger<LoggingMessenger> logger) : IMessenger
{
    public Task Send(string recipient, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Messaging {Recipient}: {Message}", recipient, message);
        return Task.CompletedTask;
    }
}
