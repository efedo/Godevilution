[CmdletBinding()]
param()

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
& (Join-Path $repositoryRoot 'run-authoritative-client.ps1')
exit $LASTEXITCODE
