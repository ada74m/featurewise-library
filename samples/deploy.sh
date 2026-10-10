#!/usr/bin/env bash
# Publishes each function, then applies the Terraform. Arguments go to terraform apply, such as
# -var name_prefix=adam for a stack of your own.
set -euo pipefail
cd "$(dirname "$0")"

rm -rf artifacts
for project in UiBackend PublicApi ScheduledWorker QueueWorker; do
  dotnet publish "SampleService.$project" -c Release -r linux-arm64 --self-contained false \
    -o "artifacts/SampleService.$project" --nologo -v quiet
done

cd Infrastructure
terraform init -input=false
terraform apply "$@"
