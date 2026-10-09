using System.Text.Json;
using Featurewise;

namespace SampleService.Tests;

// Records the JSON of each link sent, written as SqsLinkQueue writes it, in place of SQS.
public class CapturingLinkQueue(JsonSerializerOptions json, List<string> messages) : ILinkQueue
{
    public Task Send(Link link, CancellationToken cancellationToken)
    {
        messages.Add(JsonSerializer.Serialize(link, json));
        return Task.CompletedTask;
    }
}
