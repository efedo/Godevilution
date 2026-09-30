[CmdletBinding()]
param([string]$GodotExecutable, [switch]$Visible, [switch]$Journey, [switch]$TownLoop, [switch]$Multiplayer)

$ErrorActionPreference = 'Stop'
if ($Journey -and $TownLoop) { throw 'Choose either -Journey or -TownLoop.' }
if ($Multiplayer -and ($Journey -or $TownLoop)) { throw 'Multiplayer acceptance is a separate base-content profile.' }
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$sessionDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Scene-Acceptance-' + [guid]::NewGuid().ToString('N'))
$launcher = Join-Path $repositoryRoot 'godot/run-local-client.ps1'
$parameters = @{ SessionDirectory = $sessionDirectory; Headless = -not $Visible; QuitAfterSeconds = 45 }
if ($GodotExecutable) { $parameters.GodotExecutable = $GodotExecutable }
try {
    if ($Multiplayer) {
        $parameters.Multiplayer = $true
        $parameters.QuitAfterSeconds = 75
        & $launcher @parameters -AcceptancePhase MultiplayerCreate
        & $launcher @parameters -AcceptancePhase MultiplayerResume
        $created = Get-Content -LiteralPath (Join-Path $sessionDirectory 'multiplayercreate-processes.json') -Raw | ConvertFrom-Json
        $resumed = Get-Content -LiteralPath (Join-Path $sessionDirectory 'multiplayerresume-processes.json') -Raw | ConvertFrom-Json
        if ($created.HostProcessId -eq $resumed.HostProcessId -and $created.HostStartedUtc -eq $resumed.HostStartedUtc) {
            throw 'Multiplayer resume did not start a fresh authoritative host.'
        }
        foreach ($resumedClient in $resumed.Clients) {
            if ($created.Clients | Where-Object { $_.ProcessId -eq $resumedClient.ProcessId -and $_.StartedUtc -eq $resumedClient.StartedUtc }) {
                throw 'Multiplayer resume did not start two fresh Godot processes.'
            }
        }
        Write-Host "PASS: two actual Godot clients, mutual movement/state visibility, and fresh-host multiplayer resume. Evidence: $sessionDirectory"
        return
    }
    if ($TownLoop) {
        $parameters.PreviewContent = $true
        $parameters.QuitAfterSeconds = 240
        & $launcher @parameters -AcceptancePhase TownLoop
        $parameters.QuitAfterSeconds = 45
        & $launcher @parameters -AcceptancePhase TownLoopResume
        Write-Host "PASS: actual Godot equipment, return-to-town, trade/stash, and fresh-host preview resume. Evidence: $sessionDirectory"
        return
    }
    if ($Journey) {
        $parameters.QuitAfterSeconds = 165
        & $launcher @parameters -AcceptancePhase Journey
        $parameters.QuitAfterSeconds = 45
        & $launcher @parameters -AcceptancePhase JourneyResume
        Write-Host "PASS: actual Godot responsive controls, town-to-dungeon combat/loot, and fresh-host gameplay resume. Evidence: $sessionDirectory"
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
