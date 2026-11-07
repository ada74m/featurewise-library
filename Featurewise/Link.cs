using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Featurewise;

/// <summary>
/// A potential invocation of a feature: it names the feature it would invoke and carries the
/// arguments to invoke it with. Pass it to
/// <c>Features.Invoke</c> to invoke it.
/// </summary>
/// <remarks>
/// <para>
/// Every link derives from <see cref="Link{TFeature, TResult}"/>, which names the feature. Declare a
/// link as this type to handle links whose feature and result aren't known in advance, such as links
/// read from JSON by a general-purpose endpoint, and as <see cref="Link{TResult}"/> when the result is known.
/// </para>
/// <para>
/// Links are records, so they compare by value and can be serialized, stored, or sent to a client and
/// invoked later. Serialize them with options set up by <c>Features.ConfigureJson</c>.
/// </para>
/// <para>
/// A link can outlive the code that made it: a client holds it across a deployment, a queue holds it
/// through one, and old and new versions of a function can run side by side. So change a feature's
/// arguments compatibly. Adding a nullable or defaulted argument is safe. Removing, renaming or
/// requiring one takes two releases: the first accepts links of both shapes, and the second, once old
/// links are gone, drops the old shape. A link missing an argument its feature requires fails to
/// deserialize rather than reaching the feature.
/// </para>
/// </remarks>
public abstract record Link : IJsonOnDeserialized
{
    private string? _causedBy = Activity.Current?.Id;

    // Distinguishes a cause assigned explicitly, including by a deserializer, from the one stamped
    // when the link was constructed.
    private bool _causedByAssigned;

    // Only the framework's own link types derive from this, so every link names its feature.
    private protected Link()
    {
    }

    /// <summary>
    /// The operation that created this link, as a W3C traceparent, or <see langword="null"/> if the
    /// link was created outside any operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set from <see cref="Activity.Current"/> when the link is constructed, so a link that a feature
    /// creates records that feature's invocation. When the link is invoked, <c>Features</c> links
    /// the new invocation's activity to this operation.
    /// </para>
    /// <para>
    /// A <c>with</c> expression copies the value from the original link rather than restamping it.
    /// </para>
    /// <para>
    /// A deserialized link takes its value only from its JSON: if the JSON has none, neither does the link.
    /// </para>
    /// <para>
    /// The value is diagnostic only: a link that leaves the process can come back with any value.
    /// </para>
    /// </remarks>
    public string? CausedBy
    {
        get => _causedBy;
        init
        {
            _causedBy = value;
            _causedByAssigned = true;
        }
    }

    /// <summary>
    /// Compares links by type and arguments, ignoring <see cref="CausedBy"/>, which records where a
    /// link came from rather than what it would invoke.
    /// </summary>
    /// <param name="other">The link to compare with.</param>
    /// <returns><see langword="true"/> if both links would invoke the same feature with the same arguments.</returns>
    public virtual bool Equals(Link? other) => other is not null && EqualityContract == other.EqualityContract;

    /// <summary>Returns a hash code that, like <see cref="Equals(Link)"/>, ignores <see cref="CausedBy"/>.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => EqualityContract.GetHashCode();

    // Constructing the link during deserialization stamped it with whatever operation is reading it.
    void IJsonOnDeserialized.OnDeserialized()
    {
        if (!_causedByAssigned)
            _causedBy = null;
    }
}

/// <summary>
/// A link whose invocation produces <typeparamref name="TResult"/>. Pass it to
/// <c>Features.Invoke</c>
/// to invoke it and get a typed result.
/// </summary>
/// <typeparam name="TResult">The type that invoking the link produces.</typeparam>
/// <remarks>
/// Every link derives from <see cref="Link{TFeature, TResult}"/>, which names the feature. Declare a
/// link as this type to handle links to any feature that produces <typeparamref name="TResult"/>.
/// </remarks>
public abstract record Link<TResult> : Link
{
    // Only Link<TFeature, TResult> derives from this, so every link names its feature.
    private protected Link()
    {
    }
}

/// <summary>
/// A link to the feature <typeparamref name="TFeature"/>. Each feature's arguments type derives from
/// this; see <see cref="Command{TArgs, TResult}"/> for an example.
/// </summary>
/// <typeparam name="TFeature">The feature the link would invoke.</typeparam>
/// <typeparam name="TResult">The type that invoking the link produces.</typeparam>
/// <remarks>
/// This is a base record rather than an interface so that a link type can name only one feature: a
/// type can implement an interface several times over, but it has only one base class.
/// </remarks>
public abstract record Link<TFeature, TResult> : Link<TResult>
    where TFeature : IFeature<TResult>;
