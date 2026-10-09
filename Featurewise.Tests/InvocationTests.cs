using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Featurewise.Tests;

public class InvocationTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task AFeatureCanBeInvokedDirectlyToTestItAlone()
    {
        var args = new Fire.Args("e1");
        var feature = new Fire(NullLogger<Fire>.Instance);
        var result = await feature.Invoke(args, CancellationToken.None);

        testOutputHelper.WriteLine(result.ToString());
    }

    [Fact]
    public async Task EachInvocationRunsInItsOwnScopeWhichIsDisposedAfterwards()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Probe, AnyCaller<Probe.Args>>()
            .RegisterDependencies(services => services.AddScoped<ScopeProbe>());

        var first = await features.Invoke(new Probe.Args(), Callers.Manager);
        var second = await features.Invoke(new Probe.Args(), Callers.Manager);

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
    }

    [Fact]
    public async Task AFeatureThatInvokesALinkThrows()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterFeature<Escalate, AnyCaller<Escalate.Args>>()
            .RegisterDependencies(s => s.AddSingleton(features));

        await Assert.ThrowsAsync<InvalidOperationException>(() => features.Invoke(new Escalate.Args("e1"), Callers.Manager));
    }

    [Fact]
    public async Task InvocationsInSequenceAreNotNested()
    {
        await using var features = new Features();
        features.RegisterFeature<Fire, FireAuthorization>();

        await features.Invoke(new Fire.Args("e1"), Callers.Manager);
        var second = await features.Invoke(new Fire.Args("e2"), Callers.Manager);

        Assert.IsType<Fire.Result.Fired>(second);
    }

    [Fact]
    public async Task InvokingWithACancelledTokenDoesNotInvokeTheFeature()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features();
        features
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Manager, new CancellationToken(canceled: true)));

        Assert.Empty(logs.Entries);
    }

    [Fact]
    public async Task CancellingDuringAnInvocationStopsTheFeatureAndIsNotLoggedAsAFailure()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features();
        features
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Manager, cancellation.Token));

        var last = logs.Entries.Last();
        Assert.Equal(LogLevel.Information, last.Level);
        Assert.Matches(@"^Invoking Fire was cancelled after \d+ ms$", last.Message);
        Assert.DoesNotContain(logs.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task AFeatureCancellingItselfIsLoggedAsAFailure()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features();
        features
            .RegisterFeature<GiveUp, AnyCaller<GiveUp.Args>>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        await Assert.ThrowsAsync<OperationCanceledException>(() => features.Invoke(new GiveUp.Args(), Callers.Manager));

        var last = logs.Entries.Last();
        Assert.Equal(LogLevel.Error, last.Level);
        Assert.Matches(@"^Invoking GiveUp failed after \d+ ms$", last.Message);
    }

    [Fact]
    public async Task ALinkCanBeInvokedWithoutKnowingItsFeatureOrResult()
    {
        await using var features = new Features();
        features.RegisterFeature<Fire, FireAuthorization>();
        Link link = new Fire.Args("e1");

        var result = await features.Invoke(link, Callers.Manager);

        Assert.Equal(new Fire.Result.Fired(), result);
    }

    [Fact]
    public async Task AnExpectedFailureIsAnOutcomeNotAnError()
    {
        var logs = new CapturingLoggerProvider();
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Features.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stopped.Add
        };
        ActivitySource.AddActivityListener(listener);
        await using var features = new Features();
        features
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        var result = await features.Invoke(new Fire.Args(Fire.Nobody), Callers.Manager);

        Assert.Equal(new Fire.Result.NotFound(Fire.Nobody), result);
        var finished = Assert.Single(logs.Entries, e => e.Message.StartsWith("Invoked Fire"));
        Assert.Equal(LogLevel.Information, finished.Level);
        Assert.Matches(@"^Invoked Fire in \d+ ms: NotFound$", finished.Message);
        Assert.DoesNotContain(logs.Entries, e => e.Level >= LogLevel.Warning);
        var activity = Assert.Single(stopped, a => a.OperationName == nameof(Fire));
        Assert.Equal("NotFound", activity.GetTagItem(Features.OutcomeTag));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }

    [Fact]
    public async Task AFeatureWithNothingToReturnReturnsNil()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features();
        features
            .RegisterFeature<HandleEmployeeLeft, AnyCaller<HandleEmployeeLeft.Args>>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        var result = await features.Invoke(new HandleEmployeeLeft.Args("e1"), SystemCaller.For("employee-events"));

        Assert.Same(Nil.Value, result);
        var finished = Assert.Single(logs.Entries, e => e.Message.StartsWith("Invoked HandleEmployeeLeft"));
        Assert.Matches(@"^Invoked HandleEmployeeLeft in \d+ ms: Nil$", finished.Message);
    }

    [Fact]
    public async Task AFeatureCanFindOutAboutItsInvocation()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Features.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stopped.Add
        };
        ActivitySource.AddActivityListener(listener);
        await using var features = new Features();
        features.RegisterFeature<DescribeInvocation, AnyCaller<DescribeInvocation.Args>>();
        const string cause = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        using var cancellation = new CancellationTokenSource();

        var described = await features.Invoke(
            new DescribeInvocation.Args { CausedBy = cause }, Callers.Manager, "message-1", cancellation.Token);

        var activity = Assert.Single(stopped, a => a.OperationName == nameof(DescribeInvocation));
        Assert.Equal(new DescribeInvocation.Result("manager", "message-1", activity.Id, cause, true), described);
    }
}
