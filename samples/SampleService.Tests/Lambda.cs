using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;

namespace SampleService.Tests;

// Lambda contexts and events, shaped as AWS delivers them.
public static class Lambda
{
    public static TestLambdaContext Context() => new() { RemainingTime = TimeSpan.FromSeconds(30) };

    // JWT claims as API Gateway's authorizer passes them: Mia the manager, and Ed, who isn't one.
    public static readonly Dictionary<string, string> Manager = new() { ["sub"] = "m1", ["cognito:groups"] = "[manager]" };

    public static readonly Dictionary<string, string> Employee = new() { ["sub"] = "e1" };

    // A request to the UI backend's one route, POST /{feature}.
    public static APIGatewayHttpApiV2ProxyRequest Post(
        string feature, Dictionary<string, string>? claims, string? body, string? idempotencyKey = null) =>
        new()
        {
            RawPath = $"/{feature}",
            PathParameters = new Dictionary<string, string> { ["feature"] = feature },
            Headers = idempotencyKey is null ? null : new Dictionary<string, string> { ["idempotency-key"] = idempotencyKey },
            Body = body,
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "POST" },
                Authorizer = claims is null
                    ? null
                    : new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                    {
                        Jwt = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription.JwtDescription { Claims = claims }
                    }
            }
        };

    public static SQSEvent Messages(params (string Id, string Body)[] messages) =>
        new() { Records = messages.Select(m => new SQSEvent.SQSMessage { MessageId = m.Id, Body = m.Body }).ToList() };

    // How SNS wraps a message it delivers to an SQS queue.
    public static string SnsEnvelope(string topicArn, string messageId, string message) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            Type = "Notification",
            MessageId = messageId,
            TopicArn = topicArn,
            Message = message,
            Timestamp = "2026-10-08T12:00:00.000Z"
        });
}
