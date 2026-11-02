#!/usr/bin/env sh
set -eu
CONFIG="${1:-examples/config-a.json}"
dotnet run --project src/DependencyGraph/DependencyGraph.csproj -- --config "$CONFIG"
