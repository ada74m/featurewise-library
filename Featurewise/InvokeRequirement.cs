using Microsoft.AspNetCore.Authorization;

namespace Featurewise;

/// <summary>
/// The requirement that the caller may invoke a link. Say who may invoke links to a feature by writing
/// an authorization handler for this requirement, with the feature's arguments type as the resource.
/// </summary>
/// <remarks>
/// <para>
/// A link is refused unless a handler approves it, so a feature with no handler can't be invoked,
/// unless the feature has <see cref="AllowAnonymousAttribute"/>. Register handlers with
/// <see cref="FeaturesBuilder.RegisterAuthorizationHandler{THandler}"/>.
/// </para>
/// <para>
/// A handler runs in the invocation's scope, so it can take the same dependencies as the feature, such
/// as a store to load a record the link's arguments identify. It can take <see cref="Invocation"/> to
/// pass the invocation's cancellation token on to them. If a
/// handler throws, the invocation fails, and is logged and traced as failed, as if the feature had thrown.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Managers may fire anyone but themselves.
/// public class FireAuthorization : AuthorizationHandler&lt;InvokeRequirement, Fire.Args&gt;
/// {
///     protected override Task HandleRequirementAsync(
///         AuthorizationHandlerContext context, InvokeRequirement requirement, Fire.Args link)
///     {
///         if (context.User.IsInRole("manager") &amp;&amp; link.EmployeeId != context.User.Identity?.Name)
///             context.Succeed(requirement);
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public sealed class InvokeRequirement : IAuthorizationRequirement
{
    private InvokeRequirement()
    {
    }

    /// <summary>The requirement's only instance.</summary>
    public static InvokeRequirement Instance { get; } = new();
}
