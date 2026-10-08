using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Featurewise.Tests;

public class FeaturesTests(ITestOutputHelper testOutputHelper)
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
    public async Task PrintTheLogsOfReviewingThenFiring()
    {
        var logs = new CapturingLoggerProvider();
        
        await using var features = new Features();
        features.RegisterFeature<Review>();
        features.RegisterFeature<Fire>().RegisterAuthorizationHandler<FireAuthorization>();
        features.RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));
        
        var link = new Review.Args("e1");
        var result = await features.Invoke(link, Callers.Manager);

        var fire = result.LinkToFire!;
        var fireResult = await features.Invoke(fire, Callers.Manager);
        
        testOutputHelper.WriteLine(result.Comments);
        
        testOutputHelper.WriteLine(string.Join(Environment.NewLine, logs.Entries));
    }

    [Fact]
    public async Task InvokingAnUnregisteredLinkThrows()
    {
        await using var features = new Features();

        await Assert.ThrowsAsync<ArgumentException>(() =>
        {
            var link = new Fire.Args("e1");
            return features.Invoke(link, Callers.Manager);
        });
    }

    [Fact]
    public void RegisteringAFeatureThatNamesAnotherFeatureAsItselfThrows()
    {
        using var features = new Features();
        Assert.Throws<ArgumentException>(() => features.RegisterFeature<Promote>());
    }

    [Fact]
    public void RegisteringAFeatureTwiceThrows()
    {
        using var features = new Features();
        features.RegisterFeature<Fire>();
        Assert.Throws<InvalidOperationException>(() => features.RegisterFeature<Fire>());
    }

    [Fact]
    public async Task RegisteringAfterInvokingThrows()
    {
        await using var features = new Features();
        features.RegisterFeature<Fire>().RegisterAuthorizationHandler<FireAuthorization>();
        await features.Invoke(new Fire.Args("e1"), Callers.Manager);

        Assert.Throws<InvalidOperationException>(() => features.RegisterFeature<Probe>());
        Assert.Throws<InvalidOperationException>(() => features.RegisterDependencies(s => s.AddScoped<ScopeProbe>()));
        Assert.Throws<InvalidOperationException>(() => features.UseLoggerFactory(NullLoggerFactory.Instance));
        Assert.Throws<InvalidOperationException>(() => features.RegisterAuthorizationHandler<FireAuthorization>());
    }

    [Fact]
    public async Task FeatureLogsCarryTheInvokedFeature()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>()
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
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>()
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
            .RegisterFeature<Explode>()
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
                .RegisterFeature<Fire>()
                .RegisterAuthorizationHandler<FireAuthorization>()
                .UseLoggerFactory(hostLoggerFactory);

            await features.Invoke(new Fire.Args("e1"), Callers.Manager);
        }

        hostLoggerFactory.CreateLogger("Host").LogInformation("Still logging");

        Assert.Contains(logs.Entries, e => e.Message == "Firing e1");
        Assert.Equal("Still logging", logs.Entries.Last().Message);
    }

    [Fact]
    public async Task InvokingAFeatureWithAnUnregisteredDependencyThrows()
    {
        await using var features = new Features();
        features.RegisterFeature<Probe>();

        await Assert.ThrowsAsync<AggregateException>(() => features.Invoke(new Probe.Args(), Callers.Manager));
    }

    [Fact]
    public async Task EachInvocationRunsInItsOwnScopeWhichIsDisposedAfterwards()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Probe>()
            .RegisterDependencies(services => services.AddScoped<ScopeProbe>());

        var first = await features.Invoke(new Probe.Args(), Callers.Manager);
        var second = await features.Invoke(new Probe.Args(), Callers.Manager);

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
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
            .RegisterFeature<Review>()
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>();

        Fire.Args fireLink;
        using (new Activity("first request").Start())
        {
            var result = await features.Invoke(new Review.Args("e1"), Callers.Manager);
            fireLink = result.LinkToFire!;
        }

        var review = Assert.Single(stopped, a => a.OperationName == nameof(Review));
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
            .RegisterFeature<Review>()
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        var result = await features.Invoke(new Review.Args("e1"), Callers.Manager);
        var fireLink = result.LinkToFire!;
        await features.Invoke(fireLink, Callers.Manager);

        var invokingReview = Assert.Single(logs.Entries, e => e.Message.StartsWith("Invoking Review "));
        Assert.Equal($"Invoking Review as {fireLink.CausedBy} caused by (null)", invokingReview.Message);
        Assert.Contains($"Invocation {fireLink.CausedBy} caused by (null)", invokingReview.Scopes);

        var firing = Assert.Single(logs.Entries, e => e.Message == "Firing e1");
        Assert.Contains(firing.Scopes, s => s.StartsWith("Invocation 00-") && s.EndsWith($" caused by {fireLink.CausedBy}"));
    }

    [Fact]
    public async Task AFeatureThatDependsOnAnotherFeatureFailsTheFirstInvocation()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>()
            .RegisterFeature<Dismiss>();

        var thrown = await Assert.ThrowsAsync<AggregateException>(() => features.Invoke(new Fire.Args("e1"), Callers.Manager));

        Assert.Contains($"Unable to resolve service for type '{typeof(Fire).FullName}' while attempting to activate '{typeof(Dismiss).FullName}'", thrown.Message);
    }

    [Fact]
    public async Task AFeatureThatInvokesALinkThrows()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>()
            .RegisterFeature<Escalate>()
            .RegisterDependencies(s => s.AddSingleton(features));

        await Assert.ThrowsAsync<InvalidOperationException>(() => features.Invoke(new Escalate.Args("e1"), Callers.Manager));
    }

    [Fact]
    public async Task InvocationsInSequenceAreNotNested()
    {
        await using var features = new Features();
        features.RegisterFeature<Fire>().RegisterAuthorizationHandler<FireAuthorization>();

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
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>()
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
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>()
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
            .RegisterFeature<GiveUp>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        await Assert.ThrowsAsync<OperationCanceledException>(() => features.Invoke(new GiveUp.Args(), Callers.Manager));

        var last = logs.Entries.Last();
        Assert.Equal(LogLevel.Error, last.Level);
        Assert.Matches(@"^Invoking GiveUp failed after \d+ ms$", last.Message);
    }

    [Fact]
    public void ALinkRoundTripsThroughItsBaseTypeWithItsFeatureNameAndCause()
    {
        using var features = new Features();
        features.RegisterFeature<Fire>();
        var options = features.ConfigureJson(new JsonSerializerOptions());
        Link<Fire.Result> link = new Fire.Args("e1") { CausedBy = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" };

        var json = JsonSerializer.Serialize(link, options);
        var readBack = JsonSerializer.Deserialize<Link<Fire.Result>>(json, options);

        Assert.Contains("\"$feature\":\"Fire\"", json);
        var fireLink = Assert.IsType<Fire.Args>(readBack);
        Assert.Equal("e1", fireLink.EmployeeId);
        Assert.Equal(link.CausedBy, fireLink.CausedBy);
    }

    [Fact]
    public void ALinkCarriesItsFeatureNameWhereverItIsSerialized()
    {
        using var features = new Features();
        features.RegisterFeature<Review>().RegisterFeature<Fire>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize(new Review.Result("comments", new Fire.Args("e1")), options);
        var readBack = JsonSerializer.Deserialize<Review.Result>(json, options)!;

        Assert.Contains("\"LinkToFire\":{\"$feature\":\"Fire\"", json);
        Assert.Equal(new Fire.Args("e1"), readBack.LinkToFire);
    }

    [Fact]
    public void AFeatureNameAttributeNamesTheFeatureInJson()
    {
        using var features = new Features();
        features.RegisterFeature<Review>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize<Link<Review.Result>>(new Review.Args("e1"), options);

        Assert.Contains("\"$feature\":\"review-employee\"", json);
    }

    [Fact]
    public void ALinkCanBeReadWithItsFeatureNameAnywhere()
    {
        using var features = new Features();
        features.RegisterFeature<Fire>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var readBack = JsonSerializer.Deserialize<Link<Fire.Result>>("""{"EmployeeId":"e1","$feature":"Fire"}""", options);

        Assert.Equal(new Fire.Args("e1"), readBack);
    }

    [Fact]
    public void SerializingALinkToAnUnregisteredFeatureThrows()
    {
        using var features = new Features();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize<Link<Fire.Result>>(new Fire.Args("e1"), options));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new Fire.Args("e1"), options));
    }

    [Fact]
    public void RegisteringTwoFeaturesWithTheSameNameThrows()
    {
        using var features = new Features();
        features.RegisterFeature<Fire>();

        Assert.Throws<InvalidOperationException>(() => features.RegisterFeature<Impostor>());
    }

    [Fact]
    public void ALinkReadFromJsonWithoutACauseHasNone()
    {
        using var features = new Features();
        features.RegisterFeature<Fire>();
        var options = features.ConfigureJson(new JsonSerializerOptions());
        using var request = new Activity("request reading the link").Start();

        var readThroughFeatures = JsonSerializer.Deserialize<Link<Fire.Result>>("""{"$feature":"Fire","EmployeeId":"e1"}""", options)!;
        var readPlainly = JsonSerializer.Deserialize<Fire.Args>("""{"EmployeeId":"e1"}""")!;

        Assert.Null(readThroughFeatures.CausedBy);
        Assert.Null(readPlainly.CausedBy);
        Assert.Equal(request.Id, new Fire.Args("e1").CausedBy);
    }

    [Fact]
    public async Task ALinkCanBeInvokedWithoutKnowingItsFeatureOrResult()
    {
        await using var features = new Features();
        features.RegisterFeature<Fire>().RegisterAuthorizationHandler<FireAuthorization>();
        Link link = new Fire.Args("e1");

        var result = await features.Invoke(link, Callers.Manager);

        Assert.Equal(new Fire.Result.Fired(), result);
    }

    [Fact]
    public async Task ALinkReadFromJsonAsAnyLinkCanBeInvoked()
    {
        await using var features = new Features();
        features.RegisterFeature<Review>().RegisterFeature<Fire>().RegisterAuthorizationHandler<FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());
        var json = JsonSerializer.Serialize(new Fire.Args("e1"), options);

        var link = JsonSerializer.Deserialize<Link>(json, options)!;
        var result = await features.Invoke(link, Callers.Manager);

        Assert.Equal(new Fire.Args("e1"), link);
        Assert.Equal("""{"$case":"Fired"}""", JsonSerializer.Serialize(result, options));
    }

    [Fact]
    public async Task ACallerNoRuleApprovesIsDeniedAndTheFeatureIsNotInvoked()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>()
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
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>();

        var denied = await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Anonymous));

        Assert.False(denied.CallerAuthenticated);
    }

    [Fact]
    public async Task RulesCanDependOnTheLinksArguments()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>();

        await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("manager"), Callers.Manager));
    }

    [Fact]
    public async Task AFeatureWithNoRuleIsDeniedToEveryone()
    {
        await using var features = new Features();
        features.RegisterFeature<Fire>();

        await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Manager));
    }

    [Fact]
    public async Task AFeatureThatAllowsAnonymousCallersNeedsNoRule()
    {
        await using var features = new Features();
        features.RegisterFeature<WhoAmI>();

        Assert.Null(await features.Invoke(new WhoAmI.Args(), Callers.Anonymous));
    }

    [Fact]
    public async Task AFeatureCanTakeItsCallerAsADependency()
    {
        await using var features = new Features();
        features.RegisterFeature<WhoAmI>();

        Assert.Equal("manager", await features.Invoke(new WhoAmI.Args(), Callers.Manager));
    }

    [Fact]
    public async Task AFeatureOffersALinkOnlyIfItsCallerMayInvokeIt()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Review>()
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>();

        var managersReview = await features.Invoke(new Review.Args("e1"), Callers.Manager);
        var employeesReview = await features.Invoke(new Review.Args("e1"), Callers.Employee);

        Assert.Equal(new Fire.Args("e1"), managersReview.LinkToFire);
        Assert.Null(employeesReview.LinkToFire);
    }

    [Fact]
    public async Task AnOfferedLinkIsCheckedAgainWhenInvoked()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Review>()
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>();

        var offered = (await features.Invoke(new Review.Args("e1"), Callers.Manager)).LinkToFire!;

        await Assert.ThrowsAsync<InvocationDeniedException>(() => features.Invoke(offered, Callers.Employee));
    }

    [Fact]
    public async Task AnySignedInCallerAdmitsSignedInCallersOnly()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<AnySignedInCaller<Fire.Args>>();

        var result = await features.Invoke(new Fire.Args("e1"), Callers.Employee);
        var denied = await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), Callers.Anonymous));

        Assert.IsType<Fire.Result.Fired>(result);
        Assert.False(denied.CallerAuthenticated);
    }

    [Fact]
    public async Task AnySignedInCallerAppliesOnlyToItsOwnLinkType()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Review>()
            .RegisterAuthorizationHandler<AnySignedInCaller<Review.Args>>()
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>();

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
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<AnySignedInCaller<Fire.Args>>();

        var result = await features.Invoke(new Fire.Args("e1"), SystemCaller.For("nightly-reminders"));

        Assert.IsType<Fire.Result.Fired>(result);
    }

    [Fact]
    public async Task ASystemCallerGetsNoSpecialTreatment()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>();

        await Assert.ThrowsAsync<InvocationDeniedException>(
            () => features.Invoke(new Fire.Args("e1"), SystemCaller.For("nightly-reminders")));
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
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>()
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
    public void AResultRoundTripsThroughItsBaseTypeWithItsCaseName()
    {
        using var features = new Features();
        features.RegisterFeature<Fire>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize<Fire.Result>(new Fire.Result.NotFound("e1"), options);
        var readBack = JsonSerializer.Deserialize<Fire.Result>(json, options);

        Assert.Equal("""{"$case":"NotFound","EmployeeId":"e1"}""", json);
        Assert.Equal(new Fire.Result.NotFound("e1"), readBack);
    }

    [Fact]
    public void AResultCarriesItsCaseNameWhereverItIsSerialized()
    {
        using var features = new Features();
        features.RegisterFeature<Fire>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize(new Fire.Result.Fired(), options);

        Assert.Equal("""{"$case":"Fired"}""", json);
        Assert.Equal(new Fire.Result.Fired(), JsonSerializer.Deserialize<Fire.Result>(json, options));
    }

    [Fact]
    public void AResultWithoutCasesIsSerializedAsItIs()
    {
        using var features = new Features();
        features.RegisterFeature<Review>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize(new Review.Result("comments", null), options);

        Assert.Equal("""{"Comments":"comments","LinkToFire":null}""", json);
    }

    [Fact]
    public void ASealedCaseNamesItselfOnlyThroughItsResultType()
    {
        using var features = new Features();
        features.RegisterFeature<TossCoin>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        Assert.Equal("""{"$case":"Heads"}""", JsonSerializer.Serialize<TossCoin.Result>(new TossCoin.Result.Heads(), options));
        Assert.Equal("{}", JsonSerializer.Serialize(new TossCoin.Result.Heads(), options));
        Assert.Equal("""{"$case":"Tails"}""", JsonSerializer.Serialize(new TossCoin.Result.Tails(), options));
    }

    // A host's container, checked the way ASP.NET checks one in development.
    private static ServiceProvider BuildHost(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    [Fact]
    public async Task AHostCanInvokeFeaturesRegisteredInItsContainer()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>());
        await using var host = BuildHost(services);

        var result = await host.GetRequiredService<Features>().Invoke(new Fire.Args("e1"), Callers.Manager);

        Assert.IsType<Fire.Result.Fired>(result);
    }

    [Fact]
    public async Task HostedFeaturesUseTheHostsServicesInScopesOfTheirOwn()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopeProbe>();
        services.AddFeatures(features => features.RegisterFeature<Probe>());
        await using var host = BuildHost(services);
        var features = host.GetRequiredService<Features>();

        await using var requestScope = host.CreateAsyncScope();
        var requestsProbe = requestScope.ServiceProvider.GetRequiredService<ScopeProbe>();
        var first = await features.Invoke(new Probe.Args(), Callers.Manager);
        var second = await features.Invoke(new Probe.Args(), Callers.Manager);

        Assert.Equal(3, new[] { requestsProbe.Id, first.Id, second.Id }.Distinct().Count());
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
        Assert.False(requestsProbe.Disposed);
    }

    [Fact]
    public async Task HostedFeaturesLogThroughTheHostsLogging()
    {
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logs));
        services.AddFeatures(features => features.RegisterFeature<WhoAmI>());
        await using var host = BuildHost(services);

        await host.GetRequiredService<Features>().Invoke(new WhoAmI.Args(), Callers.Manager);

        Assert.Contains(logs.Entries, e => e.Message.StartsWith("Invoking WhoAmI "));
    }

    [Fact]
    public async Task AHostsOwnClaimsPrincipalIsLeftAlone()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Callers.Employee);
        services.AddFeatures(features => features.RegisterFeature<WhoAmI>());
        await using var host = BuildHost(services);

        var invokedAs = await host.GetRequiredService<Features>().Invoke(new WhoAmI.Args(), Callers.Manager);
        await using var requestScope = host.CreateAsyncScope();

        Assert.Equal("manager", invokedAs);
        Assert.Same(Callers.Employee, requestScope.ServiceProvider.GetRequiredService<ClaimsPrincipal>());
    }

    [Fact]
    public async Task RegistrationsFromSeveralAddFeaturesCallsAccumulate()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features.RegisterFeature<WhoAmI>());
        services.AddFeatures(features => features.RegisterFeature<TossCoin>());
        await using var host = BuildHost(services);
        var features = host.GetRequiredService<Features>();

        Assert.Single(services, d => d.ServiceType == typeof(Features));
        Assert.Equal("manager", await features.Invoke(new WhoAmI.Args(), Callers.Manager));
        Assert.IsType<TossCoin.Result.Heads>(await features.Invoke(new TossCoin.Args(), Callers.Manager));
    }

    [Fact]
    public async Task AHostedFeaturesCannotRegister()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features.RegisterFeature<WhoAmI>());
        await using var host = BuildHost(services);
        var features = host.GetRequiredService<Features>();

        Assert.Throws<InvalidOperationException>(() => features.RegisterFeature<Fire>());
        Assert.Throws<InvalidOperationException>(() => features.RegisterDependencies(_ => { }));
        Assert.Throws<InvalidOperationException>(() => features.UseLoggerFactory(NullLoggerFactory.Instance));
    }

    [Fact]
    public async Task AHostedFeatureThatInvokesALinkThrows()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>()
            .RegisterFeature<Escalate>());
        await using var host = BuildHost(services);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetRequiredService<Features>().Invoke(new Escalate.Args("e1"), Callers.Manager));
    }

    [Fact]
    public async Task AHostedFeaturesSerializesLinksToItsFeatures()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features.RegisterFeature<Fire>());
        await using var host = BuildHost(services);
        var options = host.GetRequiredService<Features>().ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize<Link>(new Fire.Args("e1"), options);

        Assert.Equal(new Fire.Args("e1"), JsonSerializer.Deserialize<Link>(json, options));
    }

    [Fact]
    public void BuildingChecksRegistrationsImmediately()
    {
        using var features = new Features();
        features.RegisterFeature<Probe>();

        Assert.Throws<AggregateException>(() => features.Build());
    }

    [Fact]
    public void BuildingClosesRegistration()
    {
        using var features = new Features();
        features.RegisterFeature<WhoAmI>().Build();

        Assert.Throws<InvalidOperationException>(() => features.RegisterFeature<TossCoin>());
        Assert.Throws<InvalidOperationException>(() => features.RegisterDependencies(_ => { }));
    }

    [Fact]
    public async Task ABuiltFeaturesCanBeBuiltAgainAndInvoked()
    {
        await using var features = new Features().RegisterFeature<WhoAmI>().Build().Build();

        Assert.Equal("manager", await features.Invoke(new WhoAmI.Args(), Callers.Manager));
    }

    [Fact]
    public async Task BuildingAHostedFeaturesDoesNothing()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features.RegisterFeature<WhoAmI>());
        await using var host = BuildHost(services);
        var features = host.GetRequiredService<Features>();

        Assert.Same(features, features.Build());
        Assert.Equal("manager", await features.Invoke(new WhoAmI.Args(), Callers.Manager));
    }

    [Theory]
    [InlineData("""{"$feature":"Fire"}""")]
    [InlineData("""{"$feature":"Fire","EmployeeId":null}""")]
    [InlineData("""{"$feature":"FindEmployees","Limit":10}""")]
    [InlineData("""{"$feature":"FindEmployees","Query":"a"}""")]
    [InlineData("""{"$feature":"FindEmployees","Query":"a","Limit":null}""")]
    public void ALinkMissingANonNullableArgumentIsRejected(string json)
    {
        using var features = new Features();
        features.RegisterFeature<Fire>().RegisterFeature<FindEmployees>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Link>(json, options));
    }

    [Fact]
    public async Task ALinksNullableAndDefaultedArgumentsMayBeLeftOut()
    {
        await using var features = new Features();
        features.RegisterFeature<FindEmployees>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var link = JsonSerializer.Deserialize<Link>("""{"$feature":"FindEmployees","Query":"a","Limit":10}""", options)!;

        Assert.Equal("a 10 - - 1", await features.Invoke(link, Callers.Anonymous));
    }

    [Fact]
    public void TypesOtherThanLinksAreReadAsBefore()
    {
        using var features = new Features();
        features.RegisterFeature<Review>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var result = JsonSerializer.Deserialize<Review.Result>("{}", options)!;

        Assert.Null(result.Comments);
    }

    [Fact]
    public async Task ARuleThatThrowsFailsTheInvocationAndIsLoggedAsAFailure()
    {
        var logs = new CapturingLoggerProvider();
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<BrokenFireAuthorization>()
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
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<SlowFireAuthorization>()
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
        await using var features = new Features();
        features
            .RegisterFeature<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>()
            .RegisterDependencies(s => s.AddLogging(b => b.AddProvider(logs)));

        await Assert.ThrowsAsync<InvocationDeniedException>(() => features.Invoke(new Fire.Args("e1"), Callers.Employee));

        Assert.DoesNotContain(logs.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task AFeatureCanReadTheIdempotencyKey()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Remind>()
            .RegisterDependencies(s => s.AddSingleton<SentReminders>());

        var withKey = await features.Invoke(new Remind.Args("e1"), Callers.Manager, "message-1");
        var withoutKey = await features.Invoke(new Remind.Args("e1"), Callers.Manager);

        Assert.Equal(new Remind.Result.Sent("message-1"), withKey);
        Assert.Equal(new Remind.Result.Sent(null), withoutKey);
    }

    [Fact]
    public async Task AFeatureCanRecogniseARedeliveryByItsIdempotencyKey()
    {
        await using var features = new Features();
        features
            .RegisterFeature<Remind>()
            .RegisterDependencies(s => s.AddSingleton<SentReminders>());
        Link link = new Remind.Args("e1");

        var first = await features.Invoke(link, Callers.Manager, "message-1");
        var redelivered = await features.Invoke(link, Callers.Manager, "message-1");
        var newRequest = await features.Invoke(link, Callers.Manager, "message-2");

        Assert.IsType<Remind.Result.Sent>(first);
        Assert.IsType<Remind.Result.AlreadySent>(redelivered);
        Assert.IsType<Remind.Result.Sent>(newRequest);
    }

    [Fact]
    public async Task TheIdempotencyKeyIsLoggedAndTraced()
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
            .RegisterFeature<Remind>()
            .RegisterDependencies(s => s.AddSingleton<SentReminders>().AddLogging(b => b.AddProvider(logs)));

        await features.Invoke(new Remind.Args("e1"), Callers.Manager, "message-1");

        var starting = Assert.Single(logs.Entries, e => e.Message.StartsWith("Invoking Remind "));
        Assert.EndsWith(" for idempotency key message-1", starting.Message);
        Assert.Contains("Idempotency key message-1", starting.Scopes);
        Assert.Equal("message-1", Assert.Single(stopped, a => a.OperationName == nameof(Remind)).GetTagItem(Features.IdempotencyKeyTag));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ABlankIdempotencyKeyIsRejected(string key)
    {
        await using var features = new Features();
        features
            .RegisterFeature<Remind>()
            .RegisterDependencies(s => s.AddSingleton<SentReminders>());

        await Assert.ThrowsAsync<ArgumentException>(() => features.Invoke(new Remind.Args("e1"), Callers.Manager, key));
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
        features.RegisterFeature<DescribeInvocation>();
        const string cause = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        using var cancellation = new CancellationTokenSource();

        var described = await features.Invoke(
            new DescribeInvocation.Args { CausedBy = cause }, Callers.Manager, "message-1", cancellation.Token);

        var activity = Assert.Single(stopped, a => a.OperationName == nameof(DescribeInvocation));
        Assert.Equal(new DescribeInvocation.Result("manager", "message-1", activity.Id, cause, true), described);
    }

    [Fact]
    public async Task OutsideAnInvocationThereIsNoCaller()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features.RegisterFeature<WhoAmI>());
        await using var host = BuildHost(services);
        await using var requestScope = host.CreateAsyncScope();

        var invocation = requestScope.ServiceProvider.GetRequiredService<Invocation>();

        Assert.Throws<InvalidOperationException>(() => invocation.Caller);
        Assert.Null(invocation.Id);
    }
}
