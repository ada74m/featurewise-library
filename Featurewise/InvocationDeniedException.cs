namespace Featurewise;

/// <summary>
/// Thrown by <see cref="Features"/> when the caller may not invoke a link.
/// </summary>
/// <param name="feature">The feature the link names.</param>
/// <param name="callerAuthenticated">Whether the caller was authenticated.</param>
/// <remarks>
/// Entrypoints map this to their own convention. An HTTP entrypoint would return 401 Unauthorized if
/// <see cref="CallerAuthenticated"/> is <see langword="false"/>, inviting the caller to sign in, and
/// 403 Forbidden otherwise.
/// </remarks>
public sealed class InvocationDeniedException(string feature, bool callerAuthenticated)
    : Exception($"the caller may not invoke {feature}")
{
    /// <summary>The feature the link names.</summary>
    public string Feature { get; } = feature;

    /// <summary>Whether the caller was authenticated.</summary>
    public bool CallerAuthenticated { get; } = callerAuthenticated;
}
