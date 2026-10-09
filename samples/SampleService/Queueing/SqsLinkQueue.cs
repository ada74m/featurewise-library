using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Featurewise;

namespace SampleService;

// Sends links to an SQS queue as their JSON, which names their feature.
public class SqsLinkQueue(IAmazonSQS sqs, JsonSerializerOptions json, string queueUrl) : ILinkQueue
{
    public Task Send(Link link, CancellationToken cancellationToken) =>
        sqs.SendMessageAsync(new SendMessageRequest(queueUrl, JsonSerializer.Serialize(link, json)), cancellationToken);
}
