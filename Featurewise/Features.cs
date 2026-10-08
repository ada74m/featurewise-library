using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Featurewise;

/// <summary>
/// An entrypoint's view of the application: register features and their dependencies, then invoke links.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Features"/> runs either in a host's container or in one of its own:
/// </para>
/// <list type="bullet">
/// <item><description>
/// Hosted, as in ASP.NET or a worker service: register features in the host's service collection with
/// <see cref="FeatureServiceCollectionExtensions.AddFeatures"/>, then resolve or inject
/// <see cref="Features"/>. Features use the host's services directly. The host disposes it.
/// </description></item>
/// <item><description>
/// Standalone, as in a console tool or a test: create one with <see cref="Features()"/>, register
/// features with <see cref="RegisterFeature{TFeature}"/> and everything they depend on with
/// <see cref="RegisterDependencies"/> and <see cref="UseLoggerFactory"/>, then call
/// <see cref="Invoke{TResult}(Link{TResult}, ClaimsPrincipal, CancellationToken)"/>.
/// <see cref="Build"/>, or else the first invocation, builds its container and closes registration.
/// Dispose it when the entrypoint shuts down.
/// </description></item>
/// </list>
/// <para>
/// Each invocation runs in its own dependency injection scope and its own <see cref="Activity"/>, and
/// is logged when it starts, finishes and fails.
/// </para>
/// <para>
/// Links can leave the process and come back, for example to a client and back again. To serialize
/// them, use JSON options set up by <see cref="ConfigureJson"/>, which write each link's feature name
/// into its JSON so that it can be read back through any base type.
/// </para>
/// <para>
/// Every invocation has a caller, and is refused unless an authorization handler registered with
/// <see cref="RegisterAuthorizationHandler{THandler}"/> approves the link for that caller, or the
/// feature has <see cref="AllowAnonymousAttribute"/>. See <see cref="InvokeRequirement"/>.
/// </para>
/// <para>
/// Features don't invoke other features, directly or through <see cref="Features"/>. A feature that
/// leads to another returns a link to it, and the entrypoint decides whether to invoke that link.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await using var features = new Features();
/// features
///     .RegisterFeature&lt;Fire&gt;()
///     .RegisterAuthorizationHandler&lt;FireAuthorization&gt;()
///     .RegisterDependencies(services =&gt; services.AddScoped&lt;IEmployeeStore, SqlEmployeeStore&gt;())
///     .UseLoggerFactory(hostLoggerFactory);
///
/// var result = await features.Invoke(new Fire.Args("e1"), httpContext.User);
/// </code>
/// </example>
public class Features : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> that invocation activities come
    /// from. Pass it to OpenTelemetry's <c>AddSource</c> to export them.
    /// </summary>
    public const string ActivitySourceName = "Featurewise";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    // Features are registered under this key, which only Featurewise holds, so nothing else in the
    // container can depend on a feature.
    internal static readonly object FeatureKey = new();

    // Set for the duration of each invocation, so a nested call to Invoke can be rejected however the
    // feature got hold of a Features.
    private static readonly AsyncLocal<bool> Invoking = new();

    /// <summary>
    /// The JSON property that names a link's feature, in JSON written with options set up by
    /// <see cref="ConfigureJson"/>.
    /// </summary>
    public const string FeatureNameProperty = "$feature";

    /// <summary>
    /// The JSON property that names a result's case, in JSON written with options set up by
    /// <see cref="ConfigureJson"/>.
    /// </summary>
    public const string CaseProperty = "$case";

    /// <summary>
    /// The tag on an invocation's <see cref="Activity"/> that names the type of the feature's result,
    /// which for a result with cases is the case.
    /// </summary>
    public const string OutcomeTag = "featurewise.outcome";

    /// <summary>
    /// The tag on an invocation's <see cref="Activity"/> that holds the idempotency key the entrypoint
    /// gave, if any.
    /// </summary>
    public const string IdempotencyKeyTag = "featurewise.idempotency_key";

    private readonly FeatureRegistry _registry;
    private readonly ActivityListener _activityListener;

    // Standalone: a container of our own, built on the first invocation.
    private readonly FeaturesBuilder? _builder;
    private readonly Lazy<ServiceProvider>? _ownProvider;

    // Hosted: the host's container, already built.
    private readonly IServiceProvider? _hostProvider;

    /// <summary>
    /// Creates a standalone <see cref="Features"/>, with a container of its own and no features registered.
    /// </summary>
    /// <remarks>
    /// Logging and authorization are registered, so that any feature can take an
    /// <see cref="ILogger{TCategoryName}"/>, <see cref="Invocation"/> and <see cref="LinkPermissions"/>.
    /// Log output goes nowhere until the entrypoint adds logging providers through
    /// <see cref="RegisterDependencies"/> or borrows a logger factory with <see cref="UseLoggerFactory"/>.
    /// </remarks>
    public Features()
    {
        _activityListener = ListenToInvocations();
        var services = new ServiceCollection();
        _builder = new FeaturesBuilder(services);
        _registry = _builder.Registry;
        _ownProvider = new Lazy<ServiceProvider>(() => services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }));
    }

    // Created by the host's container, as registered by AddFeatures.
    internal Features(IServiceProvider hostProvider, FeatureRegistry registry)
    {
        _activityListener = ListenToInvocations();
        _hostProvider = hostProvider;
        _registry = registry;
    }

    private IServiceProvider Provider => _hostProvider ?? _ownProvider!.Value;

    /// <summary>Registers a feature in a standalone <see cref="Features"/>, so that links to it can be invoked.</summary>
    /// <typeparam name="TFeature">The feature, which derives from <see cref="Feature{TFeature, TArgs, TResult}"/>.</typeparam>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <remarks>
    /// This registers the feature as <see cref="FeaturesBuilder.RegisterFeature{TFeature}"/> does, and
    /// building the container checks every registration, so a service or feature that depends on a
    /// feature fails the first invocation.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TFeature"/> doesn't derive from <see cref="Feature{TFeature, TArgs, TResult}"/>
    /// with itself as the first type argument.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TFeature"/> is already registered, another registered feature has the same
    /// name, registration is closed, or this <see cref="Features"/> is hosted.
    /// </exception>
    public Features RegisterFeature<TFeature>() where TFeature : class
    {
        EnsureRegistrationOpen().RegisterFeature<TFeature>();
        return this;
    }

    /// <summary>Registers services that features depend on, in a standalone <see cref="Features"/>.</summary>
    /// <param name="register">
    /// Adds registrations to the container's service collection. Any registration works, including
    /// library extension methods such as <c>AddLogging</c> or <c>AddDbContext</c>.
    /// </param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <remarks>
    /// Call this as many times as needed, before the container is built. Building it checks that every
    /// registered service can be resolved, so a missing dependency fails <see cref="Build"/>, or the
    /// first invocation, rather than the first use of the feature that needs it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Registration is closed, or this <see cref="Features"/> is hosted.
    /// </exception>
    public Features RegisterDependencies(Action<IServiceCollection> register)
    {
        register(EnsureRegistrationOpen().Services);
        return this;
    }

    /// <summary>
    /// Registers an authorization handler in a standalone <see cref="Features"/>, which says who may
    /// invoke links to some features.
    /// </summary>
    /// <typeparam name="THandler">
    /// An authorization handler for <see cref="InvokeRequirement"/>; see
    /// <see cref="FeaturesBuilder.RegisterAuthorizationHandler{THandler}"/>.
    /// </typeparam>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <exception cref="InvalidOperationException">
    /// Registration is closed, or this <see cref="Features"/> is hosted.
    /// </exception>
    public Features RegisterAuthorizationHandler<THandler>() where THandler : class, IAuthorizationHandler
    {
        EnsureRegistrationOpen().RegisterAuthorizationHandler<THandler>();
        return this;
    }

    /// <summary>
    /// Makes a standalone <see cref="Features"/> log through a logger factory the entrypoint already
    /// has, so that logging is configured in one place. A hosted <see cref="Features"/> logs through
    /// the host's logging already.
    /// </summary>
    /// <param name="loggerFactory">
    /// The factory to log through. <see cref="Features"/> doesn't dispose it; the entrypoint still owns it.
    /// </param>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <remarks>
    /// The borrowed factory replaces the one <see cref="Features"/> registers itself, so logging
    /// providers added through <see cref="RegisterDependencies"/> are ignored.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Registration is closed, or this <see cref="Features"/> is hosted.
    /// </exception>
    public Features UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        var services = EnsureRegistrationOpen().Services;
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton(loggerFactory);
        return this;
    }

    /// <summary>
    /// Builds a standalone <see cref="Features"/>' container now, checking that every registered service
    /// can be resolved, and closes registration. Otherwise the first invocation does this.
    /// </summary>
    /// <returns>This instance, so that calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// Call this when the entrypoint starts, so that a misconfiguration fails at startup and the cost of
    /// building the container isn't added to the first invocation. In an AWS Lambda function, call it
    /// in the function's constructor, so that the work happens during initialization.
    /// </para>
    /// <para>
    /// Calling it again does nothing, and so does calling it on a hosted <see cref="Features"/>, whose
    /// container the host has built already.
    /// </para>
    /// </remarks>
    /// <exception cref="AggregateException">
    /// A registered service depends on a service that isn't registered, or on a feature.
    /// </exception>
    public Features Build()
    {
        _ = _ownProvider?.Value;
        return this;
    }

    /// <summary>
    /// Sets up JSON options to serialize links and results: each link's JSON names its feature, and
    /// each result's JSON names its case, so both can be read back through their base types.
    /// </summary>
    /// <param name="options">
    /// The options to set up, such as ASP.NET's. Set them up before they are first used, when they
    /// become read-only. In a host, the options pattern does this, e.g.
    /// <c>services.AddOptions&lt;JsonOptions&gt;().Configure&lt;Features&gt;((json, features) =&gt; features.ConfigureJson(json.SerializerOptions))</c>.
    /// </param>
    /// <returns><paramref name="options"/>, so that the call can be chained.</returns>
    /// <remarks>
    /// <para>
    /// A link's JSON names its feature in a <see cref="FeatureNameProperty"/> property, using the name
    /// given at <see cref="RegisterFeature{TFeature}"/>. Reading a link through a base type requires
    /// that property, and accepts it anywhere in the object.
    /// </para>
    /// <para>
    /// Links come back from clients, so reading a link checks its arguments as their types declare: a
    /// non-nullable argument without a default must be present, and no non-nullable argument may be
    /// null. A link that breaks either rule throws <see cref="JsonException"/> rather than reaching its
    /// feature. Nullable arguments may be left out. This applies to links alone, so a host's other
    /// types are read as before.
    /// </para>
    /// <para>
    /// The options see features registered at any time before a link type is first serialized or
    /// deserialized with them. Serializing a link to a feature that isn't registered throws
    /// <see cref="NotSupportedException"/> rather than writing JSON that can't be read back.
    /// </para>
    /// <para>
    /// A feature whose outcomes differ in kind, such as fired or not found, can return a result with
    /// cases: an abstract result type whose cases are public types nested in it and derived from it.
    /// Such a result's JSON names its case in a <see cref="CaseProperty"/> property, using the case's
    /// type name, so it can be read back through the result type. This applies to the result types of
    /// registered features.
    /// </para>
    /// <para>
    /// Leave cases unsealed. A sealed case names itself only when written through its result type, not
    /// when written as itself or as <see cref="object"/>, which is how an endpoint writes the result of
    /// <see cref="Invoke(Link, ClaimsPrincipal, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    public JsonSerializerOptions ConfigureJson(JsonSerializerOptions options) => _registry.ConfigureJson(options);

    /// <summary>
    /// Invokes the feature that <paramref name="link"/> names, with the arguments it carries, and returns
    /// its result typed as the link promises.
    /// </summary>
    /// <typeparam name="TResult">The type that the feature produces.</typeparam>
    /// <param name="link">The link to invoke.</param>
    /// <param name="caller">Who is invoking the link.</param>
    /// <param name="cancellationToken">Cancels the invocation. The feature receives it too.</param>
    /// <returns>The feature's result.</returns>
    /// <remarks>
    /// This behaves as <see cref="Invoke(Link, ClaimsPrincipal, CancellationToken)"/> does, which
    /// describes the authorization, scope, tracing, logging and cancellation of an invocation.
    /// </remarks>
    /// <inheritdoc cref="Invoke(Link, ClaimsPrincipal, CancellationToken)" path="/exception"/>
    public async Task<TResult> Invoke<TResult>(
        Link<TResult> link, ClaimsPrincipal caller, CancellationToken cancellationToken = default) =>
        (TResult)(await Invoke((Link)link, caller, cancellationToken))!;

    /// <summary>
    /// Invokes the feature that <paramref name="link"/> names, with the arguments it carries, for a
    /// request identified by <paramref name="idempotencyKey"/>, and returns its result typed as the link
    /// promises.
    /// </summary>
    /// <typeparam name="TResult">The type that the feature produces.</typeparam>
    /// <param name="link">The link to invoke.</param>
    /// <param name="caller">Who is invoking the link.</param>
    /// <param name="idempotencyKey">
    /// Identifies the request, so the feature can recognise a redelivery of it; see
    /// <see cref="Invocation.IdempotencyKey"/>. <see langword="null"/> for none.
    /// </param>
    /// <param name="cancellationToken">Cancels the invocation. The feature receives it too.</param>
    /// <returns>The feature's result.</returns>
    /// <remarks>
    /// This behaves as <see cref="Invoke(Link, ClaimsPrincipal, string?, CancellationToken)"/> does.
    /// </remarks>
    /// <inheritdoc cref="Invoke(Link, ClaimsPrincipal, string?, CancellationToken)" path="/exception"/>
    public async Task<TResult> Invoke<TResult>(
        Link<TResult> link, ClaimsPrincipal caller, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        (TResult)(await Invoke((Link)link, caller, idempotencyKey, cancellationToken))!;

    /// <summary>
    /// Invokes the feature that <paramref name="link"/> names, with the arguments it carries, without
    /// knowing which feature that is or what it produces.
    /// </summary>
    /// <param name="link">The link to invoke.</param>
    /// <param name="caller">
    /// Who is invoking the link, such as an HTTP request's user. Code acting on its own behalf, such as
    /// a scheduled job, passes an identity of its own from <see cref="SystemCaller.For"/> rather than
    /// an anonymous one.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the invocation. The feature receives it, and so do services that take
    /// <see cref="Invocation"/>, such as authorization handlers.
    /// </param>
    /// <returns>The feature's result.</returns>
    /// <remarks>
    /// <para>
    /// Use this for links whose feature isn't known in advance, such as links a general-purpose endpoint
    /// reads from JSON as <see cref="Link"/>. When the result type is known,
    /// <see cref="Invoke{TResult}(Link{TResult}, ClaimsPrincipal, CancellationToken)"/> returns it typed.
    /// </para>
    /// <para>
    /// In a standalone <see cref="Features"/> that hasn't been built with <see cref="Build"/>, the first
    /// invocation builds the container and closes registration.
    /// </para>
    /// <para>
    /// Each invocation creates a dependency injection scope, resolves the feature in it, invokes the
    /// feature and disposes the scope. A hosted <see cref="Features"/> creates the scope from the host's
    /// root container, not from the entrypoint's scope. Within the scope, <see cref="Invocation"/>
    /// describes the invocation, including <paramref name="caller"/>.
    /// </para>
    /// <para>
    /// Before the feature is resolved, the invocation is refused unless the feature has
    /// <see cref="AllowAnonymousAttribute"/> or an authorization handler approves the link for
    /// <paramref name="caller"/>. A refusal is logged as a warning, naming the caller. A handler that
    /// throws or is cancelled fails or cancels the invocation, as the feature would.
    /// </para>
    /// <para>
    /// Each invocation runs in a new <see cref="Activity"/> named after the feature. Its parent is the
    /// current activity, such as an HTTP request. If
    /// <see cref="Link.CausedBy"/> is set, the activity is also linked to the operation that
    /// created the link.
    /// </para>
    /// <para>
    /// Each invocation is logged under the feature's category when it starts, finishes and fails. The
    /// finish message names the type of the result, which for a result with cases is the case, so an
    /// expected failure the feature returns rather than throws, such as not found, shows in the logs
    /// without being logged as an error. The activity's <see cref="OutcomeTag"/> tag names it too.
    /// Everything logged during the invocation, including the feature's own logging, carries scopes
    /// naming the feature, the invocation's id and what caused it. The start message names both ids
    /// too, so causation shows even in logs that leave scopes out.
    /// </para>
    /// <para>An exception thrown by the feature is logged, then propagates unchanged.</para>
    /// <para>
    /// If <paramref name="cancellationToken"/> is already cancelled, the feature isn't invoked. If it is
    /// cancelled during the invocation and the feature stops with an
    /// <see cref="OperationCanceledException"/>, the cancellation is logged as such rather than as a
    /// failure. An <see cref="OperationCanceledException"/> the feature throws for any other reason,
    /// such as a timeout of its own, is a failure.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="link"/> doesn't derive from <see cref="Link{TFeature, TResult}"/>, or the feature
    /// it names isn't registered.
    /// </exception>
    /// <exception cref="AggregateException">
    /// This is the first invocation of a standalone <see cref="Features"/> that hasn't been built with
    /// <see cref="Build"/>, and a registered service depends on a service that isn't registered, or on a feature.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This is called during another invocation: features don't invoke other features.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled before or during the invocation.
    /// </exception>
    /// <exception cref="InvocationDeniedException">
    /// <paramref name="caller"/> may not invoke <paramref name="link"/>.
    /// </exception>
    public Task<object?> Invoke(Link link, ClaimsPrincipal caller, CancellationToken cancellationToken = default) =>
        Invoke(link, caller, idempotencyKey: null, cancellationToken);

    /// <summary>
    /// Invokes the feature that <paramref name="link"/> names, with the arguments it carries, for a
    /// request identified by <paramref name="idempotencyKey"/>, without knowing which feature that is
    /// or what it produces.
    /// </summary>
    /// <param name="link">The link to invoke.</param>
    /// <param name="caller">Who is invoking the link.</param>
    /// <param name="idempotencyKey">
    /// Identifies the request, such as a queue message's id or an HTTP request's <c>Idempotency-Key</c>
    /// header, so the feature can recognise a redelivery of it through
    /// <see cref="Invocation.IdempotencyKey"/>. <see langword="null"/> for none.
    /// </param>
    /// <param name="cancellationToken">Cancels the invocation. The feature receives it too.</param>
    /// <returns>The feature's result.</returns>
    /// <remarks>
    /// <para>
    /// This behaves as <see cref="Invoke(Link, ClaimsPrincipal, CancellationToken)"/> does, which
    /// describes the authorization, scope, tracing, logging and cancellation of an invocation.
    /// </para>
    /// <para>
    /// The framework doesn't act on the key: the feature decides what being invoked again for the same
    /// request means. The key is logged in the start message and a scope, and tagged on the activity as
    /// <see cref="IdempotencyKeyTag"/>, so redeliveries can be seen.
    /// </para>
    /// </remarks>
    /// <inheritdoc cref="Invoke(Link, ClaimsPrincipal, CancellationToken)" path="/exception"/>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is empty or white space.</exception>
    public async Task<object?> Invoke(
        Link link, ClaimsPrincipal caller, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (idempotencyKey is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (Invoking.Value)
            throw new InvalidOperationException(
                $"can't invoke {link.GetType().Name} during another invocation: features don't invoke other features, they return links to them");

        // Changes to an AsyncLocal inside an async method don't flow back to its caller, so this
        // clears itself when the invocation ends.
        Invoking.Value = true;

        cancellationToken.ThrowIfCancellationRequested();

        var featureType = FeatureTypeOf(link);
        if (!Provider.GetRequiredService<IServiceProviderIsKeyedService>().IsKeyedService(featureType, FeatureKey))
            throw new ArgumentException($"{featureType.Name} is not registered");

        await using var scope = Provider.CreateAsyncScope();

        // The invocation's parent is whatever operation is running now, e.g. an HTTP request; the
        // operation that created the link is recorded as an ActivityLink.
        ActivityLink[]? causes = ActivityContext.TryParse(link.CausedBy, null, out var cause)
            ? [new ActivityLink(cause)]
            : null;
        using var activity = ActivitySource.StartActivity(featureType.Name, ActivityKind.Internal, parentContext: default, links: causes);

        scope.ServiceProvider.GetRequiredService<Invocation>()
            .Begin(caller, cancellationToken, idempotencyKey, activity?.Id, link.CausedBy);

        // Logs under the feature's own category, and tags everything logged during this
        // invocation with the feature that was invoked, its id and what caused it.
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(featureType);
        using var featureLogScope = logger.BeginScope("Feature {Feature}", featureType.Name);
        using var invocationLogScope = logger.BeginScope("Invocation {InvocationId} caused by {CausedBy}", activity?.Id, link.CausedBy);
        using var idempotencyLogScope = idempotencyKey is null
            ? null
            : logger.BeginScope("Idempotency key {IdempotencyKey}", idempotencyKey);
        if (idempotencyKey is not null)
            activity?.SetTag(IdempotencyKeyTag, idempotencyKey);

        // The ids are in the scope too, but repeating them here keeps causation visible in logs
        // that don't show scopes, such as the console logger by default.
        if (idempotencyKey is null)
            logger.LogInformation("Invoking {Feature} as {InvocationId} caused by {CausedBy}",
                featureType.Name, activity?.Id, link.CausedBy);
        else
            logger.LogInformation("Invoking {Feature} as {InvocationId} caused by {CausedBy} for idempotency key {IdempotencyKey}",
                featureType.Name, activity?.Id, link.CausedBy, idempotencyKey);
        var started = Stopwatch.GetTimestamp();

        // Authorization is inside the try, so a rule that fails, e.g. because the store it reads is
        // down, is logged and traced like a failing feature.
        try
        {
            // Checked before the feature is resolved, so a refused invocation never constructs it.
            if (await scope.ServiceProvider.GetRequiredService<LinkPermissions>().MayInvoke(link))
            {
                var feature = (IFeature)scope.ServiceProvider.GetRequiredKeyedService(featureType, FeatureKey);
                var result = await feature.Invoke(link, cancellationToken);
                var outcome = result?.GetType().Name ?? "null";
                activity?.SetTag(OutcomeTag, outcome);
                logger.LogInformation("Invoked {Feature} in {ElapsedMilliseconds:0} ms: {Outcome}",
                    featureType.Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds, outcome);
                return result;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller cancelled, so this isn't a failure of the feature.
            logger.LogInformation("Invoking {Feature} was cancelled after {ElapsedMilliseconds:0} ms",
                featureType.Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            logger.LogError(e, "Invoking {Feature} failed after {ElapsedMilliseconds:0} ms",
                featureType.Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }

        // Refused: outside the try, so a refusal is logged as one rather than as a failure.
        activity?.SetStatus(ActivityStatusCode.Error, "denied");
        logger.LogWarning("Denied {Caller} invoking {Feature}", caller.Identity?.Name, featureType.Name);
        throw new InvocationDeniedException(featureType.Name, caller.Identity?.IsAuthenticated == true);
    }

    /// <summary>
    /// Stops listening for invocation activities and, if standalone, disposes the container and
    /// everything it created. A hosted <see cref="Features"/> leaves the host's container to the host.
    /// </summary>
    public void Dispose()
    {
        _activityListener.Dispose();
        if (_ownProvider?.IsValueCreated == true)
            _ownProvider.Value.Dispose();
    }

    /// <summary>
    /// Stops listening for invocation activities and, if standalone, disposes the container and
    /// everything it created. A hosted <see cref="Features"/> leaves the host's container to the host.
    /// </summary>
    /// <returns>A task that completes when everything is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        _activityListener.Dispose();
        if (_ownProvider?.IsValueCreated == true)
            await _ownProvider.Value.DisposeAsync();
    }

    // StartActivity returns null unless something listens, so listen ourselves to guarantee every
    // invocation has an id. AllData without Recorded leaves export decisions to other listeners,
    // e.g. OpenTelemetry's sampler.
    private static ActivityListener ListenToInvocations()
    {
        var listener = new ActivityListener
        {
            // By name, not by reference: comparing with the static field can trigger its initializer,
            // whose constructor asks this listener about the new source while the field is still null.
            ShouldListenTo = source => source.Name == ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private FeaturesBuilder EnsureRegistrationOpen()
    {
        if (_builder is null)
            throw new InvalidOperationException("this Features is hosted: register features with AddFeatures instead");
        if (_ownProvider!.IsValueCreated)
            throw new InvalidOperationException("registration is closed once Features is built, by Build or the first invocation");
        return _builder;
    }

    // The feature a link names.
    internal static Type FeatureTypeOf(Link link) =>
        GenericArgumentsOf(link.GetType(), typeof(Link<,>))?[0]
        ?? throw new ArgumentException($"{link.GetType().Name} does not derive from Link<,>");

    // The type arguments of the given generic base class, or null if type doesn't derive from it.
    internal static Type[]? GenericArgumentsOf(Type type, Type genericBase)
    {
        for (var t = type; t != null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == genericBase)
                return t.GetGenericArguments();

        return null;
    }
}
