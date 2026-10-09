using System.Text.Json;
using Featurewise;

namespace SampleService.Tests;

// Records the JSON of each link sent, written as SqsWorkQueue writes it, in place of SQS.
public class CapturingWorkQueue(JsonSerializerOptions json, List<string> messages) : IWorkQueue
{
    public Task Send(Link link, CancellationToken cancellationToken)
    {
        messages.Add(JsonSerializer.Serialize(link, json));
        return Task.CompletedTask;
    }
}
