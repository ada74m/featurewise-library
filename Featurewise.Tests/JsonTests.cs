using System.Diagnostics;
using System.Text.Json;

namespace Featurewise.Tests;

public class JsonTests
{
    [Fact]
    public void ALinkRoundTripsThroughItsBaseTypeWithItsFeatureNameAndCause()
    {
        using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());
        Link<Fire.Result> link = new Fire.Args("e1") { CausedBy = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" };

        var json = JsonSerializer.Serialize(link, options);
        var readBack = JsonSerializer.Deserialize<Link<Fire.Result>>(json, options);

        Assert.Contains("\"$command\":\"hr/Fire\"", json);
        var fireLink = Assert.IsType<Fire.Args>(readBack);
        Assert.Equal("e1", fireLink.EmployeeId);
        Assert.Equal(link.CausedBy, fireLink.CausedBy);
    }

    [Fact]
    public void ALinkQualifiesItsFeatureWithItsServiceWhereverItIsSerialized()
    {
        using var features = new Features("hr");
        features.RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>().RegisterFeature<Fire, FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var link = JsonSerializer.Serialize<Link>(new Fire.Args("e1") { CausedBy = null }, options);
        var inAResult = JsonSerializer.Serialize(new EmployeeReview.Result("comments", new Fire.Args("e1") { CausedBy = null }), options);

        Assert.Equal("""{"$command":"hr/Fire","EmployeeId":"e1","CausedBy":null}""", link);
        Assert.Contains(""""LinkToFire":{"$command":"hr/Fire","EmployeeId":"e1"""", inAResult);
        Assert.Equal("hr", features.Service);
    }

    [Fact]
    public void ALinksJsonNamesItsFeatureInAPropertyThatSaysItsFamily()
    {
        using var features = new Features("hr");
        features.RegisterLinksTo<Fire>().RegisterLinksTo<EmployeeSearch>().RegisterLinksTo<HandleEmployeeLeft>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        Assert.StartsWith("""{"$command":"hr/Fire",""", JsonSerializer.Serialize<Link>(new Fire.Args("e1"), options));
        Assert.StartsWith("""{"$query":"hr/EmployeeSearch",""", JsonSerializer.Serialize<Link>(new EmployeeSearch.Args("a", 10, null, null), options));
        Assert.StartsWith("""{"$worker":"hr/HandleEmployeeLeft",""", JsonSerializer.Serialize<Link>(new HandleEmployeeLeft.Args("e1"), options));
        Assert.Equal("$query", Features.FeatureProperty(FeatureFamily.Query));
    }

    [Theory]
    [InlineData("""{"$query":"hr/Fire","EmployeeId":"e1"}""")]
    [InlineData("""{"$worker":"hr/Fire","EmployeeId":"e1"}""")]
    [InlineData("""{"$command":"hr/Fire","$query":"hr/Fire","EmployeeId":"e1"}""")]
    [InlineData("""{"EmployeeId":"e1"}""")]
    public void ALinkThatMislabelsItsFeaturesFamilyOrNamesItTwiceOrNotAtAllIsRejected(string json)
    {
        using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Link>(json, options));
    }

    [Fact]
    public void ALinkWrittenBeforeLinksSaidTheirFamilyIsStillRead()
    {
        using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var readBack = JsonSerializer.Deserialize<Link>("""{"$feature":"hr/Fire","EmployeeId":"e1"}""", options)!;

        Assert.Equal(new Fire.Args("e1"), readBack);
        Assert.StartsWith("""{"$command":"hr/Fire",""", JsonSerializer.Serialize(readBack, options));
    }

    [Theory]
    [InlineData("""{"$command":"payroll/Fire","EmployeeId":"e1"}""")]
    [InlineData("""{"$command":"Fire","EmployeeId":"e1"}""")]
    [InlineData("""{"$command":"/Fire","EmployeeId":"e1"}""")]
    public void ALinkForAnotherServiceOrNoneIsRejected(string json)
    {
        using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Link>(json, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Fire.Args>(json, options));
    }

    [Fact]
    public void ALinkCarriesItsFeatureNameWhereverItIsSerialized()
    {
        using var features = new Features("hr");
        features.RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>().RegisterFeature<Fire, FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize(new EmployeeReview.Result("comments", new Fire.Args("e1")), options);
        var readBack = JsonSerializer.Deserialize<EmployeeReview.Result>(json, options)!;

        Assert.Contains("\"LinkToFire\":{\"$command\":\"hr/Fire\"", json);
        Assert.Equal(new Fire.Args("e1"), readBack.LinkToFire);
    }

    [Fact]
    public void AFeatureNameAttributeNamesTheFeatureInJson()
    {
        using var features = new Features("hr");
        features.RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize<Link<EmployeeReview.Result>>(new EmployeeReview.Args("e1"), options);

        Assert.Contains("\"$query\":\"hr/review-employee\"", json);
    }

    [Fact]
    public void ALinkCanBeReadWithItsFeatureNameAnywhere()
    {
        using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var readBack = JsonSerializer.Deserialize<Link<Fire.Result>>("""{"EmployeeId":"e1","$command":"hr/Fire"}""", options);

        Assert.Equal(new Fire.Args("e1"), readBack);
    }

