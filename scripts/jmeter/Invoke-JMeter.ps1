param(
    [Parameter(Mandatory)]
    [string]$TestPlan,
    [string]$PropertiesFile,
    [string]$OutputDirectory,
    [string]$JMeterHome = $env:JMETER_HOME,
    [hashtable]$Properties = @{},
    [switch]$NoHtmlReport
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
$testPlanPath = (Resolve-Path -LiteralPath $TestPlan).Path
if ([string]::IsNullOrWhiteSpace($JMeterHome)) {
    throw "Thiếu JMeterHome. Truyền -JMeterHome hoặc thiết lập JMETER_HOME."
}

$jmeterJar = Join-Path $JMeterHome "bin\ApacheJMeter.jar"
if (-not (Test-Path -LiteralPath $jmeterJar)) { throw "Không tìm thấy JMeter: $jmeterJar" }
$java = (Get-Command java -ErrorAction Stop).Source
$planName = [IO.Path]::GetFileNameWithoutExtension($testPlanPath)
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $OutputDirectory = Join-Path $repositoryRoot ".artifacts\jmeter\$planName-$timestamp"
}

$resultRoot = [IO.Path]::GetFullPath($OutputDirectory)
$jtl = Join-Path $resultRoot "results.jtl"
$html = Join-Path $resultRoot "html"
$log = Join-Path $resultRoot "jmeter.log"
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null

$arguments = [Collections.Generic.List[string]]::new()
foreach ($argument in @("-jar", $jmeterJar, "-n", "-t", $testPlanPath, "-l", $jtl, "-j", $log)) {
    $arguments.Add([string]$argument)
}
if (-not [string]::IsNullOrWhiteSpace($PropertiesFile)) {
    $arguments.Add("-q")
    $arguments.Add((Resolve-Path -LiteralPath $PropertiesFile).Path)
}
if (-not $NoHtmlReport) {
    foreach ($argument in @("-e", "-o", $html)) { $arguments.Add([string]$argument) }
}

$saveProperties = @{
    "jmeter.save.saveservice.output_format" = "csv"
    "jmeter.save.saveservice.latency" = "true"
    "jmeter.save.saveservice.connect_time" = "true"
    "jmeter.save.saveservice.response_code" = "true"
    "jmeter.save.saveservice.successful" = "true"
    "jmeter.save.saveservice.label" = "true"
    "jmeter.save.saveservice.thread_counts" = "true"
    "jmeter.save.saveservice.timestamp_format" = "ms"
}
foreach ($entry in ($saveProperties + $Properties).GetEnumerator() | Sort-Object Key) {
    $arguments.Add("-J$($entry.Key)=$($entry.Value)")
}

& $java @arguments
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) { throw "JMeter thất bại, exit code $exitCode. Log: $log" }
if (-not (Test-Path -LiteralPath $jtl)) { throw "JMeter không tạo JTL: $jtl" }
if (-not $NoHtmlReport -and -not (Test-Path -LiteralPath (Join-Path $html "index.html"))) {
    throw "JMeter không tạo HTML report: $html"
}

Write-Host "JTL: $jtl" -ForegroundColor Green
if (-not $NoHtmlReport) { Write-Host "HTML: $html\index.html" -ForegroundColor Green }
Write-Host "Log: $log"
