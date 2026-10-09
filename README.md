# Featurewise

A service is the set of things it can do, and each of those things is a feature: a command, a query or an event handler. Featurewise gives every feature the same shape, and gives the code that calls features one way to call any of them.

The call is a value. A **link** names a feature and carries the arguments to invoke it with. A link is a potential invocation: an entrypoint invokes it now, a feature returns it to say what the caller can do next, a queue carries it to a handler. **`Features`** invokes links, and wraps every invocation in the same scope, authorization, logging, tracing and cancellation.

## A feature and its link

```csharp
public class Fire(IEmployeeStore employees) : Feature<Fire, Fire.Args, Fire.Result>
{
    public record Args(string EmployeeId) : Link<Fire, Result>;

    public abstract record Result
    {
        private Result() { }

        public record Fired : Result;
        public record NotFound(string EmployeeId) : Result;
    }

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        var employee = await employees.Find(args.EmployeeId, cancellationToken);
        if (employee is null)
            return new Result.NotFound(args.EmployeeId);

        await employees.Dismiss(employee, cancellationToken);
        return new Result.Fired();
    }
}
```

`Fire.Args` is the link. It derives from `Link<Fire, Result>`, so its type names the feature it invokes and the result it produces. A link type names exactly one feature, and a feature accepts only links to itself: the compiler rejects most mismatches, and `RegisterFeature` rejects the rest.

`Fire.Result` has cases. An outcome the feature expects, such as an employee who isn't there, is a case it returns. Exceptions are left for what nobody expected: the database is down, the code is wrong.

A feature takes its dependencies through its constructor, as any service does. It can also take `Invocation`, which says who invoked it, which request it serves, and where it sits in a trace.

## Invoking links

An entrypoint holds a `Features` and calls `Invoke` with a link and the caller:

```csharp
var result = await features.Invoke(new Fire.Args("e1"), caller, cancellationToken);
```

`Features` runs in one of two ways.

**Standalone**, for a Lambda function, a console tool or a test. `Features` owns a container of its own:

```csharp
public class Function
{
    private readonly Features _features = new Features()
        .RegisterFeature<Fire>()
        .RegisterAuthorizationHandler<FireAuthorization>()
        .RegisterDependencies(services => services.AddScoped<IEmployeeStore, DynamoEmployeeStore>())
        .Build();

    public async Task<Fire.Result> Handler(FireRequest request, ILambdaContext context)
    {
        using var timeout = new CancellationTokenSource(context.RemainingTime - TimeSpan.FromSeconds(1));
        return await _features.Invoke(new Fire.Args(request.EmployeeId), CallerFrom(request), timeout.Token);
    }
}
```

`Build` creates the container and checks every registration. Called in the function's constructor, it does that work during Lambda's initialization, and a missing dependency stops the function from starting instead of failing its first event.

**Hosted**, for ASP.NET or a worker service. Features register in the host's container and use its configuration, logging and services:

```csharp
builder.Services.AddFeatures(features => features
    .RegisterFeature<Fire>()
    .RegisterAuthorizationHandler<FireAuthorization>());
builder.Services.AddOptions<JsonOptions>()
    .Configure<Features>((json, features) => features.ConfigureJson(json.SerializerOptions));

app.MapPost("/invoke", (Link link, ClaimsPrincipal user, Features features) => features.Invoke(link, user));
```

Either way, each invocation gets a dependency injection scope of its own, so a feature never shares a database context with the request that invoked it.

## Who may invoke what

Every invocation has a caller, and every invocation is refused unless a rule approves it. A feature with no rule can't be invoked by anyone.

A rule is an authorization handler for `InvokeRequirement`, with the link as the resource. It sees the caller and the arguments, and can load whatever else it needs:

