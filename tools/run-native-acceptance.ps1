[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$previousAcceptanceRoot = $env:GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY
try {
    $env:GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY = Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Native-Acceptance-' + [guid]::NewGuid().ToString('N'))
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
}
