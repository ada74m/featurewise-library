using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Featurewise.Tests;

public class RegistrationTests
{
    [Fact]
    public async Task InvokingAnUnregisteredLinkThrows()
    {
        await using var features = new Features("hr");

        await Assert.ThrowsAsync<ArgumentException>(() =>
        {
            var link = new Fire.Args("e1");
            return features.Invoke(link, Callers.Manager);
        });
    }

    [Fact]
    public async Task InvokingALinkRegisteredWithoutItsFeatureThrows()
    {
        await using var features = new Features("hr");
        features.RegisterLinksTo<Fire>();

        await Assert.ThrowsAsync<ArgumentException>(() => features.Invoke(new Fire.Args("e1"), Callers.Manager));
    }

    [Fact]
    public async Task ARegisteredFeatureIsInvocableByItsNameQualifiedByTheService()
    {
        await using var features = new Features("hr")
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>();

        Assert.True(features.IsInvocable("hr/Fire"));
        Assert.True(features.IsInvocable("hr/review-employee"));
        Assert.False(features.IsInvocable("hr/EmployeeReview"));
        Assert.False(features.IsInvocable("Fire"));
        Assert.False(features.IsInvocable("payroll/Fire"));
        Assert.False(features.IsInvocable("hr/Nothing"));
    }

    [Fact]
    public async Task AFeatureWhoseLinksAloneAreRegisteredIsNotInvocable()
    {
        await using var features = new Features("hr")
            .RegisterLinksTo<Fire>();

        Assert.False(features.IsInvocable("hr/Fire"));
    }

    [Fact]
    public void OnlyAQueryIsAQuery()
    {
        using var features = new Features("hr")
            .RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>()
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterFeature<HandleEmployeeLeft, AnySystemCaller<HandleEmployeeLeft.Args>>();

        Assert.True(features.IsQuery("hr/review-employee"));
        Assert.False(features.IsQuery("hr/Fire"));
        Assert.False(features.IsQuery("hr/HandleEmployeeLeft"));
        Assert.False(features.IsQuery("review-employee"));
        Assert.False(features.IsQuery("payroll/review-employee"));
        Assert.False(features.IsQuery("hr/Nothing"));
    }

    [Fact]
    public void AQueryWhoseLinksAloneAreRegisteredIsAQuery()
    {
        using var features = new Features("hr").RegisterLinksTo<EmployeeSearch>();

        Assert.True(features.IsQuery("hr/EmployeeSearch"));
    }

    [Fact]
    public async Task AFeatureCanBeRegisteredBeforeOrAfterItsLinks()
    {
        await using var features = new Features("hr");
        features
            .RegisterLinksTo<Fire>()
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterLinksTo<Fire>();

        Assert.Equal(new Fire.Result.Fired(), await features.Invoke(new Fire.Args("e1"), Callers.Manager));
    }

    [Fact]
    public void RegisteringLinksToAFeatureThatNamesAnotherFeatureAsItselfThrows()
    {
        using var features = new Features("hr");
        Assert.Throws<ArgumentException>(() => features.RegisterLinksTo<Promote>());
    }

    [Fact]
    public void RegisteringAFeatureThatNamesAnotherFeatureAsItselfThrows()
    {
        using var features = new Features("hr");
        Assert.Throws<ArgumentException>(() => features.RegisterFeature<Promote, AnyCaller<Fire.Args>>());
    }

    [Fact]
    public void RegisteringAFeatureTwiceThrows()
    {
        using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>();
        Assert.Throws<InvalidOperationException>(() => features.RegisterFeature<Fire, FireAuthorization>());
    }

    [Fact]
    public async Task RegisteringAfterInvokingThrows()
    {
        await using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>();
        await features.Invoke(new Fire.Args("e1"), Callers.Manager);

        Assert.Throws<InvalidOperationException>(() => features.RegisterFeature<Probe, AnyCaller<Probe.Args>>());
        Assert.Throws<InvalidOperationException>(() => features.RegisterDependencies(s => s.AddScoped<ScopeProbe>()));
        Assert.Throws<InvalidOperationException>(() => features.UseLoggerFactory(NullLoggerFactory.Instance));
    }

