using Microsoft.Extensions.Logging;

namespace Featurewise.Tests;

// Records each log entry with the scopes active when it was written.
public class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    public record Entry(string Category, LogLevel Level, string Message, Exception? Exception, List<string> Scopes)
    {
        public override string ToString() =>
            $"{Level} {Category}: {Message} [{string.Join("; ", Scopes)}]" + (Exception is null ? "" : $" {Exception.GetType().Name}: {Exception.Message}");
    }

    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public List<Entry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private class Logger(CapturingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<string>();
            provider._scopes.ForEachScope((scope, list) => list.Add(scope?.ToString() ?? ""), scopes);
            provider.Entries.Add(new Entry(category, logLevel, formatter(state, exception), exception, scopes));
        }
    }
}
