using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SampleService.Tests;

// Stand-ins for AWS and the clock, registered in place of Registrations.Aws, which
// tests seed and inspect.
public class Fakes
{
    public static readonly DateOnly Today = new(2026, 10, 8);

    public InMemoryEmployeeStore Employees { get; } = new();

    // The JSON of each link sent to the queue, as SQS would receive it.
    public List<string> QueuedMessages { get; } = [];

    public CapturingNotifier Notifier { get; } = new();

    public void Register(IServiceCollection services)
    {
        Registrations.Domain(services);
        services.AddSingleton<IEmployeeStore>(Employees);
        services.AddSingleton<IWorkQueue>(provider =>
            new CapturingWorkQueue(provider.GetRequiredService<JsonSerializerOptions>(), QueuedMessages));
        services.AddSingleton<INotifier>(Notifier);
        services.AddSingleton<TimeProvider>(new FixedTime(Today));
    }

    // Seeds the store with Staff.
    public Fakes WithStaff()
    {
        foreach (var employee in Staff.All)
            Employees.Add(employee);
        return this;
    }
}
