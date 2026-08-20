param(
    [Parameter(Position=0, Mandatory=$true)][string]$Area,
    [Parameter(Position=1, Mandatory=$true)][string]$Command,
    [Parameter(Position=2)][string]$Project,
    [Parameter(Position=3)][string]$Name,
    [switch]$Background
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if ($Area -ne 'integration') { throw "Chức năng chưa hỗ trợ: $Area" }
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
