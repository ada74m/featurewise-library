# SampleService

A small HR service built with Featurewise: managers hire, fire and review their reports, a schedule reminds them of reviews and archives leavers, and a queue carries the work that follows a departure. It runs as three AWS Lambda functions, one per entrypoint, over one core.

## Projects

| Project | What it is |
|---|---|
| `SampleService` | The core: employees, the features, the rules about who may invoke them, and the services they depend on (a DynamoDB store, a link queue on SQS, a notifier that logs). |
| `SampleService.Api` | API Gateway's HTTP API. `GET /{feature}` invokes a query with its arguments in the query string; `POST /{feature}` invokes a command with its arguments as the JSON body. The caller comes from the JWT authorizer's claims, and an `Idempotency-Key` header becomes the invocation's idempotency key. |
| `SampleService.ScheduledWorker` | EventBridge Scheduler. Each schedule's input carries its job's link, so one function runs every job, as a `SystemCaller` named after the schedule. |
| `SampleService.QueueWorker` | SQS. A message is either a link the service sent itself, or an event from another service's SNS topic, which an adapter for that topic turns into a link. Failed messages are retried alone, then dead-lettered. |
| `SampleService.Tests` | Tests of the core's features, and of each entrypoint fed the events AWS would send it. |

Each entrypoint registers only the features it invokes. Where a feature sends a link to a feature that runs elsewhere, as `Fire` queues `HandleEmployeeLeft`, the entrypoint registers that feature's links with `RegisterLinksTo`, so it can write them.

Features don't invoke features. `Fire` and `HandleAccountDeactivated` both record a departure, so they share the `LeaverRecorder` service, which saves the employee and queues the link to `HandleEmployeeLeft`.

## Infrastructure

Each project keeps the Terraform for its own resources in its `infrastructure` folder, as a module. `infrastructure/` here is the root module, which calls them and passes one's outputs to another, such as the queue worker's queue URL to the API. The functions share `infrastructure/modules/lambda-function`.

Publish each function, then plan from the root module:

```
for project in Api ScheduledWorker QueueWorker; do
  dotnet publish samples/SampleService.$project -c Release -r linux-arm64 --self-contained false \
    -o samples/artifacts/SampleService.$project
done

cd samples/infrastructure
terraform init
terraform plan -var jwt_issuer=https://cognito-idp.eu-west-2.amazonaws.com/<user pool> -var 'jwt_audience=["<app client>"]'
```

Without `jwt_issuer`, every API caller is anonymous, and every feature the API offers refuses them. Without `directory_events_topic_arn`, the queue worker subscribes to no other service's events. State is local until a backend is configured in `infrastructure/versions.tf`.
