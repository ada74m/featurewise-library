using Featurewise;

namespace SampleService;

// Sends a link to the service's queue, for the QueueWorker to invoke later. Queueing a link isn't
// invoking it: the worker invokes it through Features, with its own caller and authorization.
public interface ILinkQueue
{
    Task Send(Link link, CancellationToken cancellationToken);
}
