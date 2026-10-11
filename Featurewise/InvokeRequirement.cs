using Microsoft.AspNetCore.Authorization;

namespace Featurewise;

/// <summary>
/// The requirement that the caller may invoke a link. Say who may invoke links to a feature with a rule:
/// an authorization handler for this requirement, with the feature's arguments type as the resource.
/// </summary>
/// <remarks>
/// <para>
/// A link is refused unless one of its feature's rules approves it. A feature is registered with its
/// rules, by <c>FeaturesBuilder.RegisterFeature</c>, so every feature has at
/// least one. Write a rule nested in its feature, as in the example, or use a ready-made one:
/// <see cref="AnyCaller{TArgs}"/>, <see cref="AnySignedInCaller{TArgs}"/> or <see cref="AnySystemCaller{TArgs}"/>.
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
/// public class Fire(...) : Feature&lt;Fire, Fire.Args, Fire.Result&gt;
/// {
///     ...
///
///     // Managers may fire anyone but themselves.
///     public class Rule : AuthorizationHandler&lt;InvokeRequirement, Args&gt;
///     {
///         protected override Task HandleRequirementAsync(
///             AuthorizationHandlerContext context, InvokeRequirement requirement, Args link)
///         {
///             if (context.User.IsInRole("manager") &amp;&amp; link.EmployeeId != context.User.Identity?.Name)
///                 context.Succeed(requirement);
///             return Task.CompletedTask;
///         }
///     }
/// }
///
/// features.RegisterFeature&lt;Fire, Fire.Rule&gt;();
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
