#!/usr/bin/env bash
# Builds the hr service's package for a version: its Terraform module and the zip of each function it
# deploys, as <out-dir>/hr-<version>.zip, with the same contents unpacked in <out-dir>/hr. An
# environment uses it as source = "s3::https://.../hr/<version>.zip//Infrastructure".
set -euo pipefail
version=${1:?usage: package.sh <version> <out-dir>}
mkdir -p "${2:?usage: package.sh <version> <out-dir>}"
out=$(cd "$2" && pwd)
cd "$(dirname "$0")"

package="$out/hr"
rm -rf "$package" "$out/publish" "$out/hr-$version.zip"
mkdir -p "$package/functions"

for project in UiBackend PublicApi ScheduledWorker QueueWorker; do
  dotnet publish "SampleService.$project" -c Release -r linux-arm64 --self-contained false \
    -o "$out/publish/SampleService.$project" --nologo -v quiet
  (cd "$out/publish/SampleService.$project" && zip -qrX "$package/functions/SampleService.$project.zip" .)
done

# The modules keep their places relative to each other, as their sources refer to each other by path.
for module in Infrastructure SampleService/Infrastructure SampleService.UiBackend/Infrastructure \
  SampleService.PublicApi/Infrastructure SampleService.ScheduledWorker/Infrastructure \
  SampleService.QueueWorker/Infrastructure; do
  mkdir -p "$package/$module"
  rsync -a --include='*/' --include='*.tf' --exclude='*' --prune-empty-dirs "$module/" "$package/$module/"
done

echo "$version" > "$package/VERSION"
(cd "$package" && zip -qrX "$out/hr-$version.zip" .)
echo "$out/hr-$version.zip"
