using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Featurewise.Tests;

public class HostingTests
{
    // A host's container, checked the way ASP.NET checks one in development.
    private static ServiceProvider BuildHost(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    [Fact]
    public async Task AHostCanInvokeFeaturesRegisteredInItsContainer()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features
            .RegisterFeature<Fire, FireAuthorization>());
        await using var host = BuildHost(services);

        var result = await host.GetRequiredService<Features>().Invoke(new Fire.Args("e1"), Callers.Manager);

        Assert.IsType<Fire.Result.Fired>(result);
    }

    [Fact]
    public async Task HostedFeaturesUseTheHostsServicesInScopesOfTheirOwn()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopeProbe>();
        services.AddFeatures(features => features.RegisterFeature<Probe, AnyCaller<Probe.Args>>());
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
        services.AddFeatures(features => features.RegisterFeature<WhoAmI, AnyCaller<WhoAmI.Args>>());
        await using var host = BuildHost(services);

        await host.GetRequiredService<Features>().Invoke(new WhoAmI.Args(), Callers.Manager);

        Assert.Contains(logs.Entries, e => e.Message.StartsWith("Invoking WhoAmI "));
    }

    [Fact]
    public async Task AHostsOwnClaimsPrincipalIsLeftAlone()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Callers.Employee);
        services.AddFeatures(features => features.RegisterFeature<WhoAmI, AnyCaller<WhoAmI.Args>>());
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
        services.AddFeatures(features => features.RegisterFeature<WhoAmI, AnyCaller<WhoAmI.Args>>());
        services.AddFeatures(features => features.RegisterFeature<TossCoin, AnyCaller<TossCoin.Args>>());
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
        services.AddFeatures(features => features.RegisterFeature<WhoAmI, AnyCaller<WhoAmI.Args>>());
        await using var host = BuildHost(services);
        var features = host.GetRequiredService<Features>();

        Assert.Throws<InvalidOperationException>(() => features.RegisterFeature<Fire, FireAuthorization>());
        Assert.Throws<InvalidOperationException>(() => features.RegisterDependencies(_ => { }));
        Assert.Throws<InvalidOperationException>(() => features.UseLoggerFactory(NullLoggerFactory.Instance));
    }

    [Fact]
    public async Task AHostedFeatureThatInvokesALinkThrows()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features
            .RegisterFeature<Fire, FireAuthorization>()
            .RegisterFeature<Escalate, AnyCaller<Escalate.Args>>());
        await using var host = BuildHost(services);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetRequiredService<Features>().Invoke(new Escalate.Args("e1"), Callers.Manager));
    }

    [Fact]
    public async Task AHostedFeaturesSerializesLinksToItsFeatures()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features.RegisterFeature<Fire, FireAuthorization>());
        await using var host = BuildHost(services);
        var options = host.GetRequiredService<Features>().ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize<Link>(new Fire.Args("e1"), options);

        Assert.Equal(new Fire.Args("e1"), JsonSerializer.Deserialize<Link>(json, options));
    }

    [Fact]
    public async Task BuildingAHostedFeaturesDoesNothing()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features.RegisterFeature<WhoAmI, AnyCaller<WhoAmI.Args>>());
        await using var host = BuildHost(services);
        var features = host.GetRequiredService<Features>();

        Assert.Same(features, features.Build());
        Assert.Equal("manager", await features.Invoke(new WhoAmI.Args(), Callers.Manager));
    }

    [Fact]
    public async Task OutsideAnInvocationThereIsNoCaller()
    {
        var services = new ServiceCollection();
        services.AddFeatures(features => features.RegisterFeature<WhoAmI, AnyCaller<WhoAmI.Args>>());
        await using var host = BuildHost(services);
        await using var requestScope = host.CreateAsyncScope();

        var invocation = requestScope.ServiceProvider.GetRequiredService<Invocation>();

        Assert.Throws<InvalidOperationException>(() => invocation.Caller);
        Assert.Null(invocation.Id);
    }
}
