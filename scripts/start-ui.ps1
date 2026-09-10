$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_cli_home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
dotnet run --project .\runner\AutoTest.Web\AutoTest.Web.csproj -c Release
exit $LASTEXITCODE
