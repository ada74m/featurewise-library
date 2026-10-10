using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Featurewise;

namespace SampleService;

// Publishes the service's events, the records in Events/, for other services to subscribe to.
public interface IEventPublisher
{
    Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : class;
}

// Publishes each event to the service's SNS topic, in a CloudEvents envelope. Each message carries the
// event's type as the attribute "type" too, so a subscription's filter policy can pick the events it wants.
public class SnsEventPublisher(IAmazonSimpleNotificationService sns, Invocation invocation, string topicArn) : IEventPublisher
{
    public const string Source = "SampleService";

    // Events are a public contract, so they're written as public APIs write JSON: in camelCase.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : class =>
        sns.PublishAsync(Request(topicArn, @event, invocation.IdempotencyKey, invocation.Id), cancellationToken);

    // The message for an event. An invocation with an idempotency key gives the event an id derived from
    // it, so a retried request publishes the same event again, and subscribers can drop the duplicate.
    // The invocation's id goes in as the event's traceparent, linking it to the trace that caused it.
    public static PublishRequest Request<TEvent>(string topicArn, TEvent @event, string? idempotencyKey, string? invocationId)
        where TEvent : class
    {
        var type = typeof(TEvent).Name;
        var id = idempotencyKey is null
            ? Guid.NewGuid()
            : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{idempotencyKey}/{type}"))[..16]);
        var envelope = new Dictionary<string, object?>
        {
            ["specversion"] = "1.0",
            ["id"] = id.ToString(),
            ["source"] = Source,
            ["type"] = type,
            ["time"] = DateTimeOffset.UtcNow,
            ["datacontenttype"] = "application/json",
            ["data"] = @event
        };
        if (invocationId is not null)
            envelope["traceparent"] = invocationId;

        return new PublishRequest
        {
            TopicArn = topicArn,
            Message = JsonSerializer.Serialize(envelope, Json),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["type"] = new() { DataType = "String", StringValue = type }
            }
        };
    }
}
