using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    internal FeaturesBuilder(IServiceCollection services, string service)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        if (service.Contains('/'))
            throw new ArgumentException($"a service id can't contain /, which separates it from feature names in links: {service}", nameof(service));
        Services = services;

        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(FeatureRegistry) && !d.IsKeyedService);
        if (existing is not null)
        {
            Registry = (FeatureRegistry)existing.ImplementationInstance!;
            if (Registry.Service != service)
                throw new InvalidOperationException(
                    $"the service collection already has features for the service {Registry.Service}, not {service}");
            return;
        }

        Registry = new FeatureRegistry(service);
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

    /// <summary>Registers a feature, and the rule that says who may invoke it.</summary>
    /// <typeparam name="TFeature">The feature: a <see cref="Query{TFeature, TArgs, TResult}"/>, <see cref="Command{TFeature, TArgs, TResult}"/> or <see cref="Worker{TFeature, TArgs}"/>.</typeparam>
    /// <typeparam name="TRule">
    /// An authorization handler for <see cref="InvokeRequirement"/> whose resource is the feature's
    /// arguments type, or a base of it: usually a rule nested in the feature, or one of
    /// <see cref="AnyCaller{TArgs}"/>, <see cref="AnySignedInCaller{TArgs}"/> and <see cref="AnySystemCaller{TArgs}"/>.
    /// </typeparam>
    /// <returns>This builder, so that calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// Every invocation is refused unless one of its feature's rules approves it, so every feature is
    /// registered with at least one. A feature with several rules may be invoked by anyone any of them
    /// approves. A rule named by several features is registered once.
    /// </para>
    /// <para>
    /// The feature and its rules are registered as scoped: each invocation resolves new instances in a
    /// new scope, so a rule can take the same dependencies as a feature.
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
    /// <typeparamref name="TFeature"/> doesn't name itself as its family's first type argument; a rule isn't an
    /// <see cref="AuthorizationHandler{TRequirement, TResource}"/> for <see cref="InvokeRequirement"/>
    /// and the feature's arguments; or the feature has <see cref="AllowAnonymousAttribute"/>, which
    /// Featurewise doesn't honour: register <see cref="AnyCaller{TArgs}"/> instead.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TFeature"/> is already registered, or another registered feature has the same name.
    /// </exception>
    public FeaturesBuilder RegisterFeature<TFeature, TRule>()
        where TFeature : class
        where TRule : class, IAuthorizationHandler =>
        AddFeature<TFeature>(typeof(TRule));

    /// <summary>Registers a feature, and two rules that each say who may invoke it.</summary>
    /// <typeparam name="TFeature">The feature: a <see cref="Query{TFeature, TArgs, TResult}"/>, <see cref="Command{TFeature, TArgs, TResult}"/> or <see cref="Worker{TFeature, TArgs}"/>.</typeparam>
    /// <typeparam name="TRule1">A rule for the feature; see <see cref="RegisterFeature{TFeature, TRule}"/>.</typeparam>
    /// <typeparam name="TRule2">Another rule for the feature.</typeparam>
    /// <returns>This builder, so that calls can be chained.</returns>
    /// <remarks>The feature may be invoked by anyone either rule approves.</remarks>
    /// <inheritdoc cref="RegisterFeature{TFeature, TRule}" path="/exception"/>
    public FeaturesBuilder RegisterFeature<TFeature, TRule1, TRule2>()
        where TFeature : class
        where TRule1 : class, IAuthorizationHandler
        where TRule2 : class, IAuthorizationHandler =>
        AddFeature<TFeature>(typeof(TRule1), typeof(TRule2));

    /// <summary>Registers a feature, and three rules that each say who may invoke it.</summary>
    /// <typeparam name="TFeature">The feature: a <see cref="Query{TFeature, TArgs, TResult}"/>, <see cref="Command{TFeature, TArgs, TResult}"/> or <see cref="Worker{TFeature, TArgs}"/>.</typeparam>
    /// <typeparam name="TRule1">A rule for the feature; see <see cref="RegisterFeature{TFeature, TRule}"/>.</typeparam>
    /// <typeparam name="TRule2">Another rule for the feature.</typeparam>
    /// <typeparam name="TRule3">Another rule for the feature.</typeparam>
    /// <returns>This builder, so that calls can be chained.</returns>
    /// <remarks>
    /// The feature may be invoked by anyone any of the rules approves. A feature that needs more rules
    /// than this is clearer with one rule that combines them.
    /// </remarks>
    /// <inheritdoc cref="RegisterFeature{TFeature, TRule}" path="/exception"/>
    public FeaturesBuilder RegisterFeature<TFeature, TRule1, TRule2, TRule3>()
        where TFeature : class
        where TRule1 : class, IAuthorizationHandler
        where TRule2 : class, IAuthorizationHandler
        where TRule3 : class, IAuthorizationHandler =>
        AddFeature<TFeature>(typeof(TRule1), typeof(TRule2), typeof(TRule3));

    private FeaturesBuilder AddFeature<TFeature>(params Type[] rules) where TFeature : class
    {
        var featureTypeArguments = FeatureTypeArgumentsOf<TFeature>();
        var argsType = featureTypeArguments[1];

        // Elsewhere in ASP.NET, [AllowAnonymous] opens an endpoint to everyone. Featurewise opens a
        // feature only through its rules, so a feature with the attribute wouldn't be what it seems.
        if (typeof(TFeature).GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>().Any())
            throw new ArgumentException(
                $"{typeof(TFeature).Name} has [AllowAnonymous], which Featurewise doesn't honour: register it with AnyCaller<{NameOf(argsType)}> to let anyone invoke it");

        foreach (var rule in rules)
        {
            var resource = Features.GenericArgumentsOf(rule, typeof(AuthorizationHandler<,>)) is [var requirement, var resourceType]
                && requirement == typeof(InvokeRequirement)
                    ? resourceType
                    : null;
            if (resource is null || !resource.IsAssignableFrom(argsType))
                throw new ArgumentException(
                    $"{NameOf(rule)} isn't a rule for {typeof(TFeature).Name}: a rule must derive from AuthorizationHandler<InvokeRequirement, {NameOf(argsType)}>");
        }

        if (Services.Any(d => d.ServiceType == typeof(TFeature)))
            throw new InvalidOperationException($"{typeof(TFeature).Name} is already registered");

        AddToRegistry<TFeature>(featureTypeArguments);
        Services.AddKeyedScoped<TFeature>(Features.FeatureKey);
        foreach (var rule in rules)
            Services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IAuthorizationHandler), rule));
        return this;
    }

    /// <summary>
    /// Registers a feature's links without the feature, so that links to it can be serialized here
    /// and invoked elsewhere.
    /// </summary>
    /// <typeparam name="TFeature">The feature: a <see cref="Query{TFeature, TArgs, TResult}"/>, <see cref="Command{TFeature, TArgs, TResult}"/> or <see cref="Worker{TFeature, TArgs}"/>.</typeparam>
    /// <returns>This builder, so that calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// An entrypoint that registers only some features may still create links to others, as when a
    /// feature puts a link on a queue for a worker that registers its feature. JSON options set up by
    /// <see cref="Features.ConfigureJson"/> serialize only links to features they know of, so register
    /// the links of every feature whose links leave this entrypoint without it.
    /// </para>
    /// <para>
    /// Links registered this way serialize and deserialize like links to registered features, but
    /// invoking one throws, as invoking any link to an unregistered feature does. Registering the
    /// feature itself, before or after, is allowed, and registering its links again does nothing.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TFeature"/> doesn't name itself as its family's first type argument.
    /// </exception>
    /// <exception cref="InvalidOperationException">Another registered feature has the same name.</exception>
    public FeaturesBuilder RegisterLinksTo<TFeature>() where TFeature : class
    {
        AddToRegistry<TFeature>(FeatureTypeArgumentsOf<TFeature>());
        return this;
    }

    // The type arguments TFeature passes, through its family, to Feature<TFeature, TArgs, TResult>.
    private static Type[] FeatureTypeArgumentsOf<TFeature>()
    {
        // The compiler can't stop a feature naming another feature as itself,
        // e.g. Promote : Command<Fire, Fire.Args, Fire.Result>, so check here.
        var featureTypeArguments = Features.GenericArgumentsOf(typeof(TFeature), typeof(Feature<,,>));
        var declaredSelf = featureTypeArguments?[0];
        if (declaredSelf != typeof(TFeature))
            throw new ArgumentException(
                $"{typeof(TFeature).Name} must name itself as its feature, not {declaredSelf?.Name}");

        return featureTypeArguments!;
    }

    private void AddToRegistry<TFeature>(Type[] featureTypeArguments)
    {
        var name = typeof(TFeature).GetCustomAttribute<FeatureNameAttribute>()?.Name ?? typeof(TFeature).Name;
        Registry.Add(featureTypeArguments[1], name, featureTypeArguments[2]);
    }

    // A type's name as C# writes it, such as Fire.Args or AnySignedInCaller<Fire.Args>.
    private static string NameOf(Type type)
    {
        var name = type.IsGenericType ? type.Name[..type.Name.IndexOf('`')] : type.Name;
        if (type.IsGenericType)
            name += $"<{string.Join(", ", type.GetGenericArguments().Select(NameOf))}>";
        return type.IsNested && !type.IsGenericParameter ? $"{NameOf(type.DeclaringType!)}.{name}" : name;
    }
}
