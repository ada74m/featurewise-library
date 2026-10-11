namespace Featurewise;

/// <summary>
/// A feature, invoked without knowing its arguments or result types, which is how <c>Features</c>
/// invokes every feature.
/// </summary>
/// <remarks>
/// Write features by deriving from <see cref="Feature{TFeature, TArgs, TResult}"/>, which implements this interface.
/// </remarks>
public interface IFeature
{
    /// <summary>Invokes the feature with the arguments that <paramref name="link"/> carries.</summary>
    /// <param name="link">A link to this feature.</param>
    /// <param name="cancellationToken">Cancels the invocation.</param>
    /// <returns>The result of the invocation.</returns>
    /// <exception cref="InvalidCastException"><paramref name="link"/> is not a link to this feature.</exception>
    Task<object?> Invoke(Link link, CancellationToken cancellationToken);
}

/// <summary>
/// A feature that produces <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult">The type that invoking the feature produces.</typeparam>
/// <remarks>
/// <see cref="Link{TFeature, TResult}"/> requires its feature to implement this, so that a link can't
/// promise a result its feature doesn't produce. Write features by deriving from
/// <see cref="Feature{TFeature, TArgs, TResult}"/>, which implements this interface.
/// </remarks>
public interface IFeature<TResult> : IFeature;
