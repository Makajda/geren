param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts/nuget",
    [string]$Version = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "src/Geren/Geren.csproj"

if ([System.IO.Path]::IsPathRooted($OutputDir)) {
    $resolvedOutputDir = $OutputDir
}
else {
    $resolvedOutputDir = (Join-Path $repoRoot $OutputDir)
}

New-Item -ItemType Directory -Path $resolvedOutputDir -Force | Out-Null

$packArgs = @(
    "pack", $projectPath,
    "-c", $Configuration,
    "-o", $resolvedOutputDir,
    "-nologo",
    "-p:ContinuousIntegrationBuild=true",
    "-p:IsPackable=true"
)

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $packArgs += "-p:Version=$Version"
}

Write-Host "Packing NuGet package..."
& dotnet @packArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed with exit code $LASTEXITCODE."
}

$package = Get-ChildItem -Path $resolvedOutputDir -Filter *.nupkg |
    Where-Object { $_.Name -notlike "*.symbols.nupkg" } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $package) {
    throw "Package was not produced in '$resolvedOutputDir'."
}

Write-Host "Validating package: $($package.FullName)"

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })

    $requiredPackageEntries = @(
        "analyzers/dotnet/cs/Geren.dll",
        "analyzers/dotnet/cs/Microsoft.OpenApi.Readers.dll",
        "analyzers/dotnet/cs/Microsoft.OpenApi.dll",
        "analyzers/dotnet/cs/SharpYaml.dll",
        "analyzers/dotnet/cs/System.Text.Json.dll",
        "README.md"
    )

    $missingEntries = @()
    foreach ($entry in $requiredPackageEntries) {
        if (-not ($entries -contains $entry)) {
            $missingEntries += $entry
        }
    }

    if ($missingEntries.Count -gt 0) {
        throw "Package is missing required entries: $($missingEntries -join ', ')"
    }

    $analyzerDllEntries = @(
        $entries |
            Where-Object {
                $_.StartsWith("analyzers/dotnet/cs/", [System.StringComparison]::OrdinalIgnoreCase) -and
                $_.EndsWith(".dll", [System.StringComparison]::OrdinalIgnoreCase)
            }
    )

    $expectedAnalyzerDllEntries = @(
        $requiredPackageEntries |
            Where-Object { $_.StartsWith("analyzers/dotnet/cs/", [System.StringComparison]::OrdinalIgnoreCase) -and $_.EndsWith(".dll", [System.StringComparison]::OrdinalIgnoreCase) }
    )

    $missingAnalyzerDependencies = @()
    foreach ($dllEntry in $expectedAnalyzerDllEntries) {
        if (-not ($analyzerDllEntries -contains $dllEntry)) {
            $missingAnalyzerDependencies += $dllEntry
        }
    }

    if ($missingAnalyzerDependencies.Count -gt 0) {
        throw "Analyzer dependency validation failed. Missing analyzer dlls: $($missingAnalyzerDependencies -join ', ')"
    }

    Write-Host "Analyzer files in package:"
    $analyzerDllEntries | Sort-Object | ForEach-Object { Write-Host "  - $_" }
}
finally {
    $archive.Dispose()
}

Write-Host "NuGet pipeline completed successfully."
