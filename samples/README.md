# SampleService

A small HR service built with Featurewise: managers hire, fire and review their reports, a schedule reminds them of reviews and archives leavers, and a queue carries the work that follows a departure. It runs as three AWS Lambda functions, one per entrypoint, over one core.

## Projects

| Project | What it is |
|---|---|
| `SampleService` | The core: employees, the features with the rules about who may invoke them nested in them, and the services they depend on (a DynamoDB store, a work queue on SQS, an event publisher on SNS, a messenger that logs), and the events it publishes, in `Events/`. |
| `SampleService.UiBackend` | The backend for the service's own UI, on API Gateway's HTTP API. Its contract is the features themselves, links and result cases included, so it serves only a UI shipped with the service. `POST /{feature}` invokes a query or a command with its arguments as the JSON body, so the UI follows any link the same way. Every link's `$feature` is qualified by the service, as in `"$feature":"hr/Fire"`, so a UI shell combining several services' modules sends each link back to its own service's backend, at `POST /Fire`. The backend's URL names the service and feature, so a body may leave `$feature` out, but mustn't name another. The caller comes from the JWT authorizer's claims, and an `Idempotency-Key` header becomes the invocation's idempotency key. |
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

Each project keeps the Terraform for its own resources in its `Infrastructure` folder, as a module. `Infrastructure/` here is the service's module, which calls them and passes one's outputs to another, such as the queue worker's queue URL to the UI backend. The functions share `Infrastructure/modules/lambda-function`. The service's module has no provider or state of its own: an environment supplies those, with the inputs that differ between environments, such as the issuer of its users' tokens.

## Packages and CI

`package.sh <version> <out-dir>` builds the service's package: `hr-<version>.zip`, holding the Terraform modules in their places and each function's zip in `functions/`. An environment uses it as a module, `source = "s3::https://…/hr/<version>.zip//Infrastructure"`; the `//` makes Terraform fetch the whole package, so the modules find each other and the functions.

The `CI` workflow (`.github/workflows/ci.yml`) builds the solution with warnings as errors, runs the tests, checks the Terraform, and builds a package, for every pull request and push to main. Each push to main also publishes the package to the estate's artifacts bucket as `hr/<yyyy.mm.dd>.<run>.zip` and tags the commit `hr/<version>`, once the repository has the variables `AWS_REGION`, `ARTIFACTS_BUCKET` and `PUBLISH_ROLE_ARN` from the estate's bootstrap. Environments, each pinning the services' versions, live in the separate `estate-environments` repository, which deploys them.

## Deploying

Real environments deploy from the environments repository. For your own stack, from your working tree, you need AWS credentials, Terraform, the .NET SDK, the AWS CLI and jq. `deploy.sh` builds a package into `out/`, as CI does, then runs `terraform apply` in `DevEnvironment/`, which uses the unpacked package and passes on its arguments:

```
samples/deploy.sh -var name_prefix=adam
```

`name_prefix` names every resource, so each developer can have a stack of their own; `terraform destroy` in `samples/DevEnvironment` removes it. Its state is local.

A dev user pool stands in for the estate's sign-in, with the people in `dev_users`: by default Mia (`m1`), a manager, and Ed (`e1`), who reports to her. Each is a user named by their employee id with a generated password, and an employee record. To call the UI backend as Mia:

```
cd samples/DevEnvironment
password=$(terraform output -json dev_user_passwords | jq -r .m1)
token=$(aws cognito-idp initiate-auth --auth-flow USER_PASSWORD_AUTH \
  --client-id "$(terraform output -raw dev_ui_client_id)" \
  --auth-parameters "$(jq -n --arg password "$password" '{USERNAME: "m1", PASSWORD: $password}')" \
  --query AuthenticationResult.AccessToken --output text)

curl -X POST "$(terraform output -raw ui_backend_url)/EmployeeDetails" \
  -H "Authorization: Bearer $token" -d '{"EmployeeId":"e1"}'
```

`public_api_clients` lists the public API's third-party clients and their scopes. A client trades its id and secret for a token at the token URL:

```
client=$(terraform output -json public_api_clients | jq '.["example-partner"]')
token=$(curl -s -u "$(jq -r .client_id <<<"$client"):$(jq -r .client_secret <<<"$client")" \
  -d 'grant_type=client_credentials&scope=hr/employees.read' \
  "$(terraform output -raw public_api_token_url)" | jq -r .access_token)

curl "$(terraform output -raw public_api_url)/v1/employees/e1" -H "Authorization: Bearer $token"
```

The public API throttles all clients together; quotas per client would need API keys on a REST API, or WAF rules. Without `directory_events_topic_arn`, the queue worker subscribes to no other service's events.
