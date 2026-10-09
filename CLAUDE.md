# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```
dotnet build                                                   # build the solution (FeaturewiseProto.sln)
dotnet test                                                    # run all tests
dotnet test --filter "FullyQualifiedName~RegisteringAFeatureTwiceThrows"   # run one test
```

`global.json` pins the .NET 10 SDK (released versions only, rolls forward to the latest feature band). Tests use xUnit 2.

A change is done when the build is warning-free (doc comments included) and the full test run passes. For a behaviour change, temporarily remove the new code to confirm its test fails.

## What this is

Featurewise is a library (`Featurewise/`) for structuring a service as a set of **features** (commands, queries, event handlers) invoked through **links** (records that name a feature and carry its arguments). `README.md` is the user-facing explanation of the model and its rules; read it before changing behaviour. The XML doc comments in the library are the detailed spec of each API's behaviour and are kept in step with the code (`GenerateDocumentationFile` is on, so a public member without one produces a build warning).

The design rules the code enforces, which changes must preserve (README "Boundaries"):
- **Features don't invoke features.** A feature that leads to another returns a link to it; shared logic goes in a service.
- **Every link names one feature.** Links derive from `Link<TFeature, TResult>` and nothing else.
- **Deny by default.** A feature with no authorization rule can't be invoked.
- **Links outlive the code that made them.** Adding a nullable or defaulted argument is safe; removing, renaming or requiring one takes two releases.

Settled decisions, not to be reopened: named authorization policies were rejected; `Invocation` deliberately holds neither the args nor the result; idempotency belongs to features (the framework only passes the key along); versioning has no machinery, only the compatible-change habit; `Feature.Invoke` always takes a cancellation token; Native AOT isn't supported.

## Conventions

- One type per file, except generic arities of one name, which share a file (`Link.cs`, `IFeature.cs`), and a sample facility's interface and its implementation, which share a file named after the interface without the `I` (`IEmployeeStore` and `DynamoEmployeeStore` in `Facilities/EmployeeStore.cs`).
- Feature names: commands are verb phrases, queries are noun phrases, event handlers are `Handle` plus the event. In the tests, `EmployeeSearch` is a query, `Remind` a command and `HandleEmployeeLeft` an event handler.
- Prose (README, XML docs) is in classic style: direct, no metadiscourse, no hedging.

## Architecture

- **`Link` hierarchy** (`Link.cs`): `Link` → `Link<TResult>` → `Link<TFeature, TResult>`, with `private protected` constructors so every link must derive from the last and therefore names exactly one feature. A feature's `Args` record is its link. `CausedBy` is stamped from `Activity.Current` at construction, excluded from equality, and cleared on deserialization unless the JSON supplied it.
- **`Feature<TFeature, TArgs, TResult>`** (`Feature.cs`) implements the non-generic `IFeature` so `Features` can invoke any feature from a plain `Link`. `TFeature` must be the deriving class itself; the compiler can't enforce that, so `FeaturesBuilder.RegisterFeature` checks it by reflection.
- **`Features`** (`Features.cs`) is the single invocation pipeline, in two modes sharing the same `Invoke`:
  - *Standalone*: `new Features()` owns a `FeaturesBuilder` and a lazily built `ServiceProvider` (`ValidateScopes`/`ValidateOnBuild`). `Build()` or the first `Invoke` builds it and closes registration.
  - *Hosted*: `services.AddFeatures(...)` (`FeatureServiceCollectionExtensions.cs`) registers into the host's container; `Features` is constructed internally with the host's provider.
- **`FeaturesBuilder`** registers shared infrastructure once per service collection (logging, authorization core, scoped `Invocation` and `LinkPermissions`, and a singleton `FeatureRegistry`) and finds an existing registry on later calls. Features are registered as **keyed scoped services under `Features.FeatureKey`**, a private object, which is how "features can't depend on features" is enforced: nothing else can resolve them, so a dependent fails container validation.
- **`Invoke` pipeline order** matters: reject nested invocation (an `AsyncLocal<bool>`) → check registration → create async scope → start `Activity` (parent = current activity; `CausedBy` becomes an `ActivityLink`) → populate scoped `Invocation` → log scopes and start message → authorize via `LinkPermissions.MayInvoke` *before* resolving the feature → invoke → log outcome (the result's runtime type name, i.e. the case). Authorization failures that throw are logged as failures; a denial is logged as a warning and throws `InvocationDeniedException`. Caller cancellation is logged as cancellation, not failure.
- **Authorization** is deny-by-default through ASP.NET Core authorization: `InvokeRequirement` with the link as the resource. `RegisterFeature<TFeature, TRule1[, TRule2[, TRule3]]>` registers a feature with one to three rules (authorization handlers), checked by reflection to handle the feature's `Args`; there is no other way to register a handler. A feature's own rule is nested in it (`Fire.Rule`); `AnyCaller`, `AnySignedInCaller` and `AnySystemCaller` are ready-made. `[AllowAnonymous]` is not honoured, and a feature that has it is rejected at registration. `SystemCaller.For(name)` makes a named, authenticated principal for jobs.
- **JSON** (`FeatureRegistry.ConfigureJson`): adds type-info modifiers that make link types polymorphic with a `$feature` discriminator (the feature name, from the class name or `[FeatureName]`), enforce required/non-null link arguments on read (links only, so host types are unaffected), and make registered result types with cases polymorphic with a `$case` discriminator. Cases are public non-abstract types nested in an abstract result type; they should stay unsealed.
- `Features` registers its own `ActivityListener` so every invocation gets an activity id even without OpenTelemetry.

## Samples

`samples/` holds SampleService, an HR service on AWS Lambda that uses the library as a real service would (`samples/README.md`): a core project, one project per entrypoint (`Api`, `ScheduledWorker`, `QueueWorker`), each registering only the features it invokes, and `SampleService.Tests`, which drives each entrypoint with fake Lambda events over in-memory fakes. In the core, `Models/` holds domain models (`Employee`), `Features/` the features, each with its rule nested in it when it has one of its own, and `Facilities/` whatever else supports the features (stores, the work queue, the notifier, shared services such as `LeaverRecorder`); all share the `SampleService` namespace. Each project keeps its Terraform in its own `Infrastructure/` module; `samples/Infrastructure/` is the root module that wires them together. Check Terraform with `terraform fmt -check -recursive samples` and, in `samples/Infrastructure`, `terraform init -backend=false && terraform validate`; never `plan` or `apply` against AWS unasked.

## Tests

The tests are split by topic into `*Tests.cs` files (`RegistrationTests`, `InvocationTests`, `AuthorizationTests`, `JsonTests`, `HostingTests`, …), named as sentences describing behaviour. Test classes run one at a time (`TestParallelization.cs`), because a test's `ActivityListener` hears every invocation in the process. The other files in that project are small purpose-built features, authorization handlers and helpers that the tests exercise (e.g. `Fire`, `EmployeeReview`, `Promote` as a deliberately mis-declared feature, `Callers` for test principals, `CapturingLoggerProvider` for asserting on logs). Add a new fixture feature as its own file with a one-line comment saying what it's for.
