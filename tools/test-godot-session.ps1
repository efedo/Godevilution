[CmdletBinding()]
param([string]$GodotExecutable, [switch]$Visible, [switch]$Journey, [switch]$TownLoop, [switch]$Multiplayer, [string]$CathedralProfileDirectory, [string]$CathedralDepthProfileDirectory)

$ErrorActionPreference = 'Stop'
if ($CathedralDepthProfileDirectory -and ($CathedralProfileDirectory -or $Journey -or $TownLoop -or $Multiplayer)) {
    throw 'Cathedral depth acceptance requires its own compiled profile.'
}
if ($Journey -and $TownLoop) { throw 'Choose either -Journey or -TownLoop.' }
if ($Multiplayer -and ($Journey -or $TownLoop)) { throw 'Multiplayer acceptance is a separate base-content profile.' }
if ($CathedralProfileDirectory -and ($Journey -or $TownLoop -or $Multiplayer)) {
    throw 'Cathedral acceptance is a separate compiled single-player playtest profile.'
}
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$sessionDirectory = if ($CathedralProfileDirectory -or $CathedralDepthProfileDirectory) {
    $profileDirectory = if ($CathedralDepthProfileDirectory) { $CathedralDepthProfileDirectory } else { $CathedralProfileDirectory }
    Join-Path ([IO.Path]::GetFullPath($profileDirectory)) ('sessions/acceptance-' + [guid]::NewGuid().ToString('N'))
} else { Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Scene-Acceptance-' + [guid]::NewGuid().ToString('N')) }
$launcher = Join-Path $repositoryRoot 'godot/run-local-client.ps1'
$parameters = @{ SessionDirectory = $sessionDirectory; Headless = -not $Visible; QuitAfterSeconds = 45 }
if ($GodotExecutable) { $parameters.GodotExecutable = $GodotExecutable }
try {
    if ($CathedralDepthProfileDirectory) {
        $parameters.CathedralDepthProfileDirectory = $CathedralDepthProfileDirectory
        $parameters.QuitAfterSeconds = 900
        & $launcher @parameters -AcceptancePhase CathedralDepthJourney
        $parameters.QuitAfterSeconds = 450
        & $launcher @parameters -AcceptancePhase CathedralDepthResume
        $created = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedraldepthjourney-processes.json') -Raw | ConvertFrom-Json
        $resumed = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedraldepthresume-processes.json') -Raw | ConvertFrom-Json
        if (($created.HostProcessId -eq $resumed.HostProcessId -and $created.HostStartedUtc -eq $resumed.HostStartedUtc) -or
            ($created.Clients[0].ProcessId -eq $resumed.Clients[0].ProcessId -and $created.Clients[0].StartedUtc -eq $resumed.Clients[0].StartedUtc) -or
            $created.ProfileHash -cne $resumed.ProfileHash -or $created.ContentTreeHash -cne $resumed.ContentTreeHash) {
            throw 'Cathedral depth resume requires fresh host/client processes and the same isolated profile.'
        }
        Write-Host "PASS: actual Godot Cathedral depths 1-4 descent/ascent, middle-floor fresh-host restore and native Town return. Evidence: $sessionDirectory"
        return
    }
    if ($CathedralProfileDirectory) {
        $parameters.CathedralProfileDirectory = $CathedralProfileDirectory
        $parameters.QuitAfterSeconds = 315
        & $launcher @parameters -AcceptancePhase CathedralJourney
        $parameters.QuitAfterSeconds = 195
        & $launcher @parameters -AcceptancePhase CathedralResume
        $created = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedraljourney-processes.json') -Raw | ConvertFrom-Json
        $resumed = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedralresume-processes.json') -Raw | ConvertFrom-Json
        if (($created.HostProcessId -eq $resumed.HostProcessId -and $created.HostStartedUtc -eq $resumed.HostStartedUtc) -or
            ($created.Clients[0].ProcessId -eq $resumed.Clients[0].ProcessId -and $created.Clients[0].StartedUtc -eq $resumed.Clients[0].StartedUtc) -or
            $created.ProfileHash -cne $resumed.ProfileHash -or $created.ContentTreeHash -cne $resumed.ContentTreeHash) {
            throw 'Cathedral resume requires fresh host/client processes and the same isolated profile.'
        }
        Write-Host "PASS: actual Godot generated Cathedral walking/collision, authored object/combat/loot, fresh-host restore and native Town return. Evidence: $sessionDirectory"
        return
    }
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
        $parameters.PortalPreviewContent = $true
        $parameters.QuitAfterSeconds = 240
        & $launcher @parameters -AcceptancePhase TownLoop
        $parameters.QuitAfterSeconds = 45
        & $launcher @parameters -AcceptancePhase TownLoopResume
        Write-Host "PASS: actual Godot scroll acquisition/cast, portal return, equipment/trade/stash, and fresh-host owner-return closure. Evidence: $sessionDirectory"
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
