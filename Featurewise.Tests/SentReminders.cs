using System.Collections.Concurrent;

namespace Featurewise.Tests;

// What Remind has sent, by idempotency key, standing in for a table a real feature would keep.
public class SentReminders
{
    public ConcurrentDictionary<string, string> ByKey { get; } = new();
}
