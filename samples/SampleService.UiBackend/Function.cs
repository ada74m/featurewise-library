using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Featurewise;
using Microsoft.Extensions.DependencyInjection;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace SampleService.UiBackend;

// The backend for the service's own UI: invokes its queries and commands over API Gateway's HTTP API,
// each as POST /{feature} with its arguments as the JSON body, so the UI follows a link the same way
// whatever it leads to. Its contract is the features themselves, links and result cases included,
// which suits a UI shipped with the service and no one else.
public class Function
{
    private readonly Features _features = new();
    private readonly HashSet<string> _offered = [];
    private readonly JsonSerializerOptions _json;

    public Function() : this(Registrations.Aws)
    {
    }

    public Function(Action<IServiceCollection> registerDependencies)
    {
        _features
            .RegisterFeature<EmployeeDetails, AnySignedInCaller<EmployeeDetails.Args>>()
            .RegisterFeature<ReviewsDue, AnySignedInCaller<ReviewsDue.Args>>()
            .RegisterFeature<Hire, Hire.Rule>()
            .RegisterFeature<Fire, Fire.Rule>();
        Offer<EmployeeDetails>();
        Offer<ReviewsDue>();
        Offer<Hire>();
        Offer<Fire>();

        _json = _features.ConfigureJson(new JsonSerializerOptions());

        _features
            .RegisterDependencies(services => services.AddSingleton(_json))
            .RegisterDependencies(registerDependencies)
            .Build();
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var feature = ValueOf(request.PathParameters, "feature") ?? "";
        if (!_offered.Contains(feature))
            return Problem(HttpStatusCode.NotFound, $"there is no feature named {feature}");

        // HTTP APIs give header names in lower case.
        var idempotencyKey = ValueOf(request.Headers, "idempotency-key");
        if (idempotencyKey is not null && string.IsNullOrWhiteSpace(idempotencyKey))
            return Problem(HttpStatusCode.BadRequest, "the Idempotency-Key header is blank");

        Link link;
        try
        {
            link = ReadLink(feature, BodyArguments(request));
        }
        catch (JsonException e)
        {
            return Problem(HttpStatusCode.BadRequest, e.Message);
        }

        using var timeout = new CancellationTokenSource(context.RemainingTime - TimeSpan.FromSeconds(1));
        try
        {
            var result = await _features.Invoke(link, JwtCaller.From(request), idempotencyKey, timeout.Token);
            return Response(HttpStatusCode.OK, JsonSerializer.Serialize(result, _json));
        }
        catch (InvocationDeniedException denied)
        {
            return denied.CallerAuthenticated
                ? Problem(HttpStatusCode.Forbidden, $"you may not invoke {feature}")
                : Problem(HttpStatusCode.Unauthorized, $"sign in to invoke {feature}");
        }
    }

    // Offers a registered feature to the UI. Links to other features, such as those registered only to
    // be queued, aren't invoked here.
    private void Offer<TFeature>() =>
        _offered.Add(typeof(TFeature).GetCustomAttribute<FeatureNameAttribute>()?.Name ?? typeof(TFeature).Name);

    // The path names the feature. Arguments may name it too, but only as the same feature.
    private Link ReadLink(string feature, JsonObject arguments)
    {
        if (arguments.TryGetPropertyValue(Features.FeatureNameProperty, out var named)
            && !(named is JsonValue value && value.TryGetValue<string>(out var name) && name == feature))
            throw new JsonException($"the arguments name a feature other than {feature}");

        arguments[Features.FeatureNameProperty] = feature;
        return arguments.Deserialize<Link>(_json) ?? throw new JsonException("the arguments are null");
    }

    private static JsonObject BodyArguments(APIGatewayHttpApiV2ProxyRequest request)
    {
        var body = request.IsBase64Encoded && request.Body is not null
            ? Encoding.UTF8.GetString(Convert.FromBase64String(request.Body))
            : request.Body;
        if (string.IsNullOrWhiteSpace(body))
            return new JsonObject();

        return JsonNode.Parse(body) as JsonObject ?? throw new JsonException("the body must be a JSON object");
    }

    private static string? ValueOf(IDictionary<string, string>? values, string key) =>
        values is not null && values.TryGetValue(key, out var value) ? value : null;

    private static APIGatewayHttpApiV2ProxyResponse Problem(
        HttpStatusCode status, string message, params (string Name, string Value)[] headers) =>
        Response(status, JsonSerializer.Serialize(new { error = message }), headers);

    private static APIGatewayHttpApiV2ProxyResponse Response(
        HttpStatusCode status, string json, params (string Name, string Value)[] headers) =>
        new()
        {
            StatusCode = (int)status,
            Body = json,
            Headers = headers.Append(("content-type", "application/json")).ToDictionary(h => h.Item1, h => h.Item2)
        };
}
