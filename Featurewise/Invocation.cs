using System.Security.Claims;

namespace Featurewise;

/// <summary>
/// What is known about the current invocation beyond its link's arguments: who invoked it, its
/// cancellation token, the request it serves, and its place in a trace. A feature or authorization
/// handler takes this as a dependency.
/// </summary>
/// <remarks>
/// <para>
/// There is one per invocation, set by <see cref="Features"/> before any authorization handler or the
/// feature runs. Outside an invocation, such as in a host's own scopes, <see cref="Caller"/> throws and
/// the other properties are empty.
/// </para>
/// <para>
/// This is a type of its own rather than the caller's <see cref="ClaimsPrincipal"/> so that, in a host's
/// container, it can't be confused with a <see cref="ClaimsPrincipal"/> the host registers for its own purposes.
/// </para>
/// </remarks>
public sealed class Invocation
{
    private ClaimsPrincipal? _caller;

    internal Invocation()
    {
    }

    /// <summary>Who invoked the link, as passed to <see cref="Features"/>.</summary>
    /// <exception cref="InvalidOperationException">This isn't the scope of an invocation.</exception>
    public ClaimsPrincipal Caller =>
        _caller ?? throw new InvalidOperationException("the caller is only available during an invocation");

    /// <summary>
    /// Cancels the invocation. A feature receives it as an argument too; a service that isn't passed it,
    /// such as an authorization handler, takes it from here to pass on, for example to a query.
    /// </summary>
    public CancellationToken CancellationToken { get; private set; }

    /// <summary>
    /// The idempotency key the entrypoint gave, or <see langword="null"/> if it gave none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Entrypoints deliver some requests more than once: a queue delivers each message at least once,
    /// and clients retry requests that timed out. The key identifies the request rather than the link,
    /// so a feature can tell a redelivery from a new request for the same thing, which its arguments
    /// alone can't. What the feature does with it is up to the feature: record it to skip redeliveries,
    /// pass it to an API that accepts one, or ignore it.
    /// </para>
    /// <para>
    /// The entrypoint chooses the key, such as a queue message's id or an HTTP request's
    /// <c>Idempotency-Key</c> header, and passes it to
    /// <see cref="Features.Invoke(Link, ClaimsPrincipal, string?, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    public string? IdempotencyKey { get; private set; }

    /// <summary>
    /// The invocation's id: its activity's id, as a W3C traceparent. Links the feature creates record it
    /// as their <see cref="Link.CausedBy"/>.
    /// </summary>
    public string? Id { get; private set; }

    /// <summary>
    /// What caused the invocation: the <see cref="Link.CausedBy"/> of the link being invoked, or
    /// <see langword="null"/> if the link was created outside any operation.
    /// </summary>
    public string? CausedBy { get; private set; }

    internal void Begin(
        ClaimsPrincipal caller, CancellationToken cancellationToken, string? idempotencyKey, string? id, string? causedBy)
    {
        _caller = caller;
        CancellationToken = cancellationToken;
        IdempotencyKey = idempotencyKey;
        Id = id;
        CausedBy = causedBy;
    }
}
