[CmdletBinding()]
param([string]$GodotExecutable, [switch]$Visible, [switch]$Journey)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$sessionDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Scene-Acceptance-' + [guid]::NewGuid().ToString('N'))
$launcher = Join-Path $repositoryRoot 'godot/run-local-client.ps1'
$parameters = @{ SessionDirectory = $sessionDirectory; Headless = -not $Visible; QuitAfterSeconds = 45 }
if ($GodotExecutable) { $parameters.GodotExecutable = $GodotExecutable }
try {
    if ($Journey) {
        $parameters.QuitAfterSeconds = 165
        & $launcher @parameters -AcceptancePhase Journey
        Write-Host "PASS: actual Godot responsive controls, cancelled travel, and server-owned town-to-dungeon route. Evidence: $sessionDirectory"
        return
    }
    & $launcher @parameters -AcceptancePhase Create
    & $launcher @parameters -AcceptancePhase Resume
    & $launcher @parameters -AcceptancePhase Recovery
    Write-Host "PASS: actual Godot create/move/checkpoint/fresh-host resume and server-loss/reconnect. Evidence: $sessionDirectory"
} finally {
    # Retain isolated saves/logs for diagnosis; never modify normal launcher saves.
    Write-Host "Acceptance artifacts retained at $sessionDirectory"
}
