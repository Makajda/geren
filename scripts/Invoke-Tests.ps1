param(
    [string]$Configuration = "Debug",
    [switch]$NoBuild,
    [switch]$NoRestore,
    [string]$Filter = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$testsRoot = Join-Path $repoRoot "tests"

if (-not (Test-Path $testsRoot)) {
    throw "Tests directory not found: $testsRoot"
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_UI_LANGUAGE = "en-US"
$env:DOTNET_CLI_HOME = (Join-Path $repoRoot ".dotnet-home")
$env:NUGET_PACKAGES = (Join-Path $repoRoot ".nuget\packages")
$env:DOTNET_MULTILEVEL_LOOKUP = "0"

New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME -Force | Out-Null
New-Item -ItemType Directory -Path $env:NUGET_PACKAGES -Force | Out-Null

$projects = Get-ChildItem -Path $testsRoot -Recurse -Filter *.csproj -File |
    Sort-Object FullName

if ($projects.Count -eq 0) {
    Write-Host "No test projects found under '$testsRoot'."
    exit 0
}

$baseArgs = @("test", "-c", $Configuration)
if ($NoBuild) { $baseArgs += "--no-build" }
if ($NoRestore) { $baseArgs += "--no-restore" }
if (-not [string]::IsNullOrWhiteSpace($Filter)) { $baseArgs += @("--filter", $Filter) }

$total = 0
foreach ($p in $projects) {
    $total++
    Write-Host ""
    Write-Host "==> [$total/$($projects.Count)] dotnet test $($p.FullName)"

    & dotnet @baseArgs $p.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed for: $($p.FullName)"
    }
}

Write-Host ""
Write-Host "All tests passed ($total project(s))."
