# Featurewise

Featurewise for .NET: [featurewise.dev/dotnet](https://featurewise.dev/dotnet/), and the ideas behind it at [featurewise.dev](https://featurewise.dev/).

A service is the set of things it can do, and each of those things is a feature: a query, a command or a worker. Featurewise gives every feature the same shape, and gives the code that calls features one way to call any of them.

The call is a value. A **link** names a feature and carries the arguments to invoke it with. A link is a potential invocation: an entrypoint invokes it now, a feature returns it to say what the caller can do next, a queue carries it to a worker. **`Features`** invokes links, and wraps every invocation in the same scope, authorization, logging, tracing and cancellation.

## A feature and its link

```csharp
public class Fire(IEmployeeStore employees) : Command<Fire.Args, Fire.Result>
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

`Fire.Args` is the link. It derives from `Link<Fire, Result>`, so its type names the feature it invokes and the result it produces. A link type names exactly one feature, and a feature accepts only links to itself: `RegisterFeature` rejects a feature whose arguments link to another.

`Fire.Result` has cases. An outcome the feature expects, such as an employee who isn't there, is a case it returns. Exceptions are left for what nobody expected: the database is down, the code is wrong.

A feature takes its dependencies through its constructor, as any service does. It can also take `Invocation`, which says who invoked it, which request it serves, and where it sits in a trace.

## Queries, commands and workers

Every feature belongs to one of three families, and derives from its base class:

- **`Query<TArgs, TResult>`** answers a question and changes nothing, such as `EmployeeSearch`. It's safe to repeat.
- **`Command<TArgs, TResult>`** changes state for a caller who may be waiting to know how it went, such as `Fire`.
- **`Worker<TArgs>`** changes state with nobody waiting, such as `HandleEmployeeLeft`. Its result is always `Nil`.

Anything someone might wait for is a command, even when a job sometimes invokes it. A worker is a feature nobody ever waits for.

An entrypoint asks `features.IsQuery("hr/EmployeeSearch")` before invoking a feature in a way that may repeat it: a UI backend that answers GET, a client that retries, a cache. And `features.FamilyOf("hr/EmployeeSearch")` gives any feature's family, which a UI backend passes on so that its UI gives queries addresses. Deriving from `Query` is a promise the compiler can't check, so a query that writes anything a stakeholder would care about is a bug for review to catch.

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
    private readonly Features _features = new Features("hr")
        .RegisterFeature<Fire, Fire.Rule>()
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
builder.Services.AddFeatures("hr", features => features
    .RegisterFeature<Fire, Fire.Rule>());
builder.Services.AddOptions<JsonOptions>()
    .Configure<Features>((json, features) => features.ConfigureJson(json.SerializerOptions));

app.MapPost("/invoke", (Link link, ClaimsPrincipal user, Features features) => features.Invoke(link, user));
```

Either way, each invocation gets a dependency injection scope of its own, so a feature never shares a database context with the request that invoked it.

An endpoint that takes the feature's name from the request, such as `POST /{feature}`, asks `IsInvocable("hr/Fire")` first, so it can answer a feature it doesn't invoke as not found before reading the arguments.

## Who may invoke what

Every invocation has a caller, and every invocation is refused unless a rule approves it. A feature is registered with its rules, so every feature has at least one.

A rule is an authorization handler for `InvokeRequirement`, with the feature's arguments as the resource. It sees the caller and the arguments, and can load whatever else it needs. A rule that belongs to one feature lives inside it, beside its `Args` and `Result`:

```csharp
public class Fire(IEmployeeStore employees) : Command<Fire.Args, Fire.Result>
{
    ...

    // Managers may fire anyone but themselves.
    public class Rule : AuthorizationHandler<InvokeRequirement, Args>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context, InvokeRequirement requirement, Args link)
        {
            if (context.User.IsInRole("manager") && link.EmployeeId != context.User.Identity?.Name)
                context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}

features.RegisterFeature<Fire, Fire.Rule>();
```

Three rules are ready-made. `AnyCaller<OpeningHours.Args>` opens a feature to everyone. `AnySignedInCaller<EmployeeReview.Args>` opens one to every signed-in caller. A job invokes links as `SystemCaller.For("nightly-reminders")`, which is signed in and named but has no special powers; `AnySystemCaller<HandleRemindersDue.Args>` opens a feature to system callers alone, for the workers that only the service itself should invoke.

A feature may have up to three rules, and anyone one of them approves may invoke it: `RegisterFeature<Fire, Fire.Rule, AnySystemCaller<Fire.Args>>()` lets managers and jobs fire people. Each entrypoint names the rules for the features it registers, so a feature can be open to more callers through one entrypoint than another. `RegisterFeature` rejects a rule written for another feature's arguments, and a feature with ASP.NET's `[AllowAnonymous]`, which Featurewise doesn't honour.

A feature that offers a link asks `LinkPermissions.MayInvoke` first, which applies the same rules, so callers see only the links they can follow. Offering a link proves nothing later: a link that comes back is checked again.

A refused invocation throws `InvocationDeniedException`, which says whether the caller was signed in, so an HTTP adapter can answer 401 or 403.

## Links on the wire

Links leave the process and come back. `ConfigureJson` sets up JSON options so they survive the trip:

```json
{"$command":"hr/Fire","EmployeeId":"e1","CausedBy":"00-4bf9…-00f0…-01"}
```

The first property names the feature, so a general endpoint can read any link as `Link` and invoke it without knowing what it is. Its name says the feature's family, `$query`, `$command` or `$worker`, so whoever holds a link can tell what following it will do: a query only fetches information, and is safe to repeat, while a command changes something. A link whose property doesn't match its feature's family is refused. Links written before the family was named used `$feature`, which is still read but never written. A feature's name is its class name, or the name in its `[FeatureName]` attribute, which lets the class be renamed without breaking links already handed out. Results with cases carry a `$case` property the same way.

The feature's name is qualified by its service, given when `Features` is created: `new Features("hr")`, or `AddFeatures("hr", …)` in a host. One client, such as a UI that combines several services, can hold links from all of them and send each back to the service named before the slash. A service reads only its own links: one that names no service or another throws a `JsonException`, so a link sent to the wrong service is refused instead of invoked as one of its features. Like a feature's name, a service's id is in every link handed out, so it must not change.

The options know the features registered with `Features`. An entrypoint that registers only some features can still send links to the rest: `RegisterLinksTo<HandleEmployeeLeft>()` lets its links be written and read here, while invoking one still fails, because the feature runs elsewhere.

Reading a link checks its arguments against their types. A missing required argument, or `null` where the type forbids it, fails with a `JsonException` before the link reaches authorization or the feature.

A link can outlive the code that made it: a browser tab holds it across a deployment, a queue holds it through one. Change arguments compatibly. Adding a nullable or defaulted argument is safe. Removing, renaming or requiring one takes two releases.

## Workers

A worker is a feature like any other. The message or schedule that triggers it carries a link:

```csharp
var jsonOptions = features.ConfigureJson(new JsonSerializerOptions());

foreach (var message in sqsEvent.Records)
{
    var link = JsonSerializer.Deserialize<Link>(message.Body, jsonOptions)!;
    await features.Invoke(link, SystemCaller.For("employee-events"), message.MessageId, cancellationToken);
}
```

Nobody waits for a worker's answer, so its result is always `Nil`. Its invocation still logs whether it succeeded or failed. Anything more, such as how many records it closed or that the employee was unknown, goes in a log line the worker writes itself:

```csharp
public class HandleEmployeeLeft(IPayroll payroll, ILogger<HandleEmployeeLeft> logger) : Worker<HandleEmployeeLeft.Args>
{
    public record Args(string EmployeeId) : Link<HandleEmployeeLeft, Nil>;

    public override async Task<Nil> Invoke(Args args, CancellationToken cancellationToken)
    {
        var closed = await payroll.Close(args.EmployeeId, cancellationToken);
        logger.LogInformation("Closed {Count} payroll records for {EmployeeId}", closed, args.EmployeeId);
        return Nil.Value;
    }
}
```

Queues deliver each message at least once, so a worker may run twice for one event. The message id goes in as the idempotency key, and the worker reads it from `Invocation.IdempotencyKey` to recognise a redelivery. What a repeat means belongs to the worker: closing payroll for someone already closed can simply do nothing.

## What every invocation records

Each invocation runs in an `Activity` from the `Featurewise` source, which OpenTelemetry exports with `AddSource(Features.ActivitySourceName)`. Its parent is whatever operation invoked it, such as an HTTP request. A link records the invocation that created it as `CausedBy`, and the invocation that later follows the link carries that as a trace link. A trace therefore shows two things: what each invocation ran inside, and which earlier invocation offered the action.

Each invocation logs its start, with its id, its cause and any idempotency key; its finish, with the elapsed time and the result's case; and any failure, cancellation or refusal. Everything the feature logs carries the same ids.

## Boundaries

- **Features don't invoke features.** A feature that leads to another returns a link to it, and the entrypoint decides whether to follow it. Shared logic lives in a service both features use. A feature can't take another feature as a dependency, and a feature that calls `Invoke` fails.
- **Every link names one feature.** Links derive from `Link<TFeature, TResult>` and from nothing else.
- **Deny by default.** Every feature is registered with its rules, and only what they approve is invoked.

## Installing

Featurewise comes as two packages on nuget.org, published together at one version, as previews for now:

- `Featurewise.Core` is what a service's core is written with: `Query`, `Command` and `Worker`, links, `Invocation`, `LinkPermissions`, `InvokeRequirement`, `SystemCaller` and the ready-made rules.
- `Featurewise.Hosting` is what its entrypoints invoke features with: `Features`, its registration methods, `AddFeatures` and `InvocationDeniedException`. It depends on exactly the same version of `Featurewise.Core`.

A core project that references only `Featurewise.Core` can't build a container or invoke a feature, so the compiler keeps features from invoking features. Both packages use the namespace `Featurewise`. Add the package each project needs:

```
dotnet add package Featurewise.Core --prerelease       # in the service's core
dotnet add package Featurewise.Hosting --prerelease    # in each entrypoint
```

## Building and testing

```
dotnet test
```

`global.json` pins the .NET 10 SDK to released versions. `Featurewise` is `Featurewise.Core`, `Featurewise.Hosting` is `Featurewise.Hosting`, and `Featurewise.Tests` holds their tests and the small features they exercise.

## Licence

MIT: see `LICENSE`.
