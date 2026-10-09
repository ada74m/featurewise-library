using Featurewise;

namespace SampleService.QueueWorker;

// Turns another service's events, published to an SNS topic, into links to this service's handlers.
// The other service doesn't know this service's features, so it can't send links itself.
public interface ITopicAdapter
{
    // Names the caller of links adapted from this topic's events.
    string Source { get; }

    // The link to invoke for an event, or null if the service doesn't act on it.
    Link? Adapt(string message);
}
