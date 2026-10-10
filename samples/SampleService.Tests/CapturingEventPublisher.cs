namespace SampleService.Tests;

// Records each event published, in place of SNS.
public class CapturingEventPublisher : IEventPublisher
{
    public List<object> Published { get; } = [];

    public Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : class
    {
        Published.Add(@event);
        return Task.CompletedTask;
    }
}
