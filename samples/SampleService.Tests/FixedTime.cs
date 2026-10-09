namespace SampleService.Tests;

// A clock that always says it's midday on the given day.
public class FixedTime(DateOnly today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
}
