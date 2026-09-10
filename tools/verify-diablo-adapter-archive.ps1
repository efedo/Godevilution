<#
.SYNOPSIS
    Verifies a distributable Diablo host-adapter ZIP archive.

.DESCRIPTION
    Expands one archive into an isolated temporary directory and delegates to
    the directory verifier. The temporary extraction is removed even when the
    static release gate fails.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,
    [string]$SharedContractRoot,
    [string]$ExpectedPackageId,
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$ExpectedPackageVersion
)

$ErrorActionPreference = 'Stop'
$archive = [IO.Path]::GetFullPath($ArchivePath)
$verifier = Join-Path $PSScriptRoot 'verify-diablo-adapter-package.ps1'

if (-not (Test-Path -LiteralPath $archive -PathType Leaf))
    { throw "Release archive '$archive' does not exist." }
if (-not (Test-Path -LiteralPath $verifier -PathType Leaf))
    { throw "Directory verifier '$verifier' does not exist." }

$extractionRoot = Join-Path ([IO.Path]::GetTempPath()) (
    'godevilution-adapter-archive-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $extractionRoot -Force | Out-Null
try {
    Expand-Archive -LiteralPath $archive -DestinationPath $extractionRoot
    $arguments = @{
        PackageRoot = $extractionRoot
    }
    if ($SharedContractRoot)
        { $arguments.SharedContractRoot = $SharedContractRoot }
    if ($ExpectedPackageId)
        { $arguments.ExpectedPackageId = $ExpectedPackageId }
    if ($ExpectedPackageVersion)
        { $arguments.ExpectedPackageVersion = $ExpectedPackageVersion }
    & $verifier @arguments
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0)
        { throw "The extracted package verifier failed with exit code $LASTEXITCODE." }
    Write-Host "Verified Diablo adapter archive: $archive"
} finally {
    if (Test-Path -LiteralPath $extractionRoot)
        { Remove-Item -LiteralPath $extractionRoot -Recurse -Force }
}
