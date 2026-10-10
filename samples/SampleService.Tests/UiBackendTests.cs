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
    public async Task AQueryIsInvokedByPostWithItsArgumentsAsTheBody()
    {
        var response = await Handle(Lambda.Post("EmployeeDetails", Lambda.Manager, """{"EmployeeId":"e1"}"""));

        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        var body = JsonNode.Parse(response.Body)!;
        Assert.Equal("Found", (string?)body["$case"]);
        Assert.Equal("Ed", (string?)body["Employee"]!["Name"]);
        Assert.Equal("Active", (string?)body["Employee"]!["Status"]);
        Assert.Equal("Fire", (string?)body["LinkToFire"]!["$feature"]);
    }

    [Fact]
    public async Task AQuerysArgumentsAreTypedAsTheFeatureDeclaresThem()
    {
        var withinAQuarter = await Handle(Lambda.Post("ReviewsDue", Lambda.Manager, """{"WithinDays":90}"""));
        var withinAMonth = await Handle(Lambda.Post("ReviewsDue", Lambda.Manager, null));

        Assert.Equal((int)HttpStatusCode.OK, withinAQuarter.StatusCode);
        Assert.Equal(2, JsonNode.Parse(withinAQuarter.Body)!["Employees"]!.AsArray().Count);
        Assert.Single(JsonNode.Parse(withinAMonth.Body)!["Employees"]!.AsArray());
    }

    [Fact]
    public async Task AnOfferedLinkIsFollowedByPostingItBackAsItCame()
    {
        var details = await Handle(Lambda.Post("EmployeeDetails", Lambda.Manager, """{"EmployeeId":"e1"}"""));
        var link = JsonNode.Parse(details.Body)!["LinkToFire"]!;

        var fired = await Handle(Lambda.Post((string)link["$feature"]!, Lambda.Manager, link.ToJsonString()));

        Assert.Equal("""{"$case":"Fired"}""", fired.Body);
        Assert.Equal(EmploymentStatus.Left, _fakes.Employees["e1"]!.Status);
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
    public async Task AFeatureTheUiBackendDoesntOfferIsNotFound()
    {
        var handler = await Handle(Lambda.Post("RemindManagerOfReviews", Lambda.Manager, """{"ManagerId":"m1"}"""));
        var nothing = await Handle(Lambda.Post("Nothing", Lambda.Manager, null));

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
