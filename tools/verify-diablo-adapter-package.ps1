<#
.SYNOPSIS
    Verifies a staged Diablo host-adapter package without loading it.

.DESCRIPTION
    Checks the release manifest, canonical package-relative paths, entry and
    private-dependency hashes, and (when supplied) shared contract hashes
    against a build output directory. This is intentionally a static release
    gate; the authoritative host loader remains the final runtime check.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,
    [string]$SharedContractRoot,
    [string]$ExpectedPackageId,
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$ExpectedPackageVersion
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PackageRoot)
$manifestPath = Join-Path $root 'game-host-adapter.json'

if (-not (Test-Path -LiteralPath $root -PathType Container))
    { throw "Package directory '$root' does not exist." }
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf))
    { throw "Package manifest '$manifestPath' does not exist." }

try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
} catch {
    throw "Package manifest '$manifestPath' is not valid UTF-8 JSON: $($_.Exception.Message)"
}

$invalidManifest = $null -eq $manifest
$invalidManifest = $invalidManifest -or $manifest.formatVersion -ne 1
$invalidManifest = $invalidManifest -or [string]::IsNullOrWhiteSpace([string]$manifest.id)
$invalidManifest = $invalidManifest -or [string]::IsNullOrWhiteSpace([string]$manifest.version)
$invalidManifest = $invalidManifest -or [string]::IsNullOrWhiteSpace([string]$manifest.assembly)
$invalidManifest = $invalidManifest -or [string]::IsNullOrWhiteSpace([string]$manifest.assemblySha256)
$invalidManifest = $invalidManifest -or [string]::IsNullOrWhiteSpace([string]$manifest.factoryType)
if ($invalidManifest) {
    throw 'Package manifest requires formatVersion 1, id, version, assembly, assemblySha256, and factoryType.'
}
if ($ExpectedPackageId -and $manifest.id -cne $ExpectedPackageId)
    { throw "Expected package ID '$ExpectedPackageId', found '$($manifest.id)'." }
if ($ExpectedPackageVersion -and $manifest.version -cne $ExpectedPackageVersion)
    { throw "Expected package version '$ExpectedPackageVersion', found '$($manifest.version)'." }

function Resolve-CanonicalPackageFile([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath))
        { throw 'Package paths cannot be empty.' }
    $normalized = $RelativePath.Replace('\', '/')
    $segments = $normalized.Split('/')
    $invalidPath = [IO.Path]::IsPathRooted($normalized)
    $invalidPath = $invalidPath -or $normalized.Contains(':')
    $invalidPath = $invalidPath -or $normalized.StartsWith('/')
    $invalidPath = $invalidPath -or $normalized.EndsWith('/')
    $invalidPath = $invalidPath -or $normalized.Contains('//')
    $invalidPath = $invalidPath -or [bool]($segments | Where-Object { $_ -eq '' -or $_ -eq '.' -or $_ -eq '..' })
    if ($invalidPath) {
        throw "Package path '$RelativePath' is not canonical and package-relative."
    }
    $resolved = [IO.Path]::GetFullPath((Join-Path $root $normalized))
    $containedRoot = $root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($containedRoot, [StringComparison]::OrdinalIgnoreCase))
        { throw "Package path '$RelativePath' escapes the package directory." }
    if ([IO.Path]::GetExtension($resolved) -ine '.dll')
        { throw "Package path '$RelativePath' must name a .dll file." }
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf))
        { throw "Package file '$RelativePath' does not exist." }
    return $resolved
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-Hash([string]$Label, [string]$Path, [string]$Expected) {
    if ($Expected -notmatch '^[0-9a-fA-F]{64}$')
        { throw "$Label must contain exactly 64 hexadecimal characters." }
    $actual = Get-Sha256 $Path
    if ($actual -cne $Expected.ToLowerInvariant())
        { throw "$Label hash mismatch: expected $($Expected.ToLowerInvariant()), found $actual." }
}

$entryPath = Resolve-CanonicalPackageFile ([string]$manifest.assembly)
Assert-Hash 'Entry assembly' $entryPath ([string]$manifest.assemblySha256)

$seenDependencyPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$seenDependencyNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($dependency in @($manifest.dependencies)) {
    $invalidDependency = $null -eq $dependency
    $invalidDependency = $invalidDependency -or [string]::IsNullOrWhiteSpace([string]$dependency.assembly)
    $invalidDependency = $invalidDependency -or [string]::IsNullOrWhiteSpace([string]$dependency.assemblySha256)
    if ($invalidDependency)
        { throw 'Every private dependency requires assembly and assemblySha256.' }
    $dependencyPath = Resolve-CanonicalPackageFile ([string]$dependency.assembly)
    $duplicateDependency = [string]::Equals($dependencyPath, $entryPath, [StringComparison]::OrdinalIgnoreCase)
    $duplicateDependency = $duplicateDependency -or -not $seenDependencyPaths.Add($dependencyPath)
    if ($duplicateDependency)
        { throw "Private dependency '$($dependency.assembly)' duplicates the entry or another dependency." }
    Assert-Hash "Private dependency '$($dependency.assembly)'" $dependencyPath ([string]$dependency.assemblySha256)
    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($dependencyPath).Name
    if ([string]::IsNullOrWhiteSpace($assemblyName) -or -not $seenDependencyNames.Add($assemblyName))
        { throw "Private dependency '$($dependency.assembly)' has a duplicate or missing assembly identity." }
}

if ($SharedContractRoot) {
    $contractRoot = [IO.Path]::GetFullPath($SharedContractRoot)
    foreach ($property in $manifest.sharedContracts.psobject.Properties) {
        $contractPath = Join-Path $contractRoot ($property.Name + '.dll')
        if (-not (Test-Path -LiteralPath $contractPath -PathType Leaf))
            { throw "Shared contract '$($property.Name)' was not found under '$contractRoot'." }
        Assert-Hash "Shared contract '$($property.Name)'" $contractPath ([string]$property.Value)
    }
}

Write-Host "Verified Diablo adapter package: $($manifest.id)@$($manifest.version)"
Write-Host "Entry: $([IO.Path]::GetFileName($entryPath))"
Write-Host "Private dependencies: $(@($manifest.dependencies).Count)"
