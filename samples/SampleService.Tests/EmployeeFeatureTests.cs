using Featurewise;

namespace SampleService.Tests;

public class EmployeeFeatureTests
{
    private readonly Fakes _fakes = new Fakes().WithStaff();

    [Fact]
    public async Task AManagerCanHireSomeoneWhoseFirstReviewIsInThreeMonths()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<Hire, Hire.Rule>());

        var hired = Assert.IsType<Hire.Result.Hired>(await features.Invoke(new Hire.Args("Ann", "m1"), Callers.Manager));

        Assert.Equal(
            new Employee(hired.EmployeeId, "Ann", "m1", EmploymentStatus.Active, Fakes.Today.AddMonths(3)),
            _fakes.Employees[hired.EmployeeId]);
        Assert.Equal([new EmployeeHired(hired.EmployeeId, "Ann", "m1", Fakes.Today)], _fakes.Events.Published);
    }

    [Fact]
    public async Task HiringAgainWithTheSameIdempotencyKeyHiresOnceAndPublishesTheSameEventAgain()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<Hire, Hire.Rule>());
        var staff = _fakes.Employees.Count;

        var first = await features.Invoke(new Hire.Args("Ann", "m1"), Callers.Manager, "request-1");
        var retry = await features.Invoke(new Hire.Args("Ann", "m1"), Callers.Manager, "request-1");

        Assert.Equal(first, retry);
        Assert.Equal(staff + 1, _fakes.Employees.Count);
        Assert.Equal(2, _fakes.Events.Published.Count);
        Assert.Equal(_fakes.Events.Published[0], _fakes.Events.Published[1]);
    }

    [Fact]
    public async Task HiringUnderAManagerWhoIsNotHereSaysSo()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<Hire, Hire.Rule>());

        Assert.Equal(new Hire.Result.ManagerNotFound("e3"), await features.Invoke(new Hire.Args("Ann", "e3"), Callers.Manager));
        Assert.Empty(_fakes.Events.Published);
    }

    [Fact]
    public async Task FiringRecordsTheDepartureAndPublishesThatTheyLeft()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<Fire, Fire.Rule>());

        var result = await features.Invoke(new Fire.Args("e1"), Callers.Manager);

        Assert.Equal(new Fire.Result.Fired(), result);
        Assert.Equal(Staff.Ed with { Status = EmploymentStatus.Left, LeftOn = Fakes.Today }, _fakes.Employees["e1"]);
        Assert.Equal([new EmployeeLeft("e1", Fakes.Today)], _fakes.Events.Published);
    }

    [Fact]
    public async Task FiringSomeoneWhoHasLeftSaysSoAndPublishesNothing()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<Fire, Fire.Rule>());

        Assert.Equal(new Fire.Result.AlreadyLeft(), await features.Invoke(new Fire.Args("e4"), Callers.Manager));
        Assert.Empty(_fakes.Events.Published);
    }

    [Fact]
    public async Task EmployeeDetailsOfferAManagerALinkToFireButNotAnEmployee()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<EmployeeDetails, AnySignedInCaller<EmployeeDetails.Args>>()
            .RegisterFeature<Fire, Fire.Rule>());

        var managers = await features.Invoke(new EmployeeDetails.Args("e2"), Callers.Manager);
        var employees = await features.Invoke(new EmployeeDetails.Args("e2"), Callers.Employee);

        Assert.Equal(new EmployeeDetails.Result.Found(Staff.Eve, new Fire.Args("e2")), managers);
        Assert.Equal(new EmployeeDetails.Result.Found(Staff.Eve, null), employees);
    }

    [Fact]
    public async Task ReviewsDueListsTheCallersActiveReportsSoonestFirst()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<ReviewsDue, AnySignedInCaller<ReviewsDue.Args>>());

        var withinAMonth = await features.Invoke(new ReviewsDue.Args(), Callers.Manager);
        var withinAQuarter = await features.Invoke(new ReviewsDue.Args(90), Callers.Manager);
        var anEmployees = await features.Invoke(new ReviewsDue.Args(90), Callers.Employee);

        Assert.Equal([Staff.Ed], withinAMonth.Employees);
        Assert.Equal([Staff.Ed, Staff.Eve], withinAQuarter.Employees);
        Assert.Empty(anEmployees.Employees);
    }
}
