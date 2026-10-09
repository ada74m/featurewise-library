using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Featurewise;

namespace SampleService;

// Sends a link to the service's queue, for the QueueWorker to invoke later. Queueing a link isn't
// invoking it: the worker invokes it through Features, with its own caller and authorization.
public interface IWorkQueue
{
    Task Send(Link link, CancellationToken cancellationToken);
}

// Sends links to an SQS queue as their JSON, which names their feature.
public class SqsWorkQueue(IAmazonSQS sqs, JsonSerializerOptions json, string queueUrl) : IWorkQueue
{
    public Task Send(Link link, CancellationToken cancellationToken) =>
        sqs.SendMessageAsync(new SendMessageRequest(queueUrl, JsonSerializer.Serialize(link, json)), cancellationToken);
}
