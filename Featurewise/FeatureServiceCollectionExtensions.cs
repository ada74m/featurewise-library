using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Featurewise;

/// <summary>
/// Adds features to a host's service collection, so that they run in the host's container.
/// </summary>
public static class FeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers features in <paramref name="services"/>, and <see cref="Features"/> to invoke links to them.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="register">Registers features and the rules that say who may invoke them.</param>
    /// <returns><paramref name="services"/>, so that calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// Features then use the host's services directly, including its logging, configuration and options,
    /// and anything else registered in <paramref name="services"/>. Resolve or inject
    /// <see cref="Features"/> to invoke links; it is a singleton, disposed with the host's container.
    /// </para>
    /// <para>
    /// Each invocation still runs in a scope of its own, created from the host's root container rather
    /// than from the scope of, say, the HTTP request that invoked it, so a feature never shares scoped
    /// services such as a database context with its entrypoint.
    /// </para>
    /// <para>
    /// Call this as many times as needed; registrations accumulate. Whether missing dependencies are
    /// found when the host starts depends on whether the host's container validates its registrations,
    /// which ASP.NET does by default only in development.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddFeatures(features =&gt; features
    ///     .RegisterFeature&lt;Fire, Fire.Rule&gt;()
    ///     .RegisterFeature&lt;EmployeeReview, AnySignedInCaller&lt;EmployeeReview.Args&gt;&gt;());
    ///
    /// app.MapPost("/invoke", (Link link, ClaimsPrincipal user, Features features) =&gt; features.Invoke(link, user));
    /// </code>
    /// </example>
    public static IServiceCollection AddFeatures(this IServiceCollection services, Action<FeaturesBuilder> register)
    {
        var builder = new FeaturesBuilder(services);
        register(builder);
        services.TryAddSingleton(provider => new Features(provider, builder.Registry));
        return services;
    }
}
