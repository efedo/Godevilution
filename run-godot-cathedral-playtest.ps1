[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$godotLauncher = Join-Path $repositoryRoot 'godot-client/run-local-client.ps1'
$profileDirectory = Join-Path $repositoryRoot '.local/cathedral-playtest'
if (-not (Test-Path -LiteralPath (Join-Path $profileDirectory 'cathedral-playtest.json') -PathType Leaf)) {
    throw 'Compile the private .local/cathedral-playtest profile first. See docs/ai/godot-build.md: Generated Cathedral playtest.'
}
if (-not (Test-Path -LiteralPath $godotLauncher -PathType Leaf)) {
    throw "The Godot client launcher was not found at $godotLauncher."
}
Write-Host 'Generated Cathedral geometry with authored diagnostic population; not campaign or native population parity.'
& $godotLauncher -CathedralProfileDirectory $profileDirectory
exit $LASTEXITCODE
