param(
    [Parameter(Position=0, Mandatory=$true)][string]$Area,
    [Parameter(Position=1)][string]$Command,
    [Parameter(Position=2)][string]$Project,
    [Parameter(Position=3)][string]$Name,
    [switch]$Background
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
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
if ($Area -ne 'integration') { throw "Chức năng chưa hỗ trợ: $Area. Dùng: test, build hoặc integration." }
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
