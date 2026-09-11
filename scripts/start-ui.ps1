$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_cli_home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

# Chỉ dừng đúng Web host của Tool-QC để tránh file output bị khóa khi chạy lại npm start.
$webExecutable = [System.IO.Path]::GetFullPath(
    (Join-Path $root 'runner\AutoTest.Web\bin\Release\net8.0\AutoTest.Web.exe')
)
$existingHosts = Get-Process -Name 'AutoTest.Web' -ErrorAction SilentlyContinue |
    Where-Object {
        -not [string]::IsNullOrWhiteSpace($_.Path) -and
        [System.IO.Path]::GetFullPath($_.Path).Equals(
            $webExecutable,
            [System.StringComparison]::OrdinalIgnoreCase
        )
    }

foreach ($hostProcess in $existingHosts) {
    Write-Host "Đang dừng Tool-QC UI cũ (PID $($hostProcess.Id))..." -ForegroundColor Yellow
    Stop-Process -Id $hostProcess.Id -Force -ErrorAction Stop
}

if ($existingHosts) {
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    while ((Test-Path -LiteralPath $webExecutable) -and [DateTime]::UtcNow -lt $deadline) {
        try {
            $stream = [System.IO.File]::Open($webExecutable, 'Open', 'ReadWrite', 'None')
            $stream.Dispose()
            break
        }
        catch {
            Start-Sleep -Milliseconds 200
        }
    }
}

Write-Host "Đang chuẩn bị generator và test runner..." -ForegroundColor Cyan
dotnet build .\runner\AutoTest.Generator\AutoTest.Generator.csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Không thể build testcase generator." }
$runnerDll = Join-Path $root 'runner\AutoTest.Runner\bin\Release\net8.0\AutoTest.Runner.dll'
$runnerSource = Join-Path $root 'runner\AutoTest.Runner\Program.cs'
if (-not (Test-Path -LiteralPath $runnerDll) -or
    (Get-Item -LiteralPath $runnerSource).LastWriteTimeUtc -gt (Get-Item -LiteralPath $runnerDll).LastWriteTimeUtc) {
    dotnet build .\runner\AutoTest.Runner\AutoTest.Runner.csproj `
        -c Release `
        --no-restore `
        -p:BuildProjectReferences=false `
        -p:NuGetAudit=false `
        -m:1 `
        -nr:false
    if ($LASTEXITCODE -ne 0) { throw "Cannot build the Tool-QC test runner." }
}
dotnet run --project .\runner\AutoTest.Web\AutoTest.Web.csproj -c Release --no-restore
exit $LASTEXITCODE
