[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$godotLauncher = Join-Path $repositoryRoot 'godot-client/run-local-client.ps1'
$profileDirectory = Join-Path $repositoryRoot '.local/cathedral-depths-playtest'
if (-not (Test-Path -LiteralPath (Join-Path $profileDirectory 'cathedral-depths-playtest.json') -PathType Leaf)) {
    throw 'Compile the private .local/cathedral-depths-playtest profile first. See docs/ai/godot-build.md: Generated Cathedral depths playtest.'
}
if (-not (Test-Path -LiteralPath $godotLauncher -PathType Leaf)) {
    throw "The Godot client launcher was not found at $godotLauncher."
}
Write-Host 'Native Cathedral depths 1-4; authored diagnostics and explicit seeds. Catacombs-5 boundary is not implemented.'
& $godotLauncher -CathedralDepthProfileDirectory $profileDirectory
exit $LASTEXITCODE
