[CmdletBinding()]
param([string]$GodotExecutable, [ValidateSet('legacy', 'cohort')] [string]$PersistenceMode = 'legacy', [switch]$Visible, [switch]$Journey, [switch]$TownLoop, [switch]$Lifecycle, [switch]$Multiplayer, [string]$CathedralProfileDirectory, [string]$CathedralDepthProfileDirectory)

$ErrorActionPreference = 'Stop'
$PersistenceMode = $PersistenceMode.ToLowerInvariant()
if ($CathedralDepthProfileDirectory -and ($CathedralProfileDirectory -or $Journey -or $TownLoop -or $Lifecycle -or $Multiplayer)) {
    throw 'Cathedral depth acceptance requires its own compiled profile.'
}
if ($Journey -and $TownLoop) { throw 'Choose either -Journey or -TownLoop.' }
if ($Lifecycle -and ($Journey -or $TownLoop -or $Multiplayer)) { throw 'Lifecycle acceptance is a separate Sorcerer preview profile.' }
if ($Multiplayer -and ($Journey -or $TownLoop -or $Lifecycle)) { throw 'Multiplayer acceptance is a separate base-content profile.' }
if ($CathedralProfileDirectory -and ($Journey -or $TownLoop -or $Lifecycle -or $Multiplayer)) {
    throw 'Cathedral acceptance is a separate compiled single-player playtest profile.'
}
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$sessionDirectory = if ($CathedralProfileDirectory -or $CathedralDepthProfileDirectory) {
    $profileDirectory = if ($CathedralDepthProfileDirectory) { $CathedralDepthProfileDirectory } else { $CathedralProfileDirectory }
    Join-Path ([IO.Path]::GetFullPath($profileDirectory)) ('sessions/acceptance-' + [guid]::NewGuid().ToString('N'))
} else { Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Scene-Acceptance-' + [guid]::NewGuid().ToString('N')) }
$launcher = Join-Path $repositoryRoot 'godot/run-local-client.ps1'
$parameters = @{ SessionDirectory = $sessionDirectory; PersistenceMode = $PersistenceMode; Headless = -not $Visible; QuitAfterSeconds = 45 }
if ($GodotExecutable) { $parameters.GodotExecutable = $GodotExecutable }
function Read-ModeProcessEvidence([string]$Phase) {
    $path = Join-Path $sessionDirectory ($Phase.ToLowerInvariant() + '-processes.json')
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "The launcher did not produce mode-labelled $Phase process evidence: $path"
    }
    $evidence = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $expectedHostPersistence = if ($PersistenceMode -eq 'cohort') { 'complete cohort' } else { 'legacy per-session' }
    if ($evidence.PersistenceMode -cne $PersistenceMode -or $evidence.HostPersistence -cne $expectedHostPersistence) {
        throw "The $Phase process evidence did not preserve persistence mode '$PersistenceMode'."
    }
    if ($Phase -eq 'Recovery') {
        if ($null -eq $evidence.StoppedProcessId -or [string]::IsNullOrWhiteSpace($evidence.StoppedHostStartedUtc) -or
            $null -eq $evidence.RestartedProcessId -or [string]::IsNullOrWhiteSpace($evidence.RestartedHostStartedUtc) -or
            $null -eq $evidence.GodotProcessId -or [string]::IsNullOrWhiteSpace($evidence.GodotStartedUtc) -or $null -eq $evidence.ExistingClients -or
            $null -eq $evidence.HostProcessId -or [string]::IsNullOrWhiteSpace($evidence.HostStartedUtc) -or
            $null -eq $evidence.Clients -or @($evidence.Clients).Count -lt 1) {
            throw 'Recovery process evidence omitted a host restart or retained-client identity.'
        }
    } elseif ($null -eq $evidence.HostProcessId -or [string]::IsNullOrWhiteSpace($evidence.HostStartedUtc) -or
        $null -eq $evidence.Clients -or @($evidence.Clients).Count -lt 1) {
        throw "The $Phase process evidence omitted host or client identity."
    }
    return $evidence
}
function Assert-RecoveryProcessEvidence($Evidence) {
    if ($Evidence.StoppedProcessId -ne $Evidence.HostProcessId -or
        $Evidence.StoppedHostStartedUtc -ne $Evidence.HostStartedUtc) {
        throw 'Recovery process evidence did not retain the initial recovery host identity.'
    }
    if ($Evidence.StoppedProcessId -eq $Evidence.RestartedProcessId -and
        $Evidence.StoppedHostStartedUtc -eq $Evidence.RestartedHostStartedUtc) {
        throw 'Recovery reused the authoritative host process identity.'
    }
    $client = @($Evidence.Clients)[0]
    if ($Evidence.GodotProcessId -ne $client.ProcessId -or $Evidence.GodotStartedUtc -ne $client.StartedUtc -or
        -not (@($Evidence.ExistingClients) | Where-Object {
            $_.ProcessId -eq $client.ProcessId -and $_.StartedUtc -eq $client.StartedUtc })) {
        throw 'Recovery did not retain the recovery-phase client process identity.'
    }
}
function Assert-FreshProcessEvidence($Created, $Resumed, [int]$ExpectedClientCount) {
    if ($Created.HostProcessId -eq $Resumed.HostProcessId -and $Created.HostStartedUtc -eq $Resumed.HostStartedUtc) {
        throw 'Ruleset resume did not start a fresh authoritative host.'
    }
    if (@($Created.Clients).Count -ne $ExpectedClientCount -or @($Resumed.Clients).Count -ne $ExpectedClientCount) {
        throw 'Ruleset process evidence did not retain the expected client count.'
    }
    foreach ($resumedClient in @($Resumed.Clients)) {
        if (@($Created.Clients) | Where-Object { $_.ProcessId -eq $resumedClient.ProcessId -and $_.StartedUtc -eq $resumedClient.StartedUtc }) {
            throw 'Ruleset resume reused a client process identity.'
        }
    }
}
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
        $created = Read-ModeProcessEvidence 'MultiplayerCreate'
        & $launcher @parameters -AcceptancePhase MultiplayerResume
        $resumed = Read-ModeProcessEvidence 'MultiplayerResume'
        Assert-FreshProcessEvidence $created $resumed 2
        Write-Host "PASS: two actual Godot clients, mutual movement/state visibility, and fresh-host multiplayer resume. Evidence: $sessionDirectory"
        return
    }
    if ($Lifecycle) {
        $parameters.LifecyclePreviewContent = $true
        $parameters.QuitAfterSeconds = 420
        & $launcher @parameters -AcceptancePhase Lifecycle
        $parameters.QuitAfterSeconds = 90
        & $launcher @parameters -AcceptancePhase LifecycleResume
        $created = Get-Content -LiteralPath (Join-Path $sessionDirectory 'lifecycle-processes.json') -Raw | ConvertFrom-Json
        $resumed = Get-Content -LiteralPath (Join-Path $sessionDirectory 'lifecycleresume-processes.json') -Raw | ConvertFrom-Json
        if (($created.HostProcessId -eq $resumed.HostProcessId -and $created.HostStartedUtc -eq $resumed.HostStartedUtc) -or
            ($created.Clients[0].ProcessId -eq $resumed.Clients[0].ProcessId -and $created.Clients[0].StartedUtc -eq $resumed.Clients[0].StartedUtc)) {
            throw 'Lifecycle resume did not start fresh authoritative host/client processes.'
        }
        $createdEvidence = Get-Content -LiteralPath (Join-Path $sessionDirectory 'lifecycle.json') -Raw | ConvertFrom-Json
        $resumedEvidence = Get-Content -LiteralPath (Join-Path $sessionDirectory 'lifecycleresume.json') -Raw | ConvertFrom-Json
        if ($createdEvidence.ContentHash -cne $resumedEvidence.ContentHash -or
            $createdEvidence.RulesetHash -cne $resumedEvidence.RulesetHash -or
            $createdEvidence.ClassId -ne 2 -or $resumedEvidence.ClassId -ne 2) {
            throw 'Lifecycle resume changed the persisted content/ruleset identity or Sorcerer class.'
        }
        Write-Host "PASS: actual Sorcerer quest/charged-staff/recharge/Firebolt/death/restart and fresh-host resume. Evidence: $sessionDirectory"
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
        $created = Read-ModeProcessEvidence 'Create'
        & $launcher @parameters -AcceptancePhase Resume
        $resumed = Read-ModeProcessEvidence 'Resume'
        Assert-FreshProcessEvidence $created $resumed 1
        & $launcher @parameters -AcceptancePhase Recovery
        $recovery = Read-ModeProcessEvidence 'Recovery'
        Assert-FreshProcessEvidence $resumed $recovery 1
        Assert-RecoveryProcessEvidence $recovery
        Write-Host "PASS: actual Godot create/move/checkpoint/fresh-host resume and server-loss/reconnect. Evidence: $sessionDirectory"
} finally {
    # Retain isolated saves/logs for diagnosis; never modify normal launcher saves.
    Write-Host "Acceptance artifacts retained at $sessionDirectory"
}
