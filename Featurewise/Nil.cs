using System.Text.Json.Serialization;

namespace Featurewise;

/// <summary>
/// The result of a feature that has nothing to return, such as an event handler or another
/// asynchronous worker, whose caller doesn't wait for an answer.
/// </summary>
/// <remarks>
/// <para>
/// Every feature produces a result, so a feature with nothing to say returns <see cref="Value"/>.
/// Its invocation logs and traces its outcome as <c>Nil</c>.
/// </para>
/// <para>
/// A worker that can fail in an expected way, or that recognises a redelivered message, returns a
/// result with cases instead, so that its logs and traces say what happened.
/// </para>
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
