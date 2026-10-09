using System.Text.Json;
using Featurewise;

namespace SampleService.QueueWorker;

// The envelope SNS wraps a message in when it delivers to an SQS queue without raw message delivery.
public record SnsNotification(string Type, string MessageId, string TopicArn, string Message)
{
    // The notification a message body holds, or null if it holds something else, such as a link.
    public static SnsNotification? TryRead(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var isNotification = root.ValueKind == JsonValueKind.Object
            && !root.TryGetProperty(Features.FeatureNameProperty, out _)
            && root.TryGetProperty("Type", out var type) && type.ValueEquals("Notification");
        return isNotification ? root.Deserialize<SnsNotification>() : null;
    }
}
