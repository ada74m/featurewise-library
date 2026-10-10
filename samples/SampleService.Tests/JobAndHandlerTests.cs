using Featurewise;

namespace SampleService.Tests;

public class JobAndHandlerTests
{
    private readonly Fakes _fakes = new Fakes().WithStaff();

    [Fact]
    public async Task SendReviewRemindersQueuesAReminderForEachManagerWithReviewsDueThisWeek()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<SendReviewReminders, AnySystemCaller<SendReviewReminders.Args>>()
            .RegisterLinksTo<RemindManagerOfReviews>());

        var result = await features.Invoke(new SendReviewReminders.Args(), Callers.Job);

        Assert.Equal(new SendReviewReminders.Result(1), result);
        Assert.Contains("\"$feature\":\"hr/RemindManagerOfReviews\",\"ManagerId\":\"m1\"", Assert.Single(_fakes.QueuedMessages));
    }

    [Fact]
    public async Task AReminderListsTheManagersReviewsDueThisWeekInOneMessage()
    {
        _fakes.Employees.Add(Staff.Eve with { ReviewDue = Fakes.Today.AddDays(2) });
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<RemindManagerOfReviews, AnySystemCaller<RemindManagerOfReviews.Args>>());

        var result = await features.Invoke(new RemindManagerOfReviews.Args("m1"), Callers.Job);

        Assert.Equal(new RemindManagerOfReviews.Result(2), result);
        Assert.Equal([("m1", "Reviews due: Eve on 2026-10-10, Ed on 2026-10-13")], _fakes.Messenger.Sent);
    }

    [Fact]
    public async Task AReminderForAManagerWithNothingDueSendsNothing()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<RemindManagerOfReviews, AnySystemCaller<RemindManagerOfReviews.Args>>());

        Assert.Equal(new RemindManagerOfReviews.Result(0), await features.Invoke(new RemindManagerOfReviews.Args("e1"), Callers.Job));
        Assert.Empty(_fakes.Messenger.Sent);
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
    public async Task ADeactivatedAccountRecordsTheDepartureOfAnActiveEmployeeOnly()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<HandleAccountDeactivated, AnySystemCaller<HandleAccountDeactivated.Args>>());

        var active = await features.Invoke(new HandleAccountDeactivated.Args("e1"), Callers.Job);
        var left = await features.Invoke(new HandleAccountDeactivated.Args("e4"), Callers.Job);
        var unknown = await features.Invoke(new HandleAccountDeactivated.Args("x9"), Callers.Job);

        Assert.Equal(new HandleAccountDeactivated.Result.RecordedDeparture(), active);
        Assert.Equal(new HandleAccountDeactivated.Result.AlreadyLeft(), left);
        Assert.Equal(new HandleAccountDeactivated.Result.UnknownEmployee("x9"), unknown);
        Assert.Equal(EmploymentStatus.Left, _fakes.Employees["e1"]!.Status);
        Assert.Equal([new EmployeeLeft("e1", Fakes.Today)], _fakes.Events.Published);
    }

    [Fact]
    public async Task OnlyTheServicesOwnJobsAndWorkersMayInvokeThem()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<SendReviewReminders, AnySystemCaller<SendReviewReminders.Args>>()
            .RegisterLinksTo<RemindManagerOfReviews>());

        await Assert.ThrowsAsync<InvocationDeniedException>(() => features.Invoke(new SendReviewReminders.Args(), Callers.Manager));
        Assert.Empty(_fakes.QueuedMessages);
    }
}
