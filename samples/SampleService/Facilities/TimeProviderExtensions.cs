namespace SampleService;

public static class TimeProviderExtensions
{
    // Today's date in UTC, which dates such as reviews and departures are kept in.
    public static DateOnly Today(this TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}
