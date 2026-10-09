using System.Net;
using System.Text.Json.Nodes;
using Amazon.Lambda.APIGatewayEvents;
using SampleService.UiBackend;

namespace SampleService.Tests;

public class UiBackendTests
{
    private readonly Fakes _fakes = new Fakes().WithStaff();
    private readonly Function _uiBackend;

    public UiBackendTests() => _uiBackend = new Function(_fakes.Register);

    [Fact]
    public async Task AQueryIsInvokedByGetWithItsArgumentsInTheQueryString()
    {
        var response = await Handle(Lambda.Get("EmployeeDetails", Lambda.Manager, new() { ["EmployeeId"] = "e1" }));

        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        var body = JsonNode.Parse(response.Body)!;
        Assert.Equal("Found", (string?)body["$case"]);
        Assert.Equal("Ed", (string?)body["Employee"]!["Name"]);
        Assert.Equal("Active", (string?)body["Employee"]!["Status"]);
        Assert.Equal("Fire", (string?)body["LinkToFire"]!["$feature"]);
    }

    [Fact]
    public async Task NumbersAreReadFromTheQueryString()
    {
        var response = await Handle(Lambda.Get("ReviewsDue", Lambda.Manager, new() { ["WithinDays"] = "90" }));

        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, JsonNode.Parse(response.Body)!["Employees"]!.AsArray().Count);
    }

    [Fact]
    public async Task ACommandIsInvokedByPostWithItsArgumentsAsTheBody()
    {
        var response = await Handle(Lambda.Post("Fire", Lambda.Manager, """{"EmployeeId":"e1"}"""));

        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"$case":"Fired"}""", response.Body);
    }

    [Fact]
    public async Task AnIdempotencyKeyHeaderMakesARetriedHireHireOnce()
    {
        var staff = _fakes.Employees.Count;

        var first = await Handle(Lambda.Post("Hire", Lambda.Manager, """{"Name":"Ann","ManagerId":"m1"}""", "request-1"));
        var retry = await Handle(Lambda.Post("Hire", Lambda.Manager, """{"Name":"Ann","ManagerId":"m1"}""", "request-1"));

        Assert.Equal(first.Body, retry.Body);
        Assert.Equal(staff + 1, _fakes.Employees.Count);
    }

    [Fact]
    public async Task AQueryCantBePostedNorACommandGot()
    {
        var postedQuery = await Handle(Lambda.Post("ReviewsDue", Lambda.Manager, "{}"));
        var gotCommand = await Handle(Lambda.Get("Fire", Lambda.Manager, new() { ["EmployeeId"] = "e1" }));

        Assert.Equal((int)HttpStatusCode.MethodNotAllowed, postedQuery.StatusCode);
        Assert.Equal("GET", postedQuery.Headers["allow"]);
        Assert.Equal((int)HttpStatusCode.MethodNotAllowed, gotCommand.StatusCode);
        Assert.Equal("POST", gotCommand.Headers["allow"]);
        Assert.Equal(EmploymentStatus.Active, _fakes.Employees["e1"]!.Status);
    }

    [Fact]
    public async Task AFeatureTheApiDoesntOfferIsNotFound()
    {
        var handler = await Handle(Lambda.Post("HandleEmployeeLeft", Lambda.Manager, """{"EmployeeId":"e1"}"""));
        var nothing = await Handle(Lambda.Get("Nothing", Lambda.Manager));

        Assert.Equal((int)HttpStatusCode.NotFound, handler.StatusCode);
        Assert.Equal((int)HttpStatusCode.NotFound, nothing.StatusCode);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"EmployeeId":null}""")]
    [InlineData("""{"$feature":"Hire","EmployeeId":"e1"}""")]
    public async Task ArgumentsThatDontMakeALinkAreABadRequest(string body)
    {
        var response = await Handle(Lambda.Post("Fire", Lambda.Manager, body));

        Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(EmploymentStatus.Active, _fakes.Employees["e1"]!.Status);
    }

    [Fact]
    public async Task AnAnonymousCallerIsUnauthorizedAndAnEmployeeForbidden()
    {
        var anonymous = await Handle(Lambda.Post("Fire", null, """{"EmployeeId":"e2"}"""));
        var employee = await Handle(Lambda.Post("Fire", Lambda.Employee, """{"EmployeeId":"e2"}"""));

        Assert.Equal((int)HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, employee.StatusCode);
        Assert.Equal(EmploymentStatus.Active, _fakes.Employees["e2"]!.Status);
    }

    private Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request) =>
        _uiBackend.Handle(request, Lambda.Context());
}
