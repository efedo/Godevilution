[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$godotLauncher = Join-Path $repositoryRoot 'godot-client/run-local-client.ps1'
if (-not (Test-Path -LiteralPath $godotLauncher -PathType Leaf)) {
    throw "The Godot client launcher was not found at $godotLauncher."
}
& $godotLauncher -LifecyclePreviewContent
exit $LASTEXITCODE
