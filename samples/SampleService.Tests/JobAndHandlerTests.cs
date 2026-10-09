using Featurewise;

namespace SampleService.Tests;

public class JobAndHandlerTests
{
    private readonly Fakes _fakes = new Fakes().WithStaff();

    [Fact]
    public async Task SendReviewRemindersTellsManagersOfReviewsDueThisWeek()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<SendReviewReminders, AnySystemCaller<SendReviewReminders.Args>>());

        var result = await features.Invoke(new SendReviewReminders.Args(), Callers.Job);

        Assert.Equal(new SendReviewReminders.Result(1), result);
        Assert.Equal([("m1", "Ed's review is due on 2026-10-13")], _fakes.Notifier.Sent);
    }

    [Fact]
    public async Task ArchiveLeaversRemovesThoseWhoLeftMoreThanNinetyDaysAgo()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<ArchiveLeavers, AnySystemCaller<ArchiveLeavers.Args>>());

        var result = await features.Invoke(new ArchiveLeavers.Args(), Callers.Job);

        Assert.Equal(new ArchiveLeavers.Result(1), result);
        Assert.Null(_fakes.Employees["e3"]);
        Assert.NotNull(_fakes.Employees["e4"]);
    }

    [Fact]
    public async Task HandleEmployeeLeftTellsPayroll()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<HandleEmployeeLeft, AnySystemCaller<HandleEmployeeLeft.Args>>());

        Assert.Same(Nil.Value, await features.Invoke(new HandleEmployeeLeft.Args("e4"), Callers.Job));
        Assert.Equal([("payroll", "Lou (e4) has left: close their payroll")], _fakes.Notifier.Sent);
    }

    [Fact]
    public async Task ADeactivatedAccountRecordsTheDepartureOfAnActiveEmployeeOnly()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<HandleAccountDeactivated, AnySystemCaller<HandleAccountDeactivated.Args>>()
            .RegisterLinksTo<HandleEmployeeLeft>());

        var active = await features.Invoke(new HandleAccountDeactivated.Args("e1"), Callers.Job);
        var left = await features.Invoke(new HandleAccountDeactivated.Args("e4"), Callers.Job);
        var unknown = await features.Invoke(new HandleAccountDeactivated.Args("x9"), Callers.Job);

        Assert.Equal(new HandleAccountDeactivated.Result.RecordedDeparture(), active);
        Assert.Equal(new HandleAccountDeactivated.Result.AlreadyLeft(), left);
        Assert.Equal(new HandleAccountDeactivated.Result.UnknownEmployee("x9"), unknown);
        Assert.Equal(EmploymentStatus.Left, _fakes.Employees["e1"]!.Status);
        Assert.Single(_fakes.QueuedMessages);
    }

    [Fact]
    public async Task OnlyTheServicesOwnJobsAndWorkersMayInvokeThem()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<SendReviewReminders, AnySystemCaller<SendReviewReminders.Args>>());

        await Assert.ThrowsAsync<InvocationDeniedException>(() => features.Invoke(new SendReviewReminders.Args(), Callers.Manager));
        Assert.Empty(_fakes.Notifier.Sent);
    }
}
