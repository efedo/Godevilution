[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$godotLauncher = Join-Path $PSScriptRoot 'godot/run-local-client.ps1'
if (-not (Test-Path -LiteralPath $godotLauncher -PathType Leaf)) {
    throw "The Godot client launcher was not found at $godotLauncher."
}
& $godotLauncher -Multiplayer
exit $LASTEXITCODE
