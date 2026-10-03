[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$godotLauncher = Join-Path $repositoryRoot 'godot/run-local-client.ps1'
$profileDirectory = Join-Path $repositoryRoot '.local/cathedral-doors-playtest'
$profileMarker = Join-Path $profileDirectory 'cathedral-doors-playtest.json'
if (-not (Test-Path -LiteralPath $profileMarker -PathType Leaf)) {
    throw 'Compile the private .local/cathedral-doors-playtest profile first. See docs/ai/godot-build.md: Generated Cathedral door playtest.'
}
if (-not (Test-Path -LiteralPath $godotLauncher -PathType Leaf)) {
    throw "The Godot client launcher was not found at $godotLauncher."
}
Write-Host 'Generated Cathedral mutable-door profile; opt-in diagnostic acceptance, not campaign or native population parity.'
& $godotLauncher -CathedralDoorProfileDirectory $profileDirectory
exit $LASTEXITCODE
