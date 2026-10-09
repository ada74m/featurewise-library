using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Featurewise;
using Microsoft.Extensions.DependencyInjection;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace SampleService.PublicApi;

// The service's API for other organisations' systems, on API Gateway's HTTP API. Unlike the UI backend,
// its contract is its own, described in openapi.yaml: resources under a version, HTTP status codes for
// outcomes, and public shapes that leave out links, result cases and internal fields. Each route builds
// a link, invokes it, and turns the result into a response.
public class Function
{
    private delegate Task<APIGatewayHttpApiV2ProxyResponse> Route(
        APIGatewayHttpApiV2ProxyRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Features _features = new();
    private readonly Dictionary<string, Route> _routes;

    public Function() : this(Registrations.Aws)
    {
    }

    public Function(Action<IServiceCollection> registerDependencies)
    {
        _routes = new Dictionary<string, Route>
        {
            ["GET /v1/employees/{employeeId}"] = GetEmployee
        };

        _features
            .RegisterFeature<EmployeeDetails, EmployeesReadRule>()
            .RegisterDependencies(registerDependencies)
            .Build();
    }

    // The routes this function handles, as API Gateway names them: the operations in openapi.yaml.
    public IReadOnlyCollection<string> Routes => _routes.Keys;

    public async Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        if (request.RouteKey is null || !_routes.TryGetValue(request.RouteKey, out var route))
            return Problem(HttpStatusCode.NotFound, "Not found", $"There is no route {request.RouteKey}.");

        using var timeout = new CancellationTokenSource(context.RemainingTime - TimeSpan.FromSeconds(1));
        try
        {
            return await route(request, ClientCaller.From(request), timeout.Token);
        }
        catch (InvocationDeniedException denied)
        {
            return denied.CallerAuthenticated
                ? Problem(HttpStatusCode.Forbidden, "Forbidden", "The client's token doesn't grant this operation.")
                : Problem(HttpStatusCode.Unauthorized, "Unauthorized", "The request needs a client's access token.");
        }
    }

    private async Task<APIGatewayHttpApiV2ProxyResponse> GetEmployee(
        APIGatewayHttpApiV2ProxyRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var employeeId = request.PathParameters["employeeId"];
        return await _features.Invoke(new EmployeeDetails.Args(employeeId), caller, cancellationToken) switch
        {
            EmployeeDetails.Result.Found found => Response(HttpStatusCode.OK, EmployeeView.From(found.Employee)),
            EmployeeDetails.Result.NotFound => Problem(HttpStatusCode.NotFound, "Employee not found", $"There is no employee {employeeId}."),
            var other => throw new UnreachableException($"EmployeeDetails has no case {other.GetType().Name}")
        };
    }

    private static APIGatewayHttpApiV2ProxyResponse Response<T>(HttpStatusCode status, T body) =>
        new()
        {
            StatusCode = (int)status,
            Body = JsonSerializer.Serialize(body, Json),
            Headers = new Dictionary<string, string> { ["content-type"] = "application/json" }
        };

    private static APIGatewayHttpApiV2ProxyResponse Problem(HttpStatusCode status, string title, string detail) =>
        new()
        {
            StatusCode = (int)status,
            Body = JsonSerializer.Serialize(new Problem(title, (int)status, detail), Json),
            Headers = new Dictionary<string, string> { ["content-type"] = "application/problem+json" }
        };
}