    [Fact]
    public void SerializingALinkToAnUnregisteredFeatureThrows()
    {
        using var features = new Features("hr");
        var options = features.ConfigureJson(new JsonSerializerOptions());

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize<Link<Fire.Result>>(new Fire.Args("e1"), options));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new Fire.Args("e1"), options));
    }

    [Fact]
    public void ALinkToAFeatureRegisteredOnlyForItsLinksRoundTrips()
    {
        using var features = new Features("hr");
        features.RegisterLinksTo<EmployeeReview>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize<Link>(new EmployeeReview.Args("e1"), options);
        var readBack = JsonSerializer.Deserialize<Link>(json, options);

        Assert.Contains("\"$query\":\"hr/review-employee\"", json);
        Assert.Equal(new EmployeeReview.Args("e1"), readBack);
    }

    [Fact]
    public void ALinkReadFromJsonWithoutACauseHasNone()
    {
        using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());
        using var request = new Activity("request reading the link").Start();

        var readThroughFeatures = JsonSerializer.Deserialize<Link<Fire.Result>>("""{"$command":"hr/Fire","EmployeeId":"e1"}""", options)!;
        var readPlainly = JsonSerializer.Deserialize<Fire.Args>("""{"EmployeeId":"e1"}""")!;

        Assert.Null(readThroughFeatures.CausedBy);
        Assert.Null(readPlainly.CausedBy);
        Assert.Equal(request.Id, new Fire.Args("e1").CausedBy);
    }

    [Fact]
    public async Task ALinkReadFromJsonAsAnyLinkCanBeInvoked()
    {
        await using var features = new Features("hr");
        features.RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>().RegisterFeature<Fire, FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());
        var json = JsonSerializer.Serialize(new Fire.Args("e1"), options);

        var link = JsonSerializer.Deserialize<Link>(json, options)!;
        var result = await features.Invoke(link, Callers.Manager);

        Assert.Equal(new Fire.Args("e1"), link);
        Assert.Equal("""{"$case":"Fired"}""", JsonSerializer.Serialize(result, options));
    }

    [Fact]
    public void NilRoundTripsAsAnEmptyObject()
    {
        using var features = new Features("hr");
        features.RegisterFeature<HandleEmployeeLeft, AnyCaller<HandleEmployeeLeft.Args>>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize(Nil.Value, options);

        Assert.Equal("{}", json);
        Assert.Equal(Nil.Value, JsonSerializer.Deserialize<Nil>(json, options));
    }

    [Fact]
    public void AResultRoundTripsThroughItsBaseTypeWithItsCaseName()
    {
        using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize<Fire.Result>(new Fire.Result.NotFound("e1"), options);
        var readBack = JsonSerializer.Deserialize<Fire.Result>(json, options);

        Assert.Equal("""{"$case":"NotFound","EmployeeId":"e1"}""", json);
        Assert.Equal(new Fire.Result.NotFound("e1"), readBack);
    }

    [Fact]
    public void AResultCarriesItsCaseNameWhereverItIsSerialized()
    {
        using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize(new Fire.Result.Fired(), options);

        Assert.Equal("""{"$case":"Fired"}""", json);
        Assert.Equal(new Fire.Result.Fired(), JsonSerializer.Deserialize<Fire.Result>(json, options));
    }

    [Fact]
    public void AResultWithoutCasesIsSerializedAsItIs()
    {
        using var features = new Features("hr");
        features.RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var json = JsonSerializer.Serialize(new EmployeeReview.Result("comments", null), options);

        Assert.Equal("""{"Comments":"comments","LinkToFire":null}""", json);
    }

    [Fact]
    public void ASealedCaseNamesItselfOnlyThroughItsResultType()
    {
        using var features = new Features("hr");
        features.RegisterFeature<TossCoin, AnyCaller<TossCoin.Args>>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        Assert.Equal("""{"$case":"Heads"}""", JsonSerializer.Serialize<TossCoin.Result>(new TossCoin.Result.Heads(), options));
        Assert.Equal("{}", JsonSerializer.Serialize(new TossCoin.Result.Heads(), options));
        Assert.Equal("""{"$case":"Tails"}""", JsonSerializer.Serialize(new TossCoin.Result.Tails(), options));
    }

    [Theory]
    [InlineData("""{"$command":"hr/Fire"}""")]
    [InlineData("""{"$command":"hr/Fire","EmployeeId":null}""")]
    [InlineData("""{"$query":"hr/EmployeeSearch","Limit":10}""")]
    [InlineData("""{"$query":"hr/EmployeeSearch","Query":"a"}""")]
    [InlineData("""{"$query":"hr/EmployeeSearch","Query":"a","Limit":null}""")]
    public void ALinkMissingANonNullableArgumentIsRejected(string json)
    {
        using var features = new Features("hr");
        features.RegisterFeature<Fire, FireAuthorization>().RegisterFeature<EmployeeSearch, AnyCaller<EmployeeSearch.Args>>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Link>(json, options));
    }

    [Fact]
    public async Task ALinksNullableAndDefaultedArgumentsMayBeLeftOut()
    {
        await using var features = new Features("hr");
        features.RegisterFeature<EmployeeSearch, AnyCaller<EmployeeSearch.Args>>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var link = JsonSerializer.Deserialize<Link>("""{"$query":"hr/EmployeeSearch","Query":"a","Limit":10}""", options)!;

        Assert.Equal("a 10 - - 1", await features.Invoke(link, Callers.Anonymous));
    }

    [Fact]
    public void TypesOtherThanLinksAreReadAsBefore()
    {
        using var features = new Features("hr");
        features.RegisterFeature<EmployeeReview, AnyCaller<EmployeeReview.Args>>();
        var options = features.ConfigureJson(new JsonSerializerOptions());

        var result = JsonSerializer.Deserialize<EmployeeReview.Result>("{}", options)!;

        Assert.Null(result.Comments);
    }
}
