using System.Text;
using System.Text.Json;
using SampleService.ScheduledWorker;

namespace SampleService.Tests;

public class ScheduledWorkerTests
{
    private const string ScheduleArn = "arn:aws:scheduler:eu-west-2:123456789012:schedule/default/review-reminders";

    private readonly Fakes _fakes = new Fakes().WithStaff();
    private readonly Function _worker;

    public ScheduledWorkerTests() => _worker = new Function(_fakes.Register);

    [Fact]
    public async Task AScheduleRunsTheJobItsInputNames()
    {
        await Handle($$$"""{"Schedule":"{{{ScheduleArn}}}","Execution":"x1","Link":{"$feature":"SendReviewReminders"}}""");

        Assert.Contains("\"$feature\":\"RemindManagerOfReviews\",\"ManagerId\":\"m1\"", Assert.Single(_fakes.QueuedMessages));
    }

    [Fact]
    public async Task AJobsArgumentsComeFromTheInput()
    {
        await Handle($$$"""{"Schedule":"{{{ScheduleArn}}}","Link":{"$feature":"ArchiveLeavers","AfterDays":5}}""");

        Assert.Null(_fakes.Employees["e3"]);
        Assert.Null(_fakes.Employees["e4"]);
    }

    [Theory]
    [InlineData("""{"Schedule":"s","Link":{"$feature":"Fire","EmployeeId":"e1"}}""")]
    [InlineData("""{"Schedule":"s"}""")]
    [InlineData("""{"Link":{"$feature":"SendReviewReminders"}}""")]
    public async Task AnInputThatDoesntNameOneOfTheWorkersJobsFails(string input)
    {
        await Assert.ThrowsAsync<JsonException>(() => Handle(input));
        Assert.Empty(_fakes.QueuedMessages);
    }

    private Task Handle(string input) =>
        _worker.Handle(new MemoryStream(Encoding.UTF8.GetBytes(input)), Lambda.Context());
}
