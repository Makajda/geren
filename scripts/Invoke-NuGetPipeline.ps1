param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts/nuget",
    [string]$Version = "",
    [switch]$EnablePackageAnalysis,
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ProjectPackageMetadata {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath
    )

    [xml]$projectXml = Get-Content -Path $ProjectPath

    $packageId = $null
    $targetFramework = $null

    foreach ($propertyGroup in $projectXml.Project.PropertyGroup) {
        if ([string]::IsNullOrWhiteSpace($packageId) -and -not [string]::IsNullOrWhiteSpace($propertyGroup.PackageId)) {
            $packageId = $propertyGroup.PackageId
        }

        if ([string]::IsNullOrWhiteSpace($targetFramework) -and -not [string]::IsNullOrWhiteSpace($propertyGroup.TargetFramework)) {
            $targetFramework = $propertyGroup.TargetFramework
        }
    }

    if ([string]::IsNullOrWhiteSpace($packageId)) {
        throw "Project '$ProjectPath' does not define PackageId."
    }

    [pscustomobject]@{
        ProjectPath = $ProjectPath
        PackageId = $packageId
        TargetFramework = $targetFramework
    }
}

function Get-LatestPackageForId {
    param(
        [Parameter(Mandatory)]
        [string]$PackageId,

        [Parameter(Mandatory)]
        [string]$PackageDirectory
    )

    $package = Get-ChildItem -Path $PackageDirectory -Filter *.nupkg |
        Where-Object {
            $_.Name -notlike "*.symbols.nupkg" -and
            $_.BaseName.StartsWith("$PackageId.", [System.StringComparison]::OrdinalIgnoreCase)
        } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $package) {
        throw "Package '$PackageId' was not produced in '$PackageDirectory'."
    }

    return $package
}

function Test-PackageEntries {
    param(
        [Parameter(Mandatory)]
        [string]$PackagePath,

        [Parameter(Mandatory)]
        [string]$PackageId,

        [Parameter(Mandatory)]
        [string[]]$RequiredEntries
    )

    Write-Host "Validating package '$PackageId': $PackagePath"

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })

        $missingEntries = @()
        foreach ($entry in $RequiredEntries) {
            if (-not ($entries -contains $entry)) {
                $missingEntries += $entry
            }
        }

        if ($missingEntries.Count -gt 0) {
            throw "Package '$PackageId' is missing required entries: $($missingEntries -join ', ')"
        }

        Write-Host "Validated entries:"
        $RequiredEntries | Sort-Object | ForEach-Object { Write-Host "  - $_" }
    }
    finally {
        $archive.Dispose()
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

if ([System.IO.Path]::IsPathRooted($OutputDir)) {
    $resolvedOutputDir = $OutputDir
}
else {
    $resolvedOutputDir = Join-Path $repoRoot $OutputDir
}

New-Item -ItemType Directory -Path $resolvedOutputDir -Force | Out-Null

$packages = @(
    [pscustomobject]@{
        Metadata = Get-ProjectPackageMetadata -ProjectPath (Join-Path $repoRoot "src/Geren/Geren.csproj")
        RequiredEntries = @(
            "analyzers/dotnet/cs/Geren.dll",
            "analyzers/dotnet/cs/Microsoft.OpenApi.dll",
            "README.md",
            "LICENSE.txt"
        )
    },
    [pscustomobject]@{
        Metadata = Get-ProjectPackageMetadata -ProjectPath (Join-Path $repoRoot "src/Geren.Server/Geren.Server.csproj")
        RequiredEntries = @(
            "lib/net10.0/Geren.Server.dll",
            "README.md",
            "LICENSE.txt"
        )
    }
)

foreach ($package in $packages) {
    $metadata = $package.Metadata
    if (-not $NoBuild) {
        $buildArgs = @(
            "build", $metadata.ProjectPath,
            "-c", $Configuration,
            "-nologo",
            "-p:ContinuousIntegrationBuild=true",
            "-p:IsPackable=true"
        )

        if (-not [string]::IsNullOrWhiteSpace($Version)) {
            $buildArgs += "-p:Version=$Version"
        }

        Write-Host "Building '$($metadata.PackageId)' from '$($metadata.ProjectPath)'..."
        & dotnet @buildArgs
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for '$($metadata.PackageId)' with exit code $LASTEXITCODE."
        }
    }

    $packArgs = @(
        "pack", $metadata.ProjectPath,
        "-c", $Configuration,
        "-o", $resolvedOutputDir,
        "-nologo",
        "-p:ContinuousIntegrationBuild=true",
        "-p:IsPackable=true"
    )

    if ($NoBuild) {
        $packArgs += "--no-build"
    }

    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $packArgs += "-p:Version=$Version"
    }

    if ($EnablePackageAnalysis) {
        $packArgs += "-p:NoPackageAnalysis=false"
        $packArgs += "-p:TreatWarningsAsErrors=true"
        $packArgs += "-p:WarningsNotAsErrors=NU5128"
    }

    Write-Host "Packing '$($metadata.PackageId)' from '$($metadata.ProjectPath)'..."
    & dotnet @packArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed for '$($metadata.PackageId)' with exit code $LASTEXITCODE."
    }
}

foreach ($package in $packages) {
    $metadata = $package.Metadata
    $packageFile = Get-LatestPackageForId -PackageId $metadata.PackageId -PackageDirectory $resolvedOutputDir

    Test-PackageEntries `
        -PackagePath $packageFile.FullName `
        -PackageId $metadata.PackageId `
        -RequiredEntries $package.RequiredEntries
}

Write-Host "NuGet pipeline completed successfully."
