namespace Featurewise;

/// <summary>
/// A feature that changes state with nobody waiting for the result, such as one that handles an event
/// from a queue or runs on a schedule. By convention, its name is <c>Handle</c> followed by what
/// triggers it.
/// </summary>
/// <typeparam name="TFeature">
/// The deriving class itself. The compiler can't enforce this, so
/// <c>Features.RegisterFeature</c> rejects a feature that names any other class here.
/// </typeparam>
/// <typeparam name="TArgs">
/// The feature's arguments, which must be a link to this feature. By convention, a record named
/// <c>Args</c> nested in the feature.
/// </typeparam>
/// <remarks>
/// <para>
/// A worker's result is always <see cref="Nil"/>, since nothing reads it. Its invocation is still logged
/// as succeeding or failing; anything more about what it did, such as how many records it archived or
/// that a message named an unknown employee, goes in a log line of its own.
/// </para>
/// <para>
/// A feature that someone might wait for is a <see cref="Command{TFeature, TArgs, TResult}"/>, even when
/// a job sometimes invokes it. A worker is usually registered with
/// <see cref="AnySystemCaller{TArgs}"/>, so that only the service's own jobs and queues invoke it. See
/// <see cref="Feature{TFeature, TArgs, TResult}"/> for what every feature shares.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class HandleEmployeeLeft(IPayroll payroll, ILogger&lt;HandleEmployeeLeft&gt; logger) : Worker&lt;HandleEmployeeLeft, HandleEmployeeLeft.Args&gt;
/// {
///     public record Args(string EmployeeId) : Link&lt;HandleEmployeeLeft, Nil&gt;;
///
///     public override async Task&lt;Nil&gt; Invoke(Args args, CancellationToken cancellationToken)
///     {
///         var closed = await payroll.Close(args.EmployeeId, cancellationToken);
///         logger.LogInformation("Closed {Count} payroll records for {EmployeeId}", closed, args.EmployeeId);
///         return Nil.Value;
///     }
/// }
/// </code>
/// </example>
public abstract class Worker<TFeature, TArgs> : Feature<TFeature, TArgs, Nil>
    where TFeature : Worker<TFeature, TArgs>
    where TArgs : Link<TFeature, Nil>;
