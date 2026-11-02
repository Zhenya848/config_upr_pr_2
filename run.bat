@echo off
set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=examples\config-a.json"
dotnet run --project src\DependencyGraph\DependencyGraph.csproj -- --config "%CONFIG%"
