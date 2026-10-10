using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Featurewise.Tests;

public class IdempotencyTests
{
    [Fact]
    public async Task AFeatureCanReadTheIdempotencyKey()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<Remind, AnyCaller<Remind.Args>>()
            .RegisterDependencies(s => s.AddSingleton<SentReminders>());

        var withKey = await features.Invoke(new Remind.Args("e1"), Callers.Manager, "message-1");
        var withoutKey = await features.Invoke(new Remind.Args("e1"), Callers.Manager);

        Assert.Equal(new Remind.Result.Sent("message-1"), withKey);
        Assert.Equal(new Remind.Result.Sent(null), withoutKey);
    }

    [Fact]
    public async Task AFeatureCanRecogniseARedeliveryByItsIdempotencyKey()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<Remind, AnyCaller<Remind.Args>>()
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
        await using var features = new Features("hr");
        features
            .RegisterFeature<Remind, AnyCaller<Remind.Args>>()
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
        await using var features = new Features("hr");
        features
            .RegisterFeature<Remind, AnyCaller<Remind.Args>>()
            .RegisterDependencies(s => s.AddSingleton<SentReminders>());

        await Assert.ThrowsAsync<ArgumentException>(() => features.Invoke(new Remind.Args("e1"), Callers.Manager, key));
    }
}
