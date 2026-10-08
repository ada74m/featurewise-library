using Microsoft.AspNetCore.Authorization;

namespace Featurewise;

/// <summary>
/// An authorization handler that lets any signed-in caller invoke links of type
/// <typeparamref name="TArgs"/>, and no anonymous caller.
/// </summary>
/// <typeparam name="TArgs">The arguments type of the feature to open to signed-in callers.</typeparam>
/// <remarks>
/// <para>
/// Register one per feature that needs no rule beyond "signed in", so that the registrations list
/// which features are open:
/// <code>
/// features
///     .RegisterFeature&lt;EmployeeReview&gt;()
///     .RegisterAuthorizationHandler&lt;AnySignedInCaller&lt;EmployeeReview.Args&gt;&gt;();
/// </code>
/// </para>
/// <para>
/// Each registration applies only to links of its own type. A handler that approves a base link type,
/// such as <see cref="Link"/>, would approve every feature's links, because any handler that approves
/// a link is enough to allow it.
/// </para>
/// <para>
/// To let anyone invoke a feature, including callers who aren't signed in, put
/// <see cref="AllowAnonymousAttribute"/> on the feature instead.
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
