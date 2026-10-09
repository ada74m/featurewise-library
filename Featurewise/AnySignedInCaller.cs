using Microsoft.AspNetCore.Authorization;

namespace Featurewise;

/// <summary>
/// An authorization handler that lets any signed-in caller invoke links of type
/// <typeparamref name="TArgs"/>, and no anonymous caller.
/// </summary>
/// <typeparam name="TArgs">The arguments type of the feature to open to signed-in callers.</typeparam>
/// <remarks>
/// <para>
/// Register a feature with it when the feature needs no rule beyond "signed in":
/// <code>
/// features.RegisterFeature&lt;EmployeeReview, AnySignedInCaller&lt;EmployeeReview.Args&gt;&gt;();
/// </code>
/// </para>
/// <para>
/// Each closed type applies only to links of its own type. A handler that approves a base link type,
/// such as <see cref="Link"/>, would approve every feature's links, because any rule that approves a
/// link is enough to allow it.
/// </para>
/// <para>
/// To let anyone invoke a feature, including callers who aren't signed in, use
/// <see cref="AnyCaller{TArgs}"/> instead.
/// </para>
/// </remarks>
public sealed class AnySignedInCaller<TArgs> : AuthorizationHandler<InvokeRequirement, TArgs> where TArgs : Link
{
    /// <summary>Approves the link if the caller is signed in.</summary>
    /// <param name="context">The authorization context, which holds the caller.</param>
    /// <param name="requirement">The requirement to invoke the link.</param>
    /// <param name="link">The link being invoked.</param>
    /// <returns>A completed task.</returns>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InvokeRequirement requirement, TArgs link)
    {
        if (context.User.Identity?.IsAuthenticated == true)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
