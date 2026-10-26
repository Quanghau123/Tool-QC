$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet_cli_home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

function Require-Command([string]$Name, [string]$InstallHint) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Missing prerequisite '$Name'. $InstallHint"
    }
}

function Require-DotNetSdk {
    $version = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $version.StartsWith('8.')) {
        throw "Tool-QC requires a .NET 8 SDK selected by global.json. Install a compatible .NET 8 SDK, then run this command again."
    }

    Write-Host "Using .NET SDK $version." -ForegroundColor DarkGray
}

function Require-Node {
    $version = (& node --version).Trim().TrimStart('v')
    $major = [int](($version -split '\.')[0])
    if ($LASTEXITCODE -ne 0 -or $major -lt 18) {
        throw "Tool-QC requires Node.js 18 or later. Install a supported Node.js LTS version, then run this command again."
    }

    Write-Host "Using Node.js $version." -ForegroundColor DarkGray
}

function Restore-NodeDependencies {
    & npm ls --omit=optional --depth=0 *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Host 'Node dependencies are already ready.' -ForegroundColor DarkGray
        return
    }

    Write-Host 'Restoring Node dependencies from package-lock.json...' -ForegroundColor Cyan
    & npm ci
    if ($LASTEXITCODE -ne 0) {
        throw 'Node dependency restore failed.'
    }
}

Require-Command 'git' 'Install Git, then run this command again.'
Require-Command 'dotnet' 'Install the .NET 8 SDK selected by global.json, then run this command again.'
Require-Command 'node' 'Install Node.js 18 or later, then run this command again.'
Require-Command 'npm' 'Install Node.js with npm, then run this command again.'
Require-DotNetSdk
Require-Node

Write-Host 'Restoring NuGet packages...' -ForegroundColor Cyan
& dotnet restore .\ApiAutoTest.sln
if ($LASTEXITCODE -ne 0) {
    throw 'NuGet package restore failed.'
}

Restore-NodeDependencies

Write-Host 'Ensuring the Playwright Chromium browser is available...' -ForegroundColor Cyan
& npx playwright install chromium
if ($LASTEXITCODE -ne 0) {
    throw 'Playwright Chromium setup failed.'
}

Write-Host 'Tool-QC setup is complete. Copy .env.example to .env and configure only your local/test environment before running npm start.' -ForegroundColor Green
