#!/usr/bin/env bash
# Runs one share of the backend tests, so CI can run them on several machines at once.
#
#   tests/run-shard.sh <shard> <shards>      e.g. tests/run-shard.sh 2 4
#
# Every test class of ACI.WebApi.Tests goes to exactly one shard, picked by a stable hash
# of its name: a class added later lands somewhere without anyone listing it. Shard 1 also
# runs ACI.Application.Tests, which takes seconds. The build must already be done
# (Release), as CI does before this step.
set -euo pipefail
shard=${1:?shard number, from 1}
shards=${2:?number of shards}
cd "$(dirname "$0")/.."

api=tests/ACI.WebApi.Tests/ACI.WebApi.Tests.csproj
# "ACI.WebApi.Tests.Some.Class.Method(args)" → "ACI.WebApi.Tests.Some.Class", once each.
classes=$(dotnet test "$api" -c Release --no-build --list-tests \
  | sed -n 's/^    //p' | sed 's/(.*$//' | sed 's/\.[^.]*$//' | sort -u)
[ -n "$classes" ] || { echo "No tests were listed." >&2; exit 1; }

filter=""
mine=0
total=0
for class in $classes; do
  total=$((total + 1))
  hash=$(printf '%s' "$class" | cksum | cut -d' ' -f1)
  if [ $((hash % shards + 1)) -eq "$shard" ]; then
    mine=$((mine + 1))
    # The trailing dot keeps "LeadTests" from also matching "LeadTestsExtra".
    filter="${filter:+$filter|}FullyQualifiedName~$class."
  fi
done
echo "Shard $shard of $shards: $mine of $total test classes."

status=0
if [ "$shard" -eq 1 ]; then
  dotnet test tests/ACI.Application.Tests/ACI.Application.Tests.csproj -c Release --no-build || status=$?
fi
if [ -n "$filter" ]; then
  dotnet test "$api" -c Release --no-build --filter "$filter" || status=$?
fi
exit $status
