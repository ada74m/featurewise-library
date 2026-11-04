using System.Text.Json.Serialization;

namespace Featurewise;

/// <summary>
/// The result of a feature that has nothing to return, such as every <see cref="Worker{TFeature, TArgs}"/>,
/// whose caller doesn't wait for an answer.
/// </summary>
/// <remarks>
/// Every feature produces a result, so a feature with nothing to say returns <see cref="Value"/>.
/// Its invocation logs and traces its outcome as <c>Nil</c>. A worker that wants its logs to say more,
/// such as that it recognised a redelivered message, logs that itself.
/// </remarks>
public sealed record Nil
{
    /// <summary>The only value of <see cref="Nil"/>.</summary>
    public static Nil Value { get; } = new();

    [JsonConstructor]
    private Nil()
    {
    }
}
