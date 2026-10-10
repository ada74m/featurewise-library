# SampleService

A small HR service built with Featurewise: managers hire, fire and review their reports, a schedule reminds them of reviews and archives leavers, and a queue carries the work that follows a departure. It runs as three AWS Lambda functions, one per entrypoint, over one core.

## Projects

| Project | What it is |
|---|---|
| `SampleService` | The core: employees, the features with the rules about who may invoke them nested in them, and the services they depend on (a DynamoDB store, a work queue on SQS, an event publisher on SNS, a messenger that logs), and the events it publishes, in `Events/`. |
| `SampleService.UiBackend` | The backend for the service's own UI, on API Gateway's HTTP API. Its contract is the features themselves, links and result cases included, so it serves only a UI shipped with the service. `POST /{feature}` invokes a query or a command with its arguments as the JSON body, so the UI follows any link the same way. The caller comes from the JWT authorizer's claims, and an `Idempotency-Key` header becomes the invocation's idempotency key. |
| `SampleService.PublicApi` | The API for other organisations' systems, on API Gateway's HTTP API. Its contract is its own, in `openapi.yaml`: versioned resources (`GET /v1/employees/{employeeId}`), HTTP status codes for outcomes, RFC 9457 problems for errors, and public shapes without links, result cases or internal fields. Clients authenticate with OAuth client credentials from a Cognito user pool of their own, and the rule for each feature checks the token's scopes. |
| `SampleService.ScheduledWorker` | EventBridge Scheduler. Each schedule's input carries its job's link, so one function runs every job, as a `SystemCaller` named after the schedule. |
| `SampleService.QueueWorker` | SQS. A message is either a link the service sent itself, or an event from another service's SNS topic, which an adapter for that topic turns into a link. Failed messages are retried alone, then dead-lettered. |
| `SampleService.Tests` | Tests of the core's features, and of each entrypoint fed the events AWS would send it. |

Each entrypoint registers only the features it invokes. Where a feature sends a link to a feature that runs elsewhere, as `SendReviewReminders` queues `RemindManagerOfReviews` for the queue worker, the entrypoint registers that feature's links with `RegisterLinksTo`, so it can write them.

Features don't invoke features. `Fire` and `HandleAccountDeactivated` both record a departure, so they share the `LeaverRecorder` service, which saves the employee and publishes `EmployeeLeft`.

## Events and messages

The service announces what happens in it as events: the records in `SampleService/Events/`, such as `EmployeeHired` and `EmployeeLeft`. Look there to see everything the service publishes. Features publish them through `IEventPublisher` to the service's own SNS topic, each in a CloudEvents envelope with its type also as the message attribute `type`, so a subscription's filter policy can choose the events it wants. Events are a public contract, written in camelCase without domain types, and they change compatibly, as the public API does.

An event goes out just after the change it reports is saved, so a failure between the two loses it; an outbox, publishing from a stream of the table's changes, would close that gap. Delivery is at least once. An invocation with an idempotency key derives its events' ids from the key, so a retried request publishes the same ids again, and subscribers drop the duplicates.

A message is different: `IMessenger` sends text to a particular person, such as a manager's review reminder.

## Infrastructure

Each project keeps the Terraform for its own resources in its `Infrastructure` folder, as a module. `Infrastructure/` here is the root module, which calls them and passes one's outputs to another, such as the queue worker's queue URL to the UI backend. The functions share `Infrastructure/modules/lambda-function`.

Publish each function, then plan from the root module:

```
for project in UiBackend PublicApi ScheduledWorker QueueWorker; do
  dotnet publish samples/SampleService.$project -c Release -r linux-arm64 --self-contained false \
    -o samples/artifacts/SampleService.$project
done

cd samples/Infrastructure
terraform init
terraform plan -var jwt_issuer=https://cognito-idp.eu-west-2.amazonaws.com/<user pool> -var 'jwt_audience=["<app client>"]'
```

Without `jwt_issuer`, every UI backend caller is anonymous, and every feature the UI backend offers refuses them. `public_api_clients` lists the public API's third-party clients and their scopes; `terraform output -json public_api_clients` gives each one's id and secret, and `public_api_token_url` is where they trade them for a token. The public API throttles all clients together; quotas per client would need API keys on a REST API, or WAF rules. Without `directory_events_topic_arn`, the queue worker subscribes to no other service's events. State is local until a backend is configured in `Infrastructure/versions.tf`.
