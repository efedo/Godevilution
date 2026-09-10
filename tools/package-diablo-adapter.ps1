<#
.SYNOPSIS
    Builds and stages the production Diablo host-adapter package.

.DESCRIPTION
    Produces one hash-verified game-host-adapter.json package directory. The
    package contains the adapter entry assembly and its private Diablo closure;
    shared Godevilution contracts remain outside the package and are pinned by
    the optional sharedContracts manifest map.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$PackageVersion = '0.1.0',
    [string]$ArchivePath,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$packageProject = Join-Path $repositoryRoot 'server/src/Devilution.Server.Diablo.Package/Devilution.Server.Diablo.Package.csproj'
$serverProject = Join-Path $repositoryRoot 'server/src/Devilution.Server/Devilution.Server.csproj'
$buildOutput = Join-Path $repositoryRoot "server/src/Devilution.Server.Diablo.Package/bin/$Configuration/net8.0"
$serverOutput = Join-Path $repositoryRoot "server/src/Devilution.Server/bin/$Configuration/net10.0"
$packageRoot = [IO.Path]::GetFullPath($OutputRoot)

if (-not (Test-Path -LiteralPath $packageProject -PathType Leaf))
    { throw "The Diablo package project was not found at $packageProject." }
if (-not (Test-Path -LiteralPath $serverProject -PathType Leaf))
    { throw "The authoritative server project was not found at $serverProject." }

if (Test-Path -LiteralPath $packageRoot) {
    if (-not (Get-ChildItem -LiteralPath $packageRoot -Force | Select-Object -First 1)) {
        # An existing empty directory is a valid staging target.
    } else {
        throw "Refusing to overwrite non-empty package directory '$packageRoot'."
    }
} else {
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
}

if (-not $NoBuild) {
    & dotnet build $packageProject --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0)
        { throw "The Diablo package build failed with exit code $LASTEXITCODE." }
    & dotnet build $serverProject --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0)
        { throw "The authoritative server build failed with exit code $LASTEXITCODE." }
}

if (-not (Test-Path -LiteralPath $buildOutput -PathType Container))
    { throw "The package build output was not found at $buildOutput." }
if (-not (Test-Path -LiteralPath $serverOutput -PathType Container))
    { throw "The server build output was not found at $serverOutput." }

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf))
        { throw "Required package file was not found at $Path." }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Copy-PackageFile([string]$Name) {
    $source = Join-Path $buildOutput $Name
    $destination = Join-Path $packageRoot $Name
    Copy-Item -LiteralPath $source -Destination $destination
    return $destination
}

$entryPath = Copy-PackageFile 'Godevilution.Server.Diablo.Package.dll'
$privateDependencyNames = @(
    'Devilution.Server.Diablo.dll',
    'Devilution.Diablo.dll',
    'Devilution.Domain.dll'
)
$privateDependencies = foreach ($name in $privateDependencyNames) {
    $path = Copy-PackageFile $name
    [ordered]@{
        assembly = $name
        assemblySha256 = Get-Sha256 $path
    }
}

$sharedContractNames = @(
    'Devilution.Data.dll',
    'Devilution.Engine.dll',
    'Devilution.Server.Host.dll',
    'Devilution.Protocol.dll',
    'Google.Protobuf.dll'
)
$sharedContracts = [ordered]@{}
foreach ($name in $sharedContractNames) {
    $sourceRoot = if ($name -eq 'Google.Protobuf.dll') { $serverOutput } else { $buildOutput }
    $path = Join-Path $sourceRoot $name
    $assemblyName = [IO.Path]::GetFileNameWithoutExtension($name)
    $sharedContracts[$assemblyName] = Get-Sha256 $path
}

$manifest = [ordered]@{
    formatVersion = 1
    id = 'godevilution.diablo.package'
    version = $PackageVersion
    assembly = 'Godevilution.Server.Diablo.Package.dll'
    assemblySha256 = Get-Sha256 $entryPath
    factoryType = 'Godevilution.Server.Diablo.Package.PackagedDiabloAuthoritativeGameHostAdapterFactory'
    dependencies = @($privateDependencies)
    sharedContracts = $sharedContracts
}
$manifestPath = Join-Path $packageRoot 'game-host-adapter.json'
$manifestJson = $manifest | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText(
    $manifestPath,
    $manifestJson,
    (New-Object Text.UTF8Encoding($false)))

Write-Host "Staged Diablo adapter package: $packageRoot"
Write-Host "Package ID: $($manifest.id)"
Write-Host "Package version: $($manifest.version)"
Write-Host "Manifest: $manifestPath"

if ($ArchivePath) {
    $archive = [IO.Path]::GetFullPath($ArchivePath)
    if ([IO.Path]::GetExtension($archive) -ine '.zip')
        { throw "Release archive '$archive' must use the .zip extension." }
    $packagePrefix = $packageRoot.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($archive.StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase))
        { throw "Archive path '$archive' must be outside the staged package directory." }
    if (Test-Path -LiteralPath $archive)
        { throw "Refusing to overwrite existing release archive '$archive'." }
    $archiveParent = Split-Path -Parent $archive
    if ($archiveParent)
        { New-Item -ItemType Directory -Path $archiveParent -Force | Out-Null }
    Compress-Archive -Path (Join-Path $packageRoot '*') `
        -DestinationPath $archive -CompressionLevel Optimal
    Write-Host "Archive: $archive"
}
