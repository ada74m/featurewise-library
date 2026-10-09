using System.Security.Claims;

namespace Featurewise;

/// <summary>
/// Identities for code that invokes links on its own behalf, such as scheduled jobs and queue
/// consumers, rather than on a user's.
/// </summary>
/// <remarks>
/// <para>
/// A system caller is signed in and named after its job, so its invocations can be told apart in logs
/// and traces. It gets no special treatment: like any caller, it may invoke a link only if a rule
/// approves it. A rule can recognise it with <see cref="IsSystem"/>, and
/// <see cref="AnySystemCaller{TArgs}"/> opens a feature to system callers alone.
/// </para>
/// <para>
/// Only code running in the process can create a system caller. A caller from outside, such as an HTTP
/// request's user, carries the authentication type its authentication scheme gave it, so it isn't
/// mistaken for a system caller unless the app names a scheme <see cref="AuthenticationType"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await features.Invoke(new SendReminders.Args(), SystemCaller.For("nightly-reminders"), stoppingToken);
/// </code>
/// </example>
public static class SystemCaller
{
    /// <summary>The authentication type of a system caller's identity.</summary>
    public const string AuthenticationType = "Featurewise.System";

    /// <summary>Creates a signed-in caller for the job named <paramref name="job"/>.</summary>
    /// <param name="job">The job's name, which becomes the caller's name.</param>
    /// <returns>The caller.</returns>
    /// <exception cref="ArgumentException"><paramref name="job"/> is null, empty or white space.</exception>
    public static ClaimsPrincipal For(string job)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(job);
        return new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, job)], AuthenticationType));
    }

    /// <summary>Says whether <paramref name="caller"/> was created by <see cref="For"/>.</summary>
    /// <param name="caller">The caller to check, such as <c>context.User</c> in an authorization handler.</param>
    /// <returns><see langword="true"/> if the caller is a system caller.</returns>
    public static bool IsSystem(ClaimsPrincipal caller) =>
        caller.Identities.Any(identity => identity.AuthenticationType == AuthenticationType && identity.IsAuthenticated);
}
