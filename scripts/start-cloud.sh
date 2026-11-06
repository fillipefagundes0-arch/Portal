#!/usr/bin/env bash
set -euo pipefail
export DOTNET_CLI_HOME=/workspace/.dotnet-cli
export NUGET_PACKAGES=/workspace/.nuget/packages
export PATH="/workspace/.dotnet:$PATH"
cd /workspace/Portal
mkdir -p Portal/App_Data
export ASPNETCORE_ENVIRONMENT=Development
export DatabaseProvider=Sqlite
export ConnectionStrings__Portal='Data Source=App_Data/portal.db'
export LocalDevelopmentIdentity=true
export BootstrapAdmin='LOCAL\dev'
export Logging__LogLevel__Default=Warning
exec dotnet run --no-build --project Portal --urls http://127.0.0.1:5080
