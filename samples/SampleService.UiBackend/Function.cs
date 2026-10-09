using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Featurewise;
using Microsoft.Extensions.DependencyInjection;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace SampleService.UiBackend;

// The backend for the service's own UI: invokes its queries and commands over API Gateway's HTTP API,
// GET /{feature} for a query, with its arguments in the query string, and POST /{feature} for a command,
// with its arguments as the JSON body. Its contract is the features themselves, links and result cases
// included, which suits a UI shipped with the service and no one else.
public class Function
{
    private readonly Features _features = new();
    private readonly Dictionary<string, string> _methodsByFeature = new();
    private readonly JsonSerializerOptions _json;
    private readonly JsonSerializerOptions _queryStringJson;

    public Function() : this(Registrations.Aws)
    {
    }

    public Function(Action<IServiceCollection> registerDependencies)
    {
        _features
            .RegisterFeature<EmployeeDetails, AnySignedInCaller<EmployeeDetails.Args>>()
            .RegisterFeature<ReviewsDue, AnySignedInCaller<ReviewsDue.Args>>()
            .RegisterFeature<Hire, Hire.Rule>()
            .RegisterFeature<Fire, Fire.Rule>()
            // Fire queues a link to HandleEmployeeLeft, which the QueueWorker invokes.
            .RegisterLinksTo<HandleEmployeeLeft>();
        Query<EmployeeDetails>();
        Query<ReviewsDue>();
        Command<Hire>();
        Command<Fire>();

        _json = _features.ConfigureJson(new JsonSerializerOptions());
        // Query strings carry only strings, so numeric arguments are read from them.
        _queryStringJson = _features.ConfigureJson(new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString });

        _features
            .RegisterDependencies(services => services.AddSingleton(_json))
            .RegisterDependencies(registerDependencies)
            .Build();
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var feature = ValueOf(request.PathParameters, "feature") ?? "";
        if (!_methodsByFeature.TryGetValue(feature, out var method))
            return Problem(HttpStatusCode.NotFound, $"there is no feature named {feature}");
        if (request.RequestContext.Http.Method != method)
            return Problem(HttpStatusCode.MethodNotAllowed, $"{feature} is invoked with {method}", ("allow", method));

        // HTTP APIs give header names in lower case.
        var idempotencyKey = ValueOf(request.Headers, "idempotency-key");
        if (idempotencyKey is not null && string.IsNullOrWhiteSpace(idempotencyKey))
            return Problem(HttpStatusCode.BadRequest, "the Idempotency-Key header is blank");

        Link link;
        try
        {
            link = method == "GET"
                ? ReadLink(feature, QueryStringArguments(request.QueryStringParameters), _queryStringJson)
                : ReadLink(feature, BodyArguments(request), _json);
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

    private void Query<TFeature>() => Offer<TFeature>("GET");

    private void Command<TFeature>() => Offer<TFeature>("POST");

    // Offers a registered feature over HTTP, by the method that invokes it.
    private void Offer<TFeature>(string method)
    {
        var name = typeof(TFeature).GetCustomAttribute<FeatureNameAttribute>()?.Name ?? typeof(TFeature).Name;
        _methodsByFeature.Add(name, method);
    }

    // The path names the feature. Arguments may name it too, but only as the same feature.
    private static Link ReadLink(string feature, JsonObject arguments, JsonSerializerOptions json)
    {
        if (arguments.TryGetPropertyValue(Features.FeatureNameProperty, out var named)
            && !(named is JsonValue value && value.TryGetValue<string>(out var name) && name == feature))
            throw new JsonException($"the arguments name a feature other than {feature}");

        arguments[Features.FeatureNameProperty] = feature;
        return arguments.Deserialize<Link>(json) ?? throw new JsonException("the arguments are null");
    }

    private static JsonObject QueryStringArguments(IDictionary<string, string>? parameters)
    {
        var arguments = new JsonObject();
        foreach (var (name, value) in parameters ?? new Dictionary<string, string>())
            arguments[name] = value;
        return arguments;
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
