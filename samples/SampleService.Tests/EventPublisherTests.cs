using System.Text.Json.Nodes;

namespace SampleService.Tests;

public class EventPublisherTests
{
    private const string Topic = "arn:aws:sns:eu-west-2:123456789012:hr-events";
    private const string InvocationId = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    [Fact]
    public void AnEventIsPublishedAsACloudEventWithItsTypeAsAMessageAttribute()
    {
        var request = SnsEventPublisher.Request(Topic, new EmployeeLeft("e1", new DateOnly(2026, 10, 8)), null, InvocationId);

        var message = JsonNode.Parse(request.Message)!;
        Assert.Equal(Topic, request.TopicArn);
        Assert.Equal("1.0", (string?)message["specversion"]);
        Assert.Equal("SampleService", (string?)message["source"]);
        Assert.Equal("EmployeeLeft", (string?)message["type"]);
        Assert.Equal("application/json", (string?)message["datacontenttype"]);
        Assert.Equal(InvocationId, (string?)message["traceparent"]);
        Assert.Equal("""{"employeeId":"e1","leftOn":"2026-10-08"}""", message["data"]!.ToJsonString());
        Assert.Equal("EmployeeLeft", request.MessageAttributes["type"].StringValue);
    }

    [Fact]
    public void AnInvocationsIdempotencyKeyGivesEachOfItsEventsTheSameIdEveryTime()
    {
        var left = new EmployeeLeft("e1", new DateOnly(2026, 10, 8));
        var hired = new EmployeeHired("e1", "Ed", "m1", new DateOnly(2026, 10, 8));

        Assert.Equal(IdOf(left, "request-1"), IdOf(left, "request-1"));
        Assert.NotEqual(IdOf(left, "request-1"), IdOf(hired, "request-1"));
        Assert.NotEqual(IdOf(left, null), IdOf(left, null));
    }

    private static string IdOf<TEvent>(TEvent @event, string? idempotencyKey) where TEvent : class =>
        (string)JsonNode.Parse(SnsEventPublisher.Request(Topic, @event, idempotencyKey, null).Message)!["id"]!;
}
