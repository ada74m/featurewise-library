using Featurewise;

namespace SampleService.Tests;

public class EmployeeFeatureTests
{
    private readonly Fakes _fakes = new Fakes().WithStaff();

    [Fact]
    public async Task AManagerCanHireSomeoneWhoseFirstReviewIsInThreeMonths()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<Hire>().RegisterAuthorizationHandler<HireAuthorization>());

        var hired = Assert.IsType<Hire.Result.Hired>(await features.Invoke(new Hire.Args("Ann", "m1"), Callers.Manager));

        Assert.Equal(
            new Employee(hired.EmployeeId, "Ann", "m1", EmploymentStatus.Active, Fakes.Today.AddMonths(3)),
            _fakes.Employees[hired.EmployeeId]);
    }

    [Fact]
    public async Task HiringAgainWithTheSameIdempotencyKeyHiresOnce()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<Hire>().RegisterAuthorizationHandler<HireAuthorization>());
        var staff = _fakes.Employees.Count;

        var first = await features.Invoke(new Hire.Args("Ann", "m1"), Callers.Manager, "request-1");
        var retry = await features.Invoke(new Hire.Args("Ann", "m1"), Callers.Manager, "request-1");

        Assert.Equal(first, retry);
        Assert.Equal(staff + 1, _fakes.Employees.Count);
    }

    [Fact]
    public async Task HiringUnderAManagerWhoIsNotHereSaysSo()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<Hire>().RegisterAuthorizationHandler<HireAuthorization>());

        Assert.Equal(new Hire.Result.ManagerNotFound("e3"), await features.Invoke(new Hire.Args("Ann", "e3"), Callers.Manager));
    }

    [Fact]
    public async Task FiringRecordsTheDepartureAndQueuesTellingPayroll()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<Fire>().RegisterAuthorizationHandler<FireAuthorization>().RegisterLinksTo<HandleEmployeeLeft>());

        var result = await features.Invoke(new Fire.Args("e1"), Callers.Manager);

        Assert.Equal(new Fire.Result.Fired(), result);
        Assert.Equal(Staff.Ed with { Status = EmploymentStatus.Left, LeftOn = Fakes.Today }, _fakes.Employees["e1"]);
        Assert.Contains("\"$feature\":\"HandleEmployeeLeft\",\"EmployeeId\":\"e1\"", Assert.Single(_fakes.QueuedMessages));
    }

    [Fact]
    public async Task FiringSomeoneWhoHasLeftSaysSoAndQueuesNothing()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<Fire>().RegisterAuthorizationHandler<FireAuthorization>().RegisterLinksTo<HandleEmployeeLeft>());

        Assert.Equal(new Fire.Result.AlreadyLeft(), await features.Invoke(new Fire.Args("e4"), Callers.Manager));
        Assert.Empty(_fakes.QueuedMessages);
    }

    [Fact]
    public async Task EmployeeDetailsOfferAManagerALinkToFireButNotAnEmployee()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<EmployeeDetails>().RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<AnySignedInCaller<EmployeeDetails.Args>>()
            .RegisterAuthorizationHandler<FireAuthorization>()
            .RegisterLinksTo<HandleEmployeeLeft>());

        var managers = await features.Invoke(new EmployeeDetails.Args("e2"), Callers.Manager);
        var employees = await features.Invoke(new EmployeeDetails.Args("e2"), Callers.Employee);

        Assert.Equal(new EmployeeDetails.Result.Found(Staff.Eve, new Fire.Args("e2")), managers);
        Assert.Equal(new EmployeeDetails.Result.Found(Staff.Eve, null), employees);
    }

    [Fact]
    public async Task ReviewsDueListsTheCallersActiveReportsSoonestFirst()
    {
        await using var features = ServiceFeatures.Over(_fakes, f => f
            .RegisterFeature<ReviewsDue>().RegisterAuthorizationHandler<AnySignedInCaller<ReviewsDue.Args>>());

        var withinAMonth = await features.Invoke(new ReviewsDue.Args(), Callers.Manager);
        var withinAQuarter = await features.Invoke(new ReviewsDue.Args(90), Callers.Manager);
        var anEmployees = await features.Invoke(new ReviewsDue.Args(90), Callers.Employee);

        Assert.Equal([Staff.Ed], withinAMonth.Employees);
        Assert.Equal([Staff.Ed, Staff.Eve], withinAQuarter.Employees);
        Assert.Empty(anEmployees.Employees);
    }
}
