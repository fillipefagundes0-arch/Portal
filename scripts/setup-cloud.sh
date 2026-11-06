#!/usr/bin/env bash
set -euo pipefail
export DOTNET_CLI_HOME=/workspace/.dotnet-cli
export NUGET_PACKAGES=/workspace/.nuget/packages
export PATH="/workspace/.dotnet:$PATH"
if ! command -v dotnet >/dev/null; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/portal-dotnet-install.sh
  bash /tmp/portal-dotnet-install.sh --version 10.0.403 --install-dir /workspace/.dotnet
fi
cd /workspace/Portal
mkdir -p Portal/App_Data
dotnet restore tests/Portal.Tests.csproj
dotnet build Portal/Portal.csproj --no-restore
dotnet run --project tests/Portal.Tests.csproj --no-restore
