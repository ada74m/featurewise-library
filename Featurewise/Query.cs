namespace Featurewise;

/// <summary>
/// A feature that answers a question and changes nothing, such as searching for employees. By
/// convention, its name is a noun phrase.
/// </summary>
/// <typeparam name="TArgs">
/// The feature's arguments, which must be a link to this feature. By convention, a record named
/// <c>Args</c> nested in the feature. <c>Features.RegisterFeature</c> rejects a feature whose arguments
/// link to another.
/// </typeparam>
/// <typeparam name="TResult">What the caller asked to see.</typeparam>
/// <remarks>
/// <para>
/// A query is safe to repeat, so entrypoints may retry it, cache it, and invoke it by GET: a UI can give
/// its link an address, and a reload invokes it again. An entrypoint asks <c>Features.IsQuery</c>
/// whether a feature is one.
/// </para>
/// <para>
/// Deriving from this is a promise that the feature changes nothing a stakeholder would care about,
/// which the compiler can't check: logging and tracing are fine, and anything more makes it a
/// <see cref="Command{TArgs, TResult}"/>. See <see cref="Feature{TArgs, TResult}"/>
/// for what every feature shares.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class EmployeeSearch(IEmployeeStore employees) : Query&lt;EmployeeSearch.Args, IReadOnlyList&lt;Employee&gt;&gt;
/// {
///     public record Args(string Name) : Link&lt;EmployeeSearch, IReadOnlyList&lt;Employee&gt;&gt;;
///
///     public override Task&lt;IReadOnlyList&lt;Employee&gt;&gt; Invoke(Args args, CancellationToken cancellationToken) =&gt;
///         employees.Search(args.Name, cancellationToken);
/// }
/// </code>
/// </example>
public abstract class Query<TArgs, TResult> : Feature<TArgs, TResult>
    where TArgs : Link<TResult>;
