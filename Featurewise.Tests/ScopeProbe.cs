namespace Featurewise.Tests;

// A scoped dependency, so tests can see which scope a feature ran in.
public class ScopeProbe : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}
