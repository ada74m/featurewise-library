namespace Featurewise;

/// <summary>
/// A feature that changes state for a caller who may be waiting to know how it went, such as firing an
/// employee. By convention, its name is a verb phrase.
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
/// What the caller learns. If the outcomes differ in kind, make it a result with cases, as in the
/// example.
/// </typeparam>
/// <remarks>
/// <para>
/// Anything someone might wait for is a command, even when a job sometimes invokes it. A feature that
/// nobody ever waits for is a <see cref="Worker{TFeature, TArgs}"/>, and one that changes nothing is a
/// <see cref="Query{TFeature, TArgs, TResult}"/>.
/// </para>
/// <para>
/// Running a command twice may do its work twice, so entrypoints don't repeat one on their own: a
/// UI gives it no address that a reload could follow, and a client that retries one sends an
/// idempotency key. See <see cref="Feature{TFeature, TArgs, TResult}"/> for what every feature shares.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class Fire(ILogger&lt;Fire&gt; logger) : Command&lt;Fire, Fire.Args, Fire.Result&gt;
/// {
///     public record Args(string EmployeeId) : Link&lt;Fire, Result&gt;;
///
///     public abstract record Result
///     {
///         private Result() { }   // only the cases below can derive from it
///
///         // Unsealed, so that their JSON names their case however they're written.
///         public record Fired : Result;
///
///         public record NotFound(string EmployeeId) : Result;
///     }
///
///     // Managers may fire anyone but themselves. Registered with the feature:
///     // features.RegisterFeature&lt;Fire, Fire.Rule&gt;().
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
///
///     public override async Task&lt;Result&gt; Invoke(Args args, CancellationToken cancellationToken)
///     {
///         logger.LogInformation("Firing {EmployeeId}", args.EmployeeId);
///         ...
///         return new Result.Fired();
///     }
/// }
/// </code>
/// </example>
public abstract class Command<TFeature, TArgs, TResult> : Feature<TFeature, TArgs, TResult>
    where TFeature : Command<TFeature, TArgs, TResult>
    where TArgs : Link<TFeature, TResult>;