```csharp
// Managers may fire anyone but themselves.
public class FireAuthorization : AuthorizationHandler<InvokeRequirement, Fire.Args>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InvokeRequirement requirement, Fire.Args link)
    {
        if (context.User.IsInRole("manager") && link.EmployeeId != context.User.Identity?.Name)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

`AnySignedInCaller<EmployeeReview.Args>` opens one feature to every signed-in caller. `[AllowAnonymous]` on a feature opens it to everyone. A job invokes links as `SystemCaller.For("nightly-reminders")`, which is signed in and named but has no special powers.

A feature that offers a link asks `LinkPermissions.MayInvoke` first, which applies the same rules, so callers see only the links they can follow. Offering a link proves nothing later: a link that comes back is checked again.

A refused invocation throws `InvocationDeniedException`, which says whether the caller was signed in, so an HTTP adapter can answer 401 or 403.

## Links on the wire

Links leave the process and come back. `ConfigureJson` sets up JSON options so they survive the trip:

```json
{"$feature":"Fire","EmployeeId":"e1","CausedBy":"00-4bf9…-00f0…-01"}
```

The `$feature` property names the feature, so a general endpoint can read any link as `Link` and invoke it without knowing what it is. A feature's name is its class name, or the name in its `[FeatureName]` attribute, which lets the class be renamed without breaking links already handed out. Results with cases carry a `$case` property the same way.

The options know the features registered with `Features`. An entrypoint that registers only some features can still send links to the rest: `RegisterLinksTo<HandleEmployeeLeft>()` lets its links be written and read here, while invoking one still fails, because the feature runs elsewhere.

Reading a link checks its arguments against their types. A missing required argument, or `null` where the type forbids it, fails with a `JsonException` before the link reaches authorization or the feature.

A link can outlive the code that made it: a browser tab holds it across a deployment, a queue holds it through one. Change arguments compatibly. Adding a nullable or defaulted argument is safe. Removing, renaming or requiring one takes two releases.

## Events

An event handler is a feature like any other. The message that triggers it carries a link:

```csharp
var jsonOptions = features.ConfigureJson(new JsonSerializerOptions());

foreach (var message in sqsEvent.Records)
{
    var link = JsonSerializer.Deserialize<Link>(message.Body, jsonOptions)!;
    await features.Invoke(link, SystemCaller.For("employee-events"), message.MessageId, cancellationToken);
}
```

Nobody waits for an event handler's answer, but every feature returns a result. A handler with nothing to say returns `Nil.Value`, and its invocation logs its outcome as `Nil`:

```csharp
public class HandleEmployeeLeft(IPayroll payroll) : Feature<HandleEmployeeLeft, HandleEmployeeLeft.Args, Nil>
{
    public record Args(string EmployeeId) : Link<HandleEmployeeLeft, Nil>;

    public override async Task<Nil> Invoke(Args args, CancellationToken cancellationToken)
    {
        await payroll.Close(args.EmployeeId, cancellationToken);
        return Nil.Value;
    }
}
```

Queues deliver each message at least once, so a handler may run twice for one event. The message id goes in as the idempotency key, and the handler reads it from `Invocation.IdempotencyKey` to recognise a redelivery. What a repeat means belongs to the handler: firing someone who has already left can simply return a case that says so.

## What every invocation records

Each invocation runs in an `Activity` from the `Featurewise` source, which OpenTelemetry exports with `AddSource(Features.ActivitySourceName)`. Its parent is whatever operation invoked it, such as an HTTP request. A link records the invocation that created it as `CausedBy`, and the invocation that later follows the link carries that as a trace link. A trace therefore shows two things: what each invocation ran inside, and which earlier invocation offered the action.

Each invocation logs its start, with its id, its cause and any idempotency key; its finish, with the elapsed time and the result's case; and any failure, cancellation or refusal. Everything the feature logs carries the same ids.

## Boundaries

- **Features don't invoke features.** A feature that leads to another returns a link to it, and the entrypoint decides whether to follow it. Shared logic lives in a service both features use. A feature can't take another feature as a dependency, and a feature that calls `Invoke` fails.
- **Every link names one feature.** Links derive from `Link<TFeature, TResult>` and from nothing else.
- **Deny by default.** No rule, no invocation.

## Building and testing

```
dotnet test
```

`global.json` pins the .NET 10 SDK to released versions. `Featurewise` is the library; `Featurewise.Tests` holds its tests and the small features they exercise.
