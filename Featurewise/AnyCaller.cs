using Microsoft.AspNetCore.Authorization;

namespace Featurewise;

/// <summary>
/// An authorization handler that lets any caller invoke links of type <typeparamref name="TArgs"/>,
/// signed in or not.
/// </summary>
/// <typeparam name="TArgs">The arguments type of the feature to open to everyone.</typeparam>
/// <remarks>
/// <para>
/// Register a feature with it when anyone may invoke the feature, such as a public query:
/// <code>
/// features.RegisterFeature&lt;OpeningHours, AnyCaller&lt;OpeningHours.Args&gt;&gt;();
/// </code>
/// </para>
/// <para>
/// Featurewise doesn't honour <see cref="AllowAnonymousAttribute"/> on a feature; this takes its place,
/// so that every feature says who may invoke it in the same way. Each closed type applies only to links
/// of its own type, as with <see cref="AnySignedInCaller{TArgs}"/>.
/// </para>
/// </remarks>
public sealed class AnyCaller<TArgs> : AuthorizationHandler<InvokeRequirement, TArgs> where TArgs : Link
{
    /// <summary>Approves the link, whoever the caller is.</summary>
    /// <param name="context">The authorization context, which holds the caller.</param>
    /// <param name="requirement">The requirement to invoke the link.</param>
    /// <param name="link">The link being invoked.</param>
    /// <returns>A completed task.</returns>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InvokeRequirement requirement, TArgs link)
    {
        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
