using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Featurewise;

/// <summary>
/// Registers features, and the rules that say who may invoke them, in a service collection.
/// </summary>
/// <remarks>
/// <para>
/// A host gets one from <see cref="FeatureServiceCollectionExtensions.AddFeatures"/>. A standalone
/// <see cref="Features"/> uses one internally, behind its own registration methods.
/// </para>
/// <para>
/// The first builder for a service collection also registers what every invocation needs: logging,
/// authorization, <see cref="Invocation"/> and <see cref="LinkPermissions"/>. Registering logging
/// and authorization adds only what the collection lacks, so a host's own setup is kept.
/// </para>
/// </remarks>
public sealed class FeaturesBuilder
{
    internal FeaturesBuilder(IServiceCollection services)
    {
        Services = services;

        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(FeatureRegistry) && !d.IsKeyedService);
        if (existing is not null)
        {
            Registry = (FeatureRegistry)existing.ImplementationInstance!;
            return;
        }

        Registry = new FeatureRegistry();
        services.AddSingleton(Registry);

        // Loggers that write nowhere until the host or entrypoint adds providers, so features can
        // always take an ILogger<T>.
        services.AddLogging();

        services.AddAuthorizationCore();
        services.AddScoped(_ => new Invocation());
        services.AddScoped(provider => new LinkPermissions(
            provider.GetRequiredService<IAuthorizationService>(), provider.GetRequiredService<Invocation>()));
    }

    internal IServiceCollection Services { get; }

    internal FeatureRegistry Registry { get; }

    /// <summary>Registers a feature, so that links to it can be invoked.</summary>
    /// <typeparam name="TFeature">The feature, which derives from <see cref="Feature{TFeature, TArgs, TResult}"/>.</typeparam>
    /// <returns>This builder, so that calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// The feature is registered as scoped: each invocation resolves a new instance in a new scope.
    /// </para>
    /// <para>
    /// The feature is registered so that only <see cref="Features"/> can resolve it. A service or
    /// feature that depends on it can't be constructed, which fails when the container validates its
    /// registrations, if it does, or else at the first invocation that needs it.
    /// </para>
    /// <para>
    /// The feature's name is its class name, unless the class has a <see cref="FeatureNameAttribute"/>.
    /// Serialized links carry this name, so it must be unique among registered features.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TFeature"/> doesn't derive from <see cref="Feature{TFeature, TArgs, TResult}"/>
    /// with itself as the first type argument.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TFeature"/> is already registered, or another registered feature has the same name.
    /// </exception>
    public FeaturesBuilder RegisterFeature<TFeature>() where TFeature : class
    {
        // The compiler can't stop a feature naming another feature as itself,
        // e.g. Promote : Feature<Fire, Fire.Args, Fire.Result>, so check here.
        var featureTypeArguments = Features.GenericArgumentsOf(typeof(TFeature), typeof(Feature<,,>));
        var declaredSelf = featureTypeArguments?[0];
        if (declaredSelf != typeof(TFeature))
            throw new ArgumentException(
                $"{typeof(TFeature).Name} must derive from Feature<{typeof(TFeature).Name}, ...>, not Feature<{declaredSelf?.Name}, ...>");

        if (Services.Any(d => d.ServiceType == typeof(TFeature)))
            throw new InvalidOperationException($"{typeof(TFeature).Name} is already registered");

        var name = typeof(TFeature).GetCustomAttribute<FeatureNameAttribute>()?.Name ?? typeof(TFeature).Name;
        Registry.Add(featureTypeArguments![1], name, featureTypeArguments[2]);
        Services.AddKeyedScoped<TFeature>(Features.FeatureKey);
        return this;
    }

    /// <summary>Registers an authorization handler, which says who may invoke links to some features.</summary>
    /// <typeparam name="THandler">
    /// An authorization handler for <see cref="InvokeRequirement"/>, usually derived from
    /// <see cref="AuthorizationHandler{TRequirement, TResource}"/> with a feature's arguments type as
    /// the resource.
    /// </typeparam>
    /// <returns>This builder, so that calls can be chained.</returns>
    /// <remarks>
    /// The handler is registered as scoped, so it can take the same dependencies as a feature.
    /// </remarks>
    public FeaturesBuilder RegisterAuthorizationHandler<THandler>() where THandler : class, IAuthorizationHandler
    {
        Services.AddScoped<IAuthorizationHandler, THandler>();
        return this;
    }
}
