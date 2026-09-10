param(
    [string]$Project = "ops-service"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
$rootEnvironmentPath = Join-Path $repositoryRoot ".env"
$projectDirectory = Join-Path $repositoryRoot "projects\$Project"
$projectEnvironmentPath = Join-Path $projectDirectory ".env"
$backupPath = Join-Path $repositoryRoot ".env.before-split.local.bak"

if (-not (Test-Path -LiteralPath $rootEnvironmentPath)) {
    throw "Không tìm thấy cấu hình gốc: $rootEnvironmentPath"
}
if (-not (Test-Path -LiteralPath $projectDirectory)) {
    throw "Không tìm thấy dự án: $projectDirectory"
}

$commonGroups = [ordered]@{
    "TOOL-QC" = @(
        "TEST_ENV", "ACTIVE_PROJECT", "TEST_TAGS", "ARTIFACTS_DIR", "TEST_RESULTS_DIR",
        "ALLOW_PRODUCTION", "ALLOW_DESTRUCTIVE_TESTS"
    )
    "HTTP STUB" = @("HTTP_STUB_HOST", "HTTP_STUB_PORT", "HTTP_STUB_BASE_URL")
    "JMETER" = @("JMETER_HOME")
    "LOAD TEST" = @(
        "LOAD_MANIFEST", "LOAD_MODE", "LOAD_WORKERS", "LOAD_ITERATIONS",
        "LOAD_WARMUP_ITERATIONS", "LOAD_RESOURCE_POOL_SIZE", "LOAD_REQUEST_TIMEOUT_MS",
        "LOAD_SLOW_THRESHOLD_MS", "LOAD_RETRY_ENABLED", "LOAD_MAX_RETRY_ATTEMPTS",
        "LOAD_RANDOM_SEED", "LOAD_QUANTITY", "LOAD_BARRIER_TIMEOUT_MS",
        "LOAD_BATCH_COOLDOWN_MS", "LOAD_DIAGNOSTICS_ENABLED",
        "LOAD_POST_FAILURE_VERIFICATION_ENABLED", "LOAD_OUTPUT_JSONL",
        "LOAD_OUTPUT_MARKDOWN", "CONFIRM_LOAD_RUN"
    )
}

$projectGroups = [ordered]@{
    "API" = @("API_BASE_URL", "ONLINE_API_BASE_URL", "OFFLINE_API_BASE_URL", "API_TIMEOUT_SECONDS")
    "AUTHENTICATION" = @(
        "AUTH_TYPE", "AUTH_TOKEN", "AUTH_CLIENT_ID", "AUTH_CLIENT_SECRET",
        "AUTH_USERNAME", "AUTH_PASSWORD", "REDEMPTION_DEVICE_TOKEN"
    )
    "DATABASE" = @("DB_PROVIDER", "DB_CONNECTION_STRING", "ONLINE_DB_CONNECTION_STRING", "OFFLINE_DB_CONNECTION_STRING")
    "REDIS" = @("REDIS_CONNECTION_STRING")
    "ELASTICSEARCH" = @(
        "ELASTICSEARCH_URL", "ELASTICSEARCH_USERNAME", "ELASTICSEARCH_PASSWORD",
        "ELASTICSEARCH_INDEX_PREFIX", "ONLINE_ELASTICSEARCH_URL",
        "ONLINE_ELASTICSEARCH_USERNAME", "ONLINE_ELASTICSEARCH_PASSWORD",
        "OFFLINE_ELASTICSEARCH_URL", "OFFLINE_ELASTICSEARCH_USERNAME",
        "OFFLINE_ELASTICSEARCH_PASSWORD", "ELASTICSEARCH_INDEX_PREFIX_V1",
        "ELASTICSEARCH_INDEX_PREFIX_V2"
    )
    "MQTT" = @(
        "MQTT_HOST", "MQTT_PORT", "MQTT_USERNAME", "MQTT_PASSWORD", "MQTT_PREFIX",
        "MQTT_CLIENT_ID", "MQTT_TIMEOUT_MS", "MQTT_LOG_PUBLISHED_MESSAGES", "MQTT_TOPICS",
        "MQTT_AUTH_DB_HOST", "MQTT_AUTH_DB_PORT", "MQTT_AUTH_DB_DATABASE",
        "MQTT_AUTH_DB_USERNAME", "MQTT_AUTH_DB_PASSWORD"
    )
    "AWS" = @("AWS_REGION", "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "AWS_S3_BUCKET")
    "INTEGRATIONS" = @("PROFILE_BONUS_STUB_BASE_URL")
    "REDEMPTION LOAD" = @(
        "REDEMPTION_LOAD_MANIFEST", "REDEMPTION_LOAD_MODE", "REDEMPTION_LOAD_WORKERS",
        "REDEMPTION_LOAD_ITERATIONS", "REDEMPTION_LOAD_WARMUP_ITERATIONS",
        "REDEMPTION_LOAD_GIFT_POOL_SIZE", "REDEMPTION_LOAD_THINK_TIME_MS",
        "REDEMPTION_LOAD_TIMEOUT_MS", "REDEMPTION_LOAD_SLOW_THRESHOLD_MS",
        "REDEMPTION_LOAD_RETRY_ENABLED", "REDEMPTION_LOAD_MAX_RETRY_ATTEMPTS",
        "REDEMPTION_LOAD_RANDOM_SEED", "REDEMPTION_LOAD_QUANTITY",
        "REDEMPTION_LOAD_OUTPUT_JSONL", "REDEMPTION_LOAD_OUTPUT_MARKDOWN"
    )
    "REDEMPTION DIAGNOSTIC" = @(
        "REDEMPTION_DIAGNOSTIC_MANIFEST", "REDEMPTION_DIAGNOSTIC_SCENARIO",
        "REDEMPTION_DIAGNOSTIC_WORKERS", "REDEMPTION_DIAGNOSTIC_ITERATIONS",
        "REDEMPTION_DIAGNOSTIC_REQUEST_TIMEOUT_SECONDS",
        "REDEMPTION_DIAGNOSTIC_SLOW_THRESHOLD_MS",
        "REDEMPTION_DIAGNOSTIC_BETWEEN_REQUEST_DELAY_MS",
        "REDEMPTION_DIAGNOSTIC_TEST_RUN_ID", "REDEMPTION_DIAGNOSTIC_OUTPUT_DIRECTORY"
    )
    "ONSITE CUSTOMER LOAD" = @(
        "ONSITE_CUSTOMER_LOAD_MANIFEST", "ONSITE_CUSTOMER_LOAD_MODE",
        "ONSITE_CUSTOMER_LOAD_WORKERS", "ONSITE_CUSTOMER_LOAD_ITERATIONS",
        "ONSITE_CUSTOMER_LOAD_WARMUP_ITERATIONS", "ONSITE_CUSTOMER_LOAD_PAGE_SIZE",
        "ONSITE_CUSTOMER_LOAD_PAGE", "ONSITE_CUSTOMER_LOAD_SCHEDULE",
        "ONSITE_CUSTOMER_LOAD_TIMEOUT_MS", "ONSITE_CUSTOMER_LOAD_SLOW_THRESHOLD_MS",
        "ONSITE_CUSTOMER_LOAD_RETRY_ENABLED", "ONSITE_CUSTOMER_LOAD_MAX_RETRY_ATTEMPTS",
        "ONSITE_CUSTOMER_ADMIN_TOKEN_VARIABLE", "ONSITE_CUSTOMER_LOAD_OUTPUT_JSONL",
        "ONSITE_CUSTOMER_LOAD_OUTPUT_MARKDOWN"
    )
    "EVENT CUSTOMER LOAD" = @(
        "EVENT_CUSTOMER_LOAD_MANIFEST", "EVENT_CUSTOMER_LOAD_MODE",
        "EVENT_CUSTOMER_LOAD_WORKERS", "EVENT_CUSTOMER_LOAD_ITERATIONS",
        "EVENT_CUSTOMER_LOAD_PAGE_SIZE", "EVENT_CUSTOMER_LOAD_MAX_PAGES",
        "EVENT_CUSTOMER_LOAD_TIMEOUT_MS", "EVENT_CUSTOMER_LOAD_SLOW_THRESHOLD_MS",
        "EVENT_CUSTOMER_ADMIN_TOKEN_VARIABLE", "EVENT_CUSTOMER_LOAD_OUTPUT_JSONL",
        "EVENT_CUSTOMER_LOAD_OUTPUT_MARKDOWN"
    )
    "WRISTBAND ASSIGNMENT LOAD" = @(
        "WRISTBAND_ASSIGNMENT_MANIFEST", "WRISTBAND_ASSIGNMENT_MODE",
        "WRISTBAND_ASSIGNMENT_WORKERS", "WRISTBAND_ASSIGNMENT_TOTAL_REQUESTS",
        "WRISTBAND_ASSIGNMENT_TIMEOUT_MS", "WRISTBAND_ASSIGNMENT_SLOW_THRESHOLD_MS",
        "WRISTBAND_ASSIGNMENT_OUTPUT_JSONL", "WRISTBAND_ASSIGNMENT_OUTPUT_MARKDOWN"
    )
}

function Read-EnvironmentValues([string[]]$Paths) {
    $values = [ordered]@{}
    foreach ($path in $Paths) {
        if (-not (Test-Path -LiteralPath $path)) { continue }
        foreach ($raw in [IO.File]::ReadAllLines($path)) {
            $line = $raw.Trim()
            if ($line.Length -eq 0 -or $line.StartsWith("#")) { continue }
            $separator = $line.IndexOf("=")
            if ($separator -le 0) { continue }
            $values[$line.Substring(0, $separator).Trim()] = $line.Substring($separator + 1)
        }
    }
    return $values
}

function Write-EnvironmentFile([string]$Path, [Collections.Specialized.OrderedDictionary]$Groups, [Collections.Specialized.OrderedDictionary]$Values, [string[]]$AdditionalKeys) {
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($group in $Groups.GetEnumerator()) {
        if ($lines.Count -gt 0) { $lines.Add("") }
        $lines.Add("# $($group.Key)")
        foreach ($key in $group.Value) {
            $value = if ($Values.Contains($key)) { $Values[$key] } else { "" }
            $lines.Add("$key=$value")
        }
    }
    if ($AdditionalKeys.Count -gt 0) {
        $lines.Add("")
        $lines.Add("# OTHER")
        foreach ($key in $AdditionalKeys) { $lines.Add("$key=$($Values[$key])") }
    }
    [IO.File]::WriteAllLines($Path, $lines, [Text.UTF8Encoding]::new($false))
}

Copy-Item -LiteralPath $rootEnvironmentPath -Destination $backupPath -Force
$values = Read-EnvironmentValues @($rootEnvironmentPath, $projectEnvironmentPath)
$commonKeys = @($commonGroups.Values | ForEach-Object { $_ })
$projectKeys = @($projectGroups.Values | ForEach-Object { $_ })
$knownKeys = @($commonKeys + $projectKeys)
$additionalProjectKeys = @($values.Keys | Where-Object { $_ -notin $knownKeys } | Sort-Object)

Write-EnvironmentFile $rootEnvironmentPath $commonGroups $values @()
Write-EnvironmentFile $projectEnvironmentPath $projectGroups $values $additionalProjectKeys

Write-Host "Đã tách cấu hình môi trường." -ForegroundColor Green
Write-Host "Cấu hình chung: $rootEnvironmentPath"
Write-Host "Cấu hình dự án: $projectEnvironmentPath"
Write-Host "Bản sao lưu local: $backupPath"
Write-Host "Không có giá trị cấu hình nào được in ra màn hình."
