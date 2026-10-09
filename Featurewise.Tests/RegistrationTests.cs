using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Featurewise.Tests;

public class RegistrationTests
{
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
    public async Task InvokingALinkRegisteredWithoutItsFeatureThrows()
    {
        await using var features = new Features();
        features.RegisterLinksTo<Fire>().RegisterAuthorizationHandler<FireAuthorization>();

        await Assert.ThrowsAsync<ArgumentException>(() => features.Invoke(new Fire.Args("e1"), Callers.Manager));
    }

    [Fact]
    public async Task AFeatureCanBeRegisteredBeforeOrAfterItsLinks()
    {
        await using var features = new Features();
        features
            .RegisterLinksTo<Fire>()
            .RegisterFeature<Fire>()
            .RegisterLinksTo<Fire>()
            .RegisterAuthorizationHandler<FireAuthorization>();

        Assert.Equal(new Fire.Result.Fired(), await features.Invoke(new Fire.Args("e1"), Callers.Manager));
    }

    [Fact]
    public void RegisteringLinksToAFeatureThatNamesAnotherFeatureAsItselfThrows()
    {
        using var features = new Features();
        Assert.Throws<ArgumentException>(() => features.RegisterLinksTo<Promote>());
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
    public async Task InvokingAFeatureWithAnUnregisteredDependencyThrows()
    {
        await using var features = new Features();
        features.RegisterFeature<Probe>();

        await Assert.ThrowsAsync<AggregateException>(() => features.Invoke(new Probe.Args(), Callers.Manager));
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
    public void RegisteringTwoFeaturesWithTheSameNameThrows()
    {
        using var features = new Features();
        features.RegisterFeature<Fire>();

        Assert.Throws<InvalidOperationException>(() => features.RegisterFeature<Impostor>());
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
}
