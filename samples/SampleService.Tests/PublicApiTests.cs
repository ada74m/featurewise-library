using System.Net;
using System.Text.Json.Nodes;
using Amazon.Lambda.APIGatewayEvents;
using SampleService.PublicApi;
using YamlDotNet.RepresentationModel;

namespace SampleService.Tests;

public class PublicApiTests
{
    private const string GetEmployee = "GET /v1/employees/{employeeId}";

    private readonly Fakes _fakes = new Fakes().WithStaff();
    private readonly Function _api;

    public PublicApiTests() => _api = new Function(_fakes.Register);

    [Fact]
    public async Task AnEmployeeIsReadInThePublicShape()
    {
        var response = await Handle(Lambda.Route(GetEmployee, new() { ["employeeId"] = "e1" }, Lambda.Partner));

        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Headers["content-type"]);
        Assert.Equal("""{"id":"e1","name":"Ed","managerId":"m1","status":"active"}""", response.Body);
    }

    [Fact]
    public async Task ALeaverShowsWhenTheyLeft()
    {
        var response = await Handle(Lambda.Route(GetEmployee, new() { ["employeeId"] = "e4" }, Lambda.Partner));

        Assert.Equal("""{"id":"e4","name":"Lou","managerId":"m1","status":"left","leftOn":"2026-09-28"}""", response.Body);
    }

    [Fact]
    public async Task AMissingEmployeeIsANotFoundProblem()
    {
        var response = await Handle(Lambda.Route(GetEmployee, new() { ["employeeId"] = "x9" }, Lambda.Partner));

        Assert.Equal((int)HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Headers["content-type"]);
        var problem = JsonNode.Parse(response.Body)!;
        Assert.Equal("about:blank", (string?)problem["type"]);
        Assert.Equal(404, (int?)problem["status"]);
    }

    [Fact]
    public async Task WithoutATokenTheCallerIsUnauthorizedAndWithoutTheScopeForbidden()
    {
        var anonymous = await Handle(Lambda.Route(GetEmployee, new() { ["employeeId"] = "e1" }, null));
        var unscoped = await Handle(Lambda.Route(GetEmployee, new() { ["employeeId"] = "e1" }, Lambda.PartnerWithoutScope));

        Assert.Equal((int)HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, unscoped.StatusCode);
    }

    [Fact]
    public async Task ARouteTheApiDoesntHaveIsNotFound()
    {
        var response = await Handle(Lambda.Route("DELETE /v1/employees/{employeeId}", new() { ["employeeId"] = "e1" }, Lambda.Partner));

        Assert.Equal((int)HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void TheSpecDescribesExactlyTheRoutesTheFunctionHandles()
    {
        var spec = new YamlStream();
        spec.Load(new StringReader(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "openapi.yaml"))));
        var paths = (YamlMappingNode)((YamlMappingNode)spec.Documents[0].RootNode)["paths"];
        string[] methods = ["get", "put", "post", "delete", "patch", "head", "options", "trace"];

        var operations = paths.Children
            .SelectMany(path => ((YamlMappingNode)path.Value).Children.Keys
                .Select(key => key.ToString())
                .Where(methods.Contains)
                .Select(method => $"{method.ToUpperInvariant()} {path.Key}"));

        Assert.Equal(_api.Routes.Order(), operations.Order());
    }

    private Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request) =>
        _api.Handle(request, Lambda.Context());
}
