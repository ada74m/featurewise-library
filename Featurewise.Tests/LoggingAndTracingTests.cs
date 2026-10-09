using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Featurewise.Tests;

public class LoggingAndTracingTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task PrintTheLogsOfReviewingThenFiring()
    {
        var logs = new CapturingLoggerProvider();
        
        await using var features = new Features();
        features.RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>();
        features.RegisterFeature<Fire, FireAuthorization>();
        features.RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));
        
        var link = new EmployeeReview.Args("e1");
        var result = await features.Invoke(link, Callers.Manager);

        var fire = result.LinkToFire!;
        var fireResult = await features.Invoke(fire, Callers.Manager);
        
        testOutputHelper.WriteLine(result.Comments);
        
        testOutputHelper.WriteLine(string.Join(Environment.NewLine, logs.Entries));
    }

    [Fact]
    public async Task FeatureLogsCarryTheInvokedFeature()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features();
        features
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        await features.Invoke(new Fire.Args("e1"), Callers.Manager);

        var entry = Assert.Single(logs.Entries, e => e.Message == "Firing e1");
        Assert.Contains("Feature Fire", entry.Scopes);
    }

    [Fact]
    public async Task InvokingLogsStartAndFinishUnderTheFeaturesCategory()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features();
        features
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        await features.Invoke(new Fire.Args("e1"), Callers.Manager);

        // Other categories, such as the authorization service's, log during the invocation too.
        var fireEntries = logs.Entries.Where(e => e.Category == typeof(Fire).FullName).ToList();
        Assert.Collection(fireEntries,
            e => Assert.Matches(@"^Invoking Fire as 00-\S+ caused by \(null\)$", e.Message),
            e => Assert.Equal("Firing e1", e.Message),
            e => Assert.Matches(@"^Invoked Fire in \d+ ms: Fired$", e.Message));
        Assert.All(fireEntries, e => Assert.Contains("Feature Fire", e.Scopes));
    }

    [Fact]
    public async Task InvokingAFailingFeatureLogsTheFailureAndRethrows()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features();
        features
            .RegisterFeature<Explode, AnyCaller<Explode.Args>>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => features.Invoke(new Explode.Args(), Callers.Manager));

        var failure = logs.Entries.Last();
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.Matches(@"^Invoking Explode failed after \d+ ms$", failure.Message);
        Assert.Same(thrown, failure.Exception);
    }

    [Fact]
    public async Task FeaturesLogThroughABorrowedLoggerFactoryWithoutDisposingIt()
    {
        var logs = new CapturingLoggerProvider();
        using var hostLoggerFactory = LoggerFactory.Create(b => b.AddProvider(logs));

        await using (var features = new Features())
        {
            features
                .RegisterFeature<Fire, FireAuthorization>()
                .UseLoggerFactory(hostLoggerFactory);

            await features.Invoke(new Fire.Args("e1"), Callers.Manager);
        }

        hostLoggerFactory.CreateLogger("Host").LogInformation("Still logging");

        Assert.Contains(logs.Entries, e => e.Message == "Firing e1");
        Assert.Equal("Still logging", logs.Entries.Last().Message);
    }

    [Fact]
    public void LinksCreatedOutsideAnyOperationHaveNoCause()
    {
        Assert.Null(new Fire.Args("e1").CausedBy);
    }

    [Fact]
    public void LinksAreEqualRegardlessOfWhatCausedThem()
    {
        var link = new Fire.Args("e1");
        var causedElsewhere = link with { CausedBy = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" };

        Assert.Equal(link, causedElsewhere);
        Assert.Equal(link.GetHashCode(), causedElsewhere.GetHashCode());
        Assert.NotEqual(link, new Fire.Args("e2"));
    }

    [Fact]
    public async Task AnInvokedLinkIsCausedByTheInvocationThatCreatedItAndParentedByTheCurrentOperation()
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
        features
            .RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>()
            .RegisterFeature<Fire, FireAuthorization>();

        Fire.Args fireLink;
        using (new Activity("first request").Start())
        {
            var result = await features.Invoke(new EmployeeReview.Args("e1"), Callers.Manager);
            fireLink = result.LinkToFire!;
        }

        var review = Assert.Single(stopped, a => a.OperationName == nameof(EmployeeReview));
        Assert.Equal(review.Id, fireLink.CausedBy);

        using var secondRequest = new Activity("second request").Start();
        await features.Invoke(fireLink, Callers.Manager);

        var fire = Assert.Single(stopped, a => a.OperationName == nameof(Fire));
        Assert.Equal(secondRequest.Id, fire.ParentId);
        var cause = Assert.Single(fire.Links).Context;
        Assert.Equal(review.TraceId, cause.TraceId);
        Assert.Equal(review.SpanId, cause.SpanId);
    }

    [Fact]
    public async Task InvocationLogsCarryTheInvocationIdAndWhatCausedIt()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features();
        features
            .RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>()
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        var result = await features.Invoke(new EmployeeReview.Args("e1"), Callers.Manager);
        var fireLink = result.LinkToFire!;
        await features.Invoke(fireLink, Callers.Manager);

        var invokingReview = Assert.Single(logs.Entries, e => e.Message.StartsWith("Invoking EmployeeReview "));
        Assert.Equal($"Invoking EmployeeReview as {fireLink.CausedBy} caused by (null)", invokingReview.Message);
        Assert.Contains($"Invocation {fireLink.CausedBy} caused by (null)", invokingReview.Scopes);

        var firing = Assert.Single(logs.Entries, e => e.Message == "Firing e1");
        Assert.Contains(firing.Scopes, s => s.StartsWith("Invocation 00-") && s.EndsWith($" caused by {fireLink.CausedBy}"));
    }
}
