namespace Featurewise;

/// <summary>
/// The base of every feature. A feature derives from one of its three families instead:
/// <see cref="Query{TFeature, TArgs, TResult}"/>, <see cref="Command{TFeature, TArgs, TResult}"/> or
/// <see cref="Worker{TFeature, TArgs}"/>.
/// </summary>
/// <typeparam name="TFeature">
/// The deriving class itself. The compiler can't enforce this, so
/// <c>Features.RegisterFeature</c> rejects a feature that names any other class here.
/// </typeparam>
/// <typeparam name="TArgs">
/// The feature's arguments, which must be a link to this feature. By convention, a record named
/// <c>Args</c> nested in the feature.
/// </typeparam>
/// <typeparam name="TResult">
/// The type that invoking the feature produces. If its outcomes differ in kind, make it a result with
/// cases, as in the example: expected failures, such as not found, are cases the feature returns, and
/// exceptions are left for unexpected ones. A feature with nothing to return produces <see cref="Nil"/>,
/// as every worker does.
/// </typeparam>
/// <remarks>
/// <para>
/// Register features with <c>Features.RegisterFeature</c>. Each invocation resolves a
/// new instance in a new dependency injection scope, so a feature takes its dependencies through its
/// constructor.
/// </para>
/// <para>
/// A feature can be invoked only by callers that one of the rules it's registered with approves: a
/// rule nested in the feature, as in the example, or a ready-made one such as
/// <see cref="AnySignedInCaller{TArgs}"/>; see <see cref="InvokeRequirement"/>. A feature can take <see cref="Invocation"/> to find out who invoked
/// it, and <see cref="LinkPermissions"/> to offer only links its caller may follow.
/// </para>
/// <para>
/// A feature may be invoked more than once for one request: queues deliver messages at least once, and
/// clients retry. Queries needn't care. A command or worker that mustn't act twice should be
/// idempotent, either through its own data, as when firing someone who has already left returns a
/// case saying so, or through the request's <see cref="Invocation.IdempotencyKey"/>.
/// </para>
/// <para>
/// A feature can't depend on or invoke another feature. To lead to another feature, return a link to
/// it; logic that several features share belongs in a service they all depend on.
/// </para>
/// </remarks>
public abstract class Feature<TFeature, TArgs, TResult> : IFeature<TResult>
    where TFeature : Feature<TFeature, TArgs, TResult>
    where TArgs : Link<TFeature, TResult>
{
    /// <summary>Does the feature's work.</summary>
    /// <param name="args">The arguments to invoke the feature with.</param>
    /// <param name="cancellationToken">
    /// Cancels the invocation. Pass it on to anything that takes one, and throw
    /// <see cref="OperationCanceledException"/> when it is cancelled.
    /// </param>
    /// <returns>The result of the invocation.</returns>
    /// <remarks>
    /// Invoke features through <c>Features</c>, which also scopes, logs and traces
    /// the invocation. Call this method directly only to test a feature in isolation.
    /// </remarks>
    public abstract Task<TResult> Invoke(TArgs args, CancellationToken cancellationToken);

    // Only Query, Command and Worker derive from this, so every feature belongs to a family.
    private protected Feature()
    {
    }

    async Task<object?> IFeature.Invoke(Link link, CancellationToken cancellationToken) =>
        await Invoke((TArgs)link, cancellationToken);
}
