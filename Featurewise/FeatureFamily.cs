namespace Featurewise;

/// <summary>
/// The three families a feature belongs to one of, as its base class says.
/// </summary>
/// <remarks>
/// An entrypoint asks <c>Features.FamilyOf</c> for a feature's family, to tell its callers what kind of
/// feature answered, as a UI backend does so that its UI gives queries addresses.
/// </remarks>
public enum FeatureFamily
{
    /// <summary>A <see cref="Query{TArgs, TResult}"/>: it changes nothing, and is safe to repeat.</summary>
    Query,

    /// <summary>A <see cref="Command{TArgs, TResult}"/>: it changes state, and someone may be waiting.</summary>
    Command,

    /// <summary>A <see cref="Worker{TArgs}"/>: nobody waits for it.</summary>
    Worker
}
