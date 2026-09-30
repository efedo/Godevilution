[CmdletBinding()]
param([string]$SessionDirectory, [switch]$Resume)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$previousAcceptanceRoot = $env:GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY
$previousResumeAcceptance = $env:GODEVILUTION_NATIVE_ACCEPTANCE_RESUME
if ($Resume -and [string]::IsNullOrWhiteSpace($SessionDirectory)) {
    throw 'Resume requires the explicit directory of a prior isolated native acceptance run.'
}
try {
    $env:GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY = if ([string]::IsNullOrWhiteSpace($SessionDirectory)) {
        Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Native-Acceptance-' + [guid]::NewGuid().ToString('N'))
    } else { [IO.Path]::GetFullPath($SessionDirectory) }
    $env:GODEVILUTION_NATIVE_ACCEPTANCE_RESUME = if ($Resume) { '1' } else { '0' }
    Write-Host "Isolated native acceptance evidence: $env:GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY"
    & (Join-Path $repositoryRoot 'run-authoritative-client.ps1')
    exit $LASTEXITCODE
}
finally {
    if ($null -eq $previousAcceptanceRoot) {
        Remove-Item Env:GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY -ErrorAction SilentlyContinue
    } else {
        $env:GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY = $previousAcceptanceRoot
    }
    if ($null -eq $previousResumeAcceptance) {
        Remove-Item Env:GODEVILUTION_NATIVE_ACCEPTANCE_RESUME -ErrorAction SilentlyContinue
    } else {
        $env:GODEVILUTION_NATIVE_ACCEPTANCE_RESUME = $previousResumeAcceptance
    }
}
