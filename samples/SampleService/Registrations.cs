using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SampleService;

// The services that features depend on, which every entrypoint registers the same way.
public static class Registrations
{
    // What the features need whatever they run against.
    public static void Domain(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<LeaverRecorder>();
    }

    // Everything, against AWS, configured by the function's environment. The entrypoint registers the
    // JsonSerializerOptions that Features set up, which the work queue writes links with.
    public static void Aws(IServiceCollection services)
    {
        Domain(services);

        // Lambda's logger writes each entry as it's logged, so nothing is lost when the function freezes.
        services.AddLogging(logging => logging.AddLambdaLogger(new LambdaLoggerOptions { IncludeScopes = true }));

        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());
        services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient());
        services.AddSingleton<IEmployeeStore>(provider =>
            new DynamoEmployeeStore(provider.GetRequiredService<IAmazonDynamoDB>(), Setting("EMPLOYEES_TABLE")));
        services.AddSingleton<IWorkQueue>(provider => new SqsWorkQueue(
            provider.GetRequiredService<IAmazonSQS>(), provider.GetRequiredService<JsonSerializerOptions>(), Setting("WORK_QUEUE_URL")));
        services.AddSingleton<INotifier, LoggingNotifier>();
    }

    private static string Setting(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"the environment variable {name} isn't set");
}