    [Fact]
    public async Task InvokingAFeatureWithAnUnregisteredDependencyThrows()
    {
        await using var features = new Features("hr");
        features.RegisterFeature<Probe, AnyCaller<Probe.Args>>();

        await Assert.ThrowsAsync<AggregateException>(() => features.Invoke(new Probe.Args(), Callers.Manager));
    }

    [Fact]
    public async Task AFeatureThatDependsOnAnotherFeatureFailsTheFirstInvocation()
    {
        await using var features = new Features("hr");
        features
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterFeature<Dismiss, AnyCaller<Dismiss.Args>>();

        var thrown = await Assert.ThrowsAsync<AggregateException>(() => features.Invoke(new Fire.Args("e1"), Callers.Manager));

        Assert.Contains($"Unable to resolve service for type '{typeof(Fire).FullName}' while attempting to activate '{typeof(Dismiss).FullName}'", thrown.Message);
    }

    [Fact]
    public void RegisteringAFeatureWithARuleForAnotherFeaturesLinksThrows()
    {
        using var features = new Features("hr");

        var thrown = Assert.Throws<ArgumentException>(() => features.RegisterFeature<Fire, AnySystemCaller<Remind.Args>>());
        Assert.Equal("AnySystemCaller<Remind.Args> isn't a rule for Fire: a rule must derive from AuthorizationHandler<InvokeRequirement, Fire.Args>", thrown.Message);
    }

    [Fact]
    public void RegisteringAFeatureThatAllowsAnonymousCallersThrows()
    {
        using var features = new Features("hr");

        var thrown = Assert.Throws<ArgumentException>(() => features.RegisterFeature<OpenDoor, AnySignedInCaller<OpenDoor.Args>>());
        Assert.Contains("AnyCaller<OpenDoor.Args>", thrown.Message);
    }

    [Fact]
    public void ARuleNamedBySeveralFeaturesIsRegisteredOnce()
    {
        var services = new ServiceCollection();
        services.AddFeatures("hr", features => features
            .RegisterFeature<WhoAmI, AnyCaller<Link>>()
            .RegisterFeature<TossCoin, AnyCaller<Link>>());

        Assert.Single(services, d => d.ImplementationType == typeof(AnyCaller<Link>));
    }

    [Fact]
    public void AServiceIdMustNameSomethingAndCantContainASlash()
    {
        Assert.Throws<ArgumentException>(() => new Features(" "));
        Assert.Throws<ArgumentException>(() => new Features("hr/payroll"));
    }

    [Fact]
    public void RegisteringTwoFeaturesWithTheSameNameThrows()
    {
        using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>();

        Assert.Throws<InvalidOperationException>(() => features.RegisterFeature<Impostor, AnyCaller<Impostor.Args>>());
    }

    [Fact]
    public void BuildingChecksRegistrationsImmediately()
    {
        using var features = new Features("hr");
        features.RegisterFeature<Probe, AnyCaller<Probe.Args>>();

        Assert.Throws<AggregateException>(() => features.Build());
    }

    [Fact]
    public void BuildingClosesRegistration()
    {
        using var features = new Features("hr");
        features.RegisterFeature<WhoAmI, AnyCaller<WhoAmI.Args>>().Build();

        Assert.Throws<InvalidOperationException>(() => features.RegisterFeature<TossCoin, AnyCaller<TossCoin.Args>>());
        Assert.Throws<InvalidOperationException>(() => features.RegisterDependencies(_ => { }));
    }

    [Fact]
    public async Task ABuiltFeaturesCanBeBuiltAgainAndInvoked()
    {
        await using var features = new Features("hr").RegisterFeature<WhoAmI, AnyCaller<WhoAmI.Args>>().Build().Build();

        Assert.Equal("manager", await features.Invoke(new WhoAmI.Args(), Callers.Manager));
    }
}
