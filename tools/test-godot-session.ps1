[CmdletBinding()]
param([string]$GodotExecutable, [switch]$Visible)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$sessionDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Scene-Acceptance-' + [guid]::NewGuid().ToString('N'))
$launcher = Join-Path $repositoryRoot 'godot/run-local-client.ps1'
$parameters = @{ SessionDirectory = $sessionDirectory; Headless = -not $Visible; QuitAfterSeconds = 45 }
if ($GodotExecutable) { $parameters.GodotExecutable = $GodotExecutable }
try {
    & $launcher @parameters -AcceptancePhase Create
    & $launcher @parameters -AcceptancePhase Resume
    Write-Host "PASS: actual Godot create/move/checkpoint/fresh-host resume/move. Evidence: $sessionDirectory"
} finally {
    # Retain isolated saves/logs for diagnosis; never modify normal launcher saves.
    Write-Host "Acceptance artifacts retained at $sessionDirectory"
}
