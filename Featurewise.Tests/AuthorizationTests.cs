using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Featurewise.Tests;

public class AuthorizationTests
{
    [Fact]
    public async Task ACallerNoRuleApprovesIsDeniedAndTheFeatureIsNotInvoked()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features("hr");
        features
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        var denied = await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Employee));

        Assert.Equal("Fire", denied.Feature);
        Assert.True(denied.CallerAuthenticated);
        Assert.DoesNotContain(logs.Entries, e => e.Message == "Firing e1");
        var warning = Assert.Single(logs.Entries, e => e.Level == LogLevel.Warning);
        Assert.Equal("Denied employee invoking Fire", warning.Message);
    }

    [Fact]
    public async Task AnAnonymousCallerIsDeniedAsUnauthenticated()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<Fire, FireAuthorization>();

        var denied = await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Anonymous));

        Assert.False(denied.CallerAuthenticated);
    }

    [Fact]
    public async Task RulesCanDependOnTheLinksArguments()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<Fire, FireAuthorization>();

        await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("manager"), Callers.Manager));
    }

    [Fact]
    public async Task AFeatureWithSeveralRulesAdmitsAnyoneOneOfThemApproves()
    {
        await using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization, AnySystemCaller<Fire.Args>>();

        var byManager = await features.Invoke(new Fire.Args("e1"), Callers.Manager);
        var byJob = await features.Invoke(new Fire.Args("e2"), SystemCaller.For("nightly-reminders"));
        await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e3"), Callers.Employee));

        Assert.IsType<Fire.Result.Fired>(byManager);
        Assert.IsType<Fire.Result.Fired>(byJob);
    }

    [Fact]
    public async Task AnyCallerAdmitsAnonymousCallers()
    {
        await using var features = new Features("hr");
        features.RegisterFeature<WhoAmI, AnyCaller<WhoAmI.Args>>();

        Assert.Null(await features.Invoke(new WhoAmI.Args(), Callers.Anonymous));
    }

    [Fact]
    public async Task AFeatureCanTakeItsCallerAsADependency()
    {
        await using var features = new Features("hr");
        features.RegisterFeature<WhoAmI, AnyCaller<WhoAmI.Args>>();

        Assert.Equal("manager", await features.Invoke(new WhoAmI.Args(), Callers.Manager));
    }

    [Fact]
    public async Task AFeatureOffersALinkOnlyIfItsCallerMayInvokeIt()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>()
            .RegisterFeature<Fire, FireAuthorization>();

        var managersReview = await features.Invoke(new EmployeeReview.Args("e1"), Callers.Manager);
        var employeesReview = await features.Invoke(new EmployeeReview.Args("e1"), Callers.Employee);

        Assert.Equal(new Fire.Args("e1"), managersReview.LinkToFire);
        Assert.Null(employeesReview.LinkToFire);
    }

    [Fact]
    public async Task AnOfferedLinkIsCheckedAgainWhenInvoked()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>()
            .RegisterFeature<Fire, FireAuthorization>();

        var offered = (await features.Invoke(new EmployeeReview.Args("e1"), Callers.Manager)).LinkToFire!;

        await Assert.ThrowsAsync<InvocationDeniedException>(() => features.Invoke(offered, Callers.Employee));
    }

    [Fact]
    public async Task AnySignedInCallerAdmitsSignedInCallersOnly()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<Fire, AnySignedInCaller<Fire.Args>>();

        var result = await features.Invoke(new Fire.Args("e1"), Callers.Employee);
        var denied = await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Anonymous));

        Assert.IsType<Fire.Result.Fired>(result);
        Assert.False(denied.CallerAuthenticated);
    }

    [Fact]
    public async Task AnySignedInCallerAppliesOnlyToItsOwnLinkType()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<EmployeeReview, AnySignedInCaller<EmployeeReview.Args>>()
            .RegisterFeature<Fire, FireAuthorization>();

        await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Employee));
    }

    [Fact]
    public void ASystemCallerIsSignedInAndNamedAfterItsJob()
    {
        var caller = SystemCaller.For("nightly-reminders");

        Assert.True(caller.Identity?.IsAuthenticated);
        Assert.Equal("nightly-reminders", caller.Identity?.Name);
        Assert.True(SystemCaller.IsSystem(caller));
        Assert.False(SystemCaller.IsSystem(Callers.Manager));
        Assert.False(SystemCaller.IsSystem(Callers.Anonymous));
    }

    [Fact]
    public async Task ASystemCallerCanInvokeFeaturesOpenToSignedInCallers()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<Fire, AnySignedInCaller<Fire.Args>>();

        var result = await features.Invoke(new Fire.Args("e1"), SystemCaller.For("nightly-reminders"));

        Assert.IsType<Fire.Result.Fired>(result);
    }

    [Fact]
    public async Task ASystemCallerGetsNoSpecialTreatment()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<Fire, FireAuthorization>();

        await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), SystemCaller.For("nightly-reminders")));
    }

    [Fact]
    public async Task AnySystemCallerAdmitsSystemCallersOnly()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<Fire, AnySystemCaller<Fire.Args>>();

        var result = await features.Invoke(new Fire.Args("e1"), SystemCaller.For("nightly-reminders"));
        var signedIn = await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Manager));
        var anonymous = await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Anonymous));

        Assert.IsType<Fire.Result.Fired>(result);
        Assert.True(signedIn.CallerAuthenticated);
        Assert.False(anonymous.CallerAuthenticated);
    }

    [Fact]
    public async Task AnySystemCallerAppliesOnlyToItsOwnLinkType()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<Remind, AnySystemCaller<Remind.Args>>()
            .RegisterDependencies(s => s.AddSingleton<SentReminders>())
            .RegisterFeature<Fire, FireAuthorization>();

        await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), SystemCaller.For("nightly-reminders")));
    }

    [Fact]
    public async Task ARuleThatThrowsFailsTheInvocationAndIsLoggedAsAFailure()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features("hr");
        features
            .RegisterFeature<Fire, BrokenFireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Manager));

        var failure = Assert.Single(logs.Entries, e => e.Level == LogLevel.Error);
        Assert.Matches(@"^Invoking Fire failed after \d+ ms$", failure.Message);
        Assert.Same(thrown, failure.Exception);
    }

    [Fact]
    public async Task ARuleCanBeCancelledWithTheInvocation()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features("hr");
        features
            .RegisterFeature<Fire, SlowFireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Manager, cancellation.Token));

        Assert.Contains(logs.Entries, e => e.Message.StartsWith("Invoking Fire was cancelled after "));
        Assert.DoesNotContain(logs.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ARefusalIsLoggedAsARefusalNotAFailure()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features("hr");
        features
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        await Assert.ThrowsAsync<InvocationDeniedException>(() => features.Invoke(new Fire.Args("e1"), Callers.Employee));

        Assert.DoesNotContain(logs.Entries, e => e.Level == LogLevel.Error);
    }
}
