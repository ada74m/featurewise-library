using Microsoft.AspNetCore.Authorization;

namespace Featurewise;

/// <summary>
/// An authorization handler that lets system callers invoke links of type <typeparamref name="TArgs"/>,
/// and no one else: not even a signed-in user.
/// </summary>
/// <typeparam name="TArgs">The arguments type of the feature to open to system callers.</typeparam>
/// <remarks>
/// <para>
/// Register a feature with it when only the service's own jobs and workers should invoke the feature,
/// as with a <see cref="Worker{TArgs}"/>:
/// <code>
/// features.RegisterFeature&lt;HandleRemindersDue, AnySystemCaller&lt;HandleRemindersDue.Args&gt;&gt;();
/// </code>
/// </para>
/// <para>
/// A system caller is one made by <see cref="SystemCaller.For"/>, which only code running in the process
/// can create. Each registration applies only to links of its own type, as with
/// <see cref="AnySignedInCaller{TArgs}"/>.
/// </para>
/// </remarks>
public sealed class AnySystemCaller<TArgs> : AuthorizationHandler<InvokeRequirement, TArgs> where TArgs : Link
{
    /// <summary>Approves the link if the caller is a system caller.</summary>
    /// <param name="context">The authorization context, which holds the caller.</param>
    /// <param name="requirement">The requirement to invoke the link.</param>
    /// <param name="link">The link being invoked.</param>
    /// <returns>A completed task.</returns>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InvokeRequirement requirement, TArgs link)
    {
        if (SystemCaller.IsSystem(context.User))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
