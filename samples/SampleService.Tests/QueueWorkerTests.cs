using System.Text;
using SampleService.QueueWorker;

namespace SampleService.Tests;

public class QueueWorkerTests
{
    private const string DirectoryTopic = "arn:aws:sns:eu-west-2:123456789012:directory-events";

    private readonly Fakes _fakes = new Fakes().WithStaff();
    private readonly Function _worker;

    public QueueWorkerTests() => _worker = new Function(_fakes.Register, $"{DirectoryTopic}=directory");

    [Fact]
    public async Task ALinkTheServiceQueuedIsInvoked()
    {
        var response = await _worker.Handle(
            Lambda.Messages(("m-1", """{"$feature":"RemindManagerOfReviews","ManagerId":"m1"}""")), Lambda.Context());

        Assert.Empty(response.BatchItemFailures);
        Assert.Equal([("m1", "Reviews due: Ed on 2026-10-13")], _fakes.Messenger.Sent);
    }

    [Fact]
    public async Task AnEventFromASubscribedTopicIsAdaptedAndInvoked()
    {
        var envelope = Lambda.SnsEnvelope(DirectoryTopic, "sns-1", """{"type":"AccountDeactivated","accountId":"e1"}""");

        var response = await _worker.Handle(Lambda.Messages(("m-1", envelope)), Lambda.Context());

        Assert.Empty(response.BatchItemFailures);
        Assert.Equal(EmploymentStatus.Left, _fakes.Employees["e1"]!.Status);
        Assert.Equal([new EmployeeLeft("e1", Fakes.Today)], _fakes.Events.Published);
    }

    [Fact]
    public async Task AnEventTheServiceDoesntActOnIsDoneWith()
    {
        var envelope = Lambda.SnsEnvelope(DirectoryTopic, "sns-1", """{"type":"AccountCreated","accountId":"e9"}""");

        var response = await _worker.Handle(Lambda.Messages(("m-1", envelope)), Lambda.Context());

        Assert.Empty(response.BatchItemFailures);
        Assert.Empty(_fakes.Events.Published);
    }

    [Fact]
    public async Task OnlyTheMessagesThatFailAreReported()
    {
        var response = await _worker.Handle(Lambda.Messages(
            ("good", """{"$feature":"RemindManagerOfReviews","ManagerId":"m1"}"""),
            ("unreadable", "not json"),
            ("unknown-topic", Lambda.SnsEnvelope("arn:aws:sns:eu-west-2:123456789012:other", "sns-2", "{}")),
            ("not-here", """{"$feature":"Fire","EmployeeId":"e1"}"""),
            ("bad-event", Lambda.SnsEnvelope(DirectoryTopic, "sns-3", """{"type":"AccountDeactivated"}"""))),
            Lambda.Context());

        Assert.Equal(
            ["unreadable", "unknown-topic", "not-here", "bad-event"],
            response.BatchItemFailures.Select(f => f.ItemIdentifier));
        Assert.Single(_fakes.Messenger.Sent);
    }

    [Fact]
    public async Task RemindersTheScheduledWorkerQueuesAreSentByTheQueueWorker()
    {
        var scheduler = new ScheduledWorker.Function(_fakes.Register);
        var input = """{"Schedule":"arn:aws:scheduler:eu-west-2:123456789012:schedule/default/review-reminders","Link":{"$feature":"SendReviewReminders"}}""";
        await scheduler.Handle(new MemoryStream(Encoding.UTF8.GetBytes(input)), Lambda.Context());

        var response = await _worker.Handle(Lambda.Messages(("m-1", Assert.Single(_fakes.QueuedMessages))), Lambda.Context());

        Assert.Empty(response.BatchItemFailures);
        Assert.Equal([("m1", "Reviews due: Ed on 2026-10-13")], _fakes.Messenger.Sent);
    }
}
