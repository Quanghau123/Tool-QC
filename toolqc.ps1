param(
    [Parameter(Position=0, Mandatory=$true)][string]$Area,
    [Parameter(Position=1)][string]$Command,
    [Parameter(Position=2)][string]$Project,
    [Parameter(Position=3)][string]$Name,
    [switch]$Background,
    [switch]$NoBrowser,
    [int]$Port = 5080
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if ($Area -eq 'ui') {
    $url = "http://127.0.0.1:$Port"
    if (-not $NoBrowser) {
        Start-Job -ScriptBlock { param($target) Start-Sleep -Seconds 2; Start-Process $target } -ArgumentList $url | Out-Null
    }
    & dotnet run --project (Join-Path $root 'runner\AutoTest.Web\AutoTest.Web.csproj') -c Release -- --url $url
    exit $LASTEXITCODE
}
if ($Area -eq 'generate') {
    if ([string]::IsNullOrWhiteSpace($Command) -or [string]::IsNullOrWhiteSpace($Project)) { throw 'Cách dùng: .\toolqc.ps1 generate <project> <source-path>' }
    & dotnet run --project (Join-Path $root 'runner\AutoTest.Generator\AutoTest.Generator.csproj') -c Release -- --project $Command --source $Project
    exit $LASTEXITCODE
}
if ($Area -eq 'coverage') {
    if ([string]::IsNullOrWhiteSpace($Command)) { throw 'Cách dùng: .\toolqc.ps1 coverage <coverage.cobertura.xml> [output.json]' }
    $arguments = @('run', '--project', (Join-Path $root 'runner\AutoTest.Generator\AutoTest.Generator.csproj'), '-c', 'Release', '--', '--coverage-report', $Command)
    if (-not [string]::IsNullOrWhiteSpace($Project)) { $arguments += @('--coverage-output', $Project) }
    & dotnet @arguments
    exit $LASTEXITCODE
}
if ($Area -eq 'test') {
    $selectedProject = $Command
    if ([string]::IsNullOrWhiteSpace($selectedProject)) {
        $rootEnv = Join-Path $root '.env'
        if (Test-Path -LiteralPath $rootEnv) {
            $activeProjectLine = Get-Content -LiteralPath $rootEnv | Where-Object { $_ -match '^\s*ACTIVE_PROJECT\s*=' } | Select-Object -First 1
            if ($activeProjectLine) { $selectedProject = ($activeProjectLine -split '=', 2)[1].Trim().Trim('"') }
        }
    }
    if ([string]::IsNullOrWhiteSpace($selectedProject)) { $selectedProject = 'ops-service' }
    $arguments = @('run', '--project', (Join-Path $root 'runner\AutoTest.Runner\AutoTest.Runner.csproj'), '-c', 'Release', '--', '--project', $selectedProject)
    if (-not [string]::IsNullOrWhiteSpace($Project)) { $arguments += @('--tags', $Project) }
    & dotnet @arguments
    exit $LASTEXITCODE
}
if ($Area -eq 'build') {
    & dotnet build (Join-Path $root 'ApiAutoTest.sln') -c Release
    exit $LASTEXITCODE
}
if ($Area -ne 'integration') { throw "Chức năng chưa hỗ trợ: $Area. Dùng: ui, test, build, generate, coverage hoặc integration." }
if ($Command -ne 'list' -and ([string]::IsNullOrWhiteSpace($Project) -or [string]::IsNullOrWhiteSpace($Name))) {
    throw 'Cách dùng: .\toolqc.ps1 integration <start|status|stop> <project> <integration> [--Background]'
}

$dll = Join-Path $root 'runner\AutoTest.HttpStub\bin\Debug\net8.0\AutoTest.HttpStub.dll'
if (!(Test-Path -LiteralPath $dll)) {
    dotnet build (Join-Path $root 'runner\AutoTest.HttpStub\AutoTest.HttpStub.csproj')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$arguments = @($dll, $Command)
if ($Command -ne 'list') { $arguments += @($Project, $Name) }
if ($Background) { $arguments += '--background' }
& dotnet @arguments
exit $LASTEXITCODE
