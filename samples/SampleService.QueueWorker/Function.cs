using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Featurewise;
using Microsoft.Extensions.DependencyInjection;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace SampleService.QueueWorker;

// Invokes what arrives on the service's queue: links the service sent itself, and events from other
// services' SNS topics, which a topic adapter turns into links.
public class Function
{
    private readonly Features _features = new();
    private readonly JsonSerializerOptions _json;
    private readonly Dictionary<string, ITopicAdapter> _adaptersByTopic;

    // The adapters this worker has, by the name TOPIC_ADAPTERS gives them.
    private static readonly Dictionary<string, ITopicAdapter> Adapters = new()
    {
        ["directory"] = new DirectoryEventsAdapter()
    };

    public Function() : this(SampleServiceDependencies.RegisterAws, Environment.GetEnvironmentVariable("TOPIC_ADAPTERS") ?? "")
    {
    }

    // topicAdapters maps topics to adapters, as "<topic arn>=<adapter>;<topic arn>=<adapter>".
    public Function(Action<IServiceCollection> registerDependencies, string topicAdapters)
    {
        _adaptersByTopic = topicAdapters
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => Adapters[pair[1]]);

        _features
            .RegisterFeature<HandleEmployeeLeft>()
            .RegisterFeature<HandleAccountDeactivated>()
            .RegisterAuthorizationHandler<AnySystemCaller<HandleEmployeeLeft.Args>>()
            .RegisterAuthorizationHandler<AnySystemCaller<HandleAccountDeactivated.Args>>();

        _json = _features.ConfigureJson(new JsonSerializerOptions());

        _features
            .RegisterDependencies(services => services.AddSingleton(_json))
            .RegisterDependencies(registerDependencies)
            .Build();
    }

    // Reports each message that fails, so SQS retries only those and, after enough tries, moves them to
    // the dead-letter queue. The event source mapping must have ReportBatchItemFailures turned on.
    public async Task<SQSBatchResponse> Handle(SQSEvent sqsEvent, ILambdaContext context)
    {
        var failures = new List<SQSBatchResponse.BatchItemFailure>();
        foreach (var message in sqsEvent.Records)
        {
            try
            {
                await Process(message, context);
            }
            catch (Exception e)
            {
                // Features logs failures inside an invocation; this also covers messages that never got
                // that far, such as unreadable ones.
                context.Logger.LogError($"Message {message.MessageId} failed: {e.Message}");
                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = message.MessageId });
            }
        }

        return new SQSBatchResponse(failures);
    }

    private async Task Process(SQSEvent.SQSMessage message, ILambdaContext context)
    {
        using var timeout = new CancellationTokenSource(context.RemainingTime - TimeSpan.FromSeconds(1));

        if (SnsNotification.TryRead(message.Body) is { } notification)
        {
            if (!_adaptersByTopic.TryGetValue(notification.TopicArn, out var adapter))
                throw new InvalidOperationException($"no adapter handles events from {notification.TopicArn}");

            // An event this service doesn't act on is done with.
            if (adapter.Adapt(notification.Message) is not { } adapted)
                return;

            // SNS gives the event one id however many queues it reaches and however often each delivers it.
            await _features.Invoke(adapted, SystemCaller.For(adapter.Source), notification.MessageId, timeout.Token);
            return;
        }

        var link = JsonSerializer.Deserialize<Link>(message.Body, _json) ?? throw new JsonException("the message is null");
        await _features.Invoke(link, SystemCaller.For("link-queue"), message.MessageId, timeout.Token);
    }
}
