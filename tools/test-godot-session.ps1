[CmdletBinding()]
param([string]$GodotExecutable, [ValidateSet('legacy', 'cohort')] [string]$PersistenceMode = 'legacy', [switch]$Visible, [switch]$Journey, [switch]$TownLoop, [switch]$Lifecycle, [switch]$Multiplayer, [switch]$MultiplayerRecovery, [string]$CathedralProfileDirectory, [string]$CathedralDepthProfileDirectory)

$ErrorActionPreference = 'Stop'
$PersistenceMode = $PersistenceMode.ToLowerInvariant()
$MultiplayerRecovery = [bool]$MultiplayerRecovery
if ($MultiplayerRecovery) { $Multiplayer = $true }
if ($MultiplayerRecovery -and $PersistenceMode -cne 'cohort') {
    throw 'MultiplayerRecovery requires -PersistenceMode cohort.'
}
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
function Assert-MultiplayerRecoveryProcessEvidence($Evidence) {
    if ($Evidence.PersistenceMode -cne 'cohort' -or $Evidence.HostPersistence -cne 'complete cohort' -or
        $null -eq $Evidence.StoppedProcessId -or $null -eq $Evidence.RestartedProcessId -or
        $Evidence.StoppedProcessId -le 0 -or $Evidence.RestartedProcessId -le 0 -or
        [string]::IsNullOrWhiteSpace($Evidence.StoppedHostStartedUtc) -or
        [string]::IsNullOrWhiteSpace($Evidence.StoppedHostEndedUtc) -or
        [string]::IsNullOrWhiteSpace($Evidence.RestartedHostStartedUtc) -or
        $null -eq $Evidence.StoppedHostExitCode -or
        @($Evidence.Clients).Count -ne 2 -or @($Evidence.ExistingClients).Count -ne 2) {
        throw 'MultiplayerRecovery process evidence omitted the complete-cohort host transition or both clients.'
    }
    if ($Evidence.StoppedProcessId -ne $Evidence.InitialHostProcessId -or
        $Evidence.StoppedHostStartedUtc -ne $Evidence.InitialHostStartedUtc) {
        throw 'MultiplayerRecovery did not retain the initial authoritative host identity in terminal evidence.'
    }
    if ($Evidence.StoppedProcessId -eq $Evidence.RestartedProcessId -and
        $Evidence.StoppedHostStartedUtc -eq $Evidence.RestartedHostStartedUtc) {
        throw 'MultiplayerRecovery reused the authoritative host process identity.'
    }
    $stoppedEnded = [DateTime]::Parse($Evidence.StoppedHostEndedUtc).ToUniversalTime()
    $restartedStarted = [DateTime]::Parse($Evidence.RestartedHostStartedUtc).ToUniversalTime()
    if ($restartedStarted -le $stoppedEnded) {
        throw 'MultiplayerRecovery recorded the replacement host before the original host terminated.'
    }
    foreach ($client in @($Evidence.Clients) + @($Evidence.ExistingClients)) {
        if ($client.ProcessId -le 0 -or [string]::IsNullOrWhiteSpace($client.StartedUtc)) {
            throw 'MultiplayerRecovery emitted an invalid retained Godot client identity.'
        }
        try { [void][DateTime]::Parse($client.StartedUtc) } catch { throw 'MultiplayerRecovery emitted an unparsable client start time.' }
    }
    $clientKeys = @($Evidence.Clients | ForEach-Object { "$($_.ProcessId):$($_.StartedUtc)" })
    $existingKeys = @($Evidence.ExistingClients | ForEach-Object { "$($_.ProcessId):$($_.StartedUtc)" })
    if ($clientKeys.Count -ne 2 -or @($clientKeys | Select-Object -Unique).Count -ne 2 -or
        (@($clientKeys | Where-Object { $_ -notin $existingKeys }).Count -ne 0) -or
        (@($existingKeys | Where-Object { $_ -notin $clientKeys }).Count -ne 0)) {
        throw 'MultiplayerRecovery did not retain the exact two Godot client process identities.'
    }
    if ($Evidence.Ready.Count -ne 2 -or $Evidence.Offline.Count -ne 2) {
        throw 'MultiplayerRecovery did not retain both per-slot readiness and offline barriers.'
    }
}
function Assert-MultiplayerRecoveryEvidence($SessionDirectory, $ContentHash, $RulesetHash) {
    $final = @()
    for ($slot = 1; $slot -le 2; $slot++) {
        $playerDirectory = Join-Path $SessionDirectory "player-$slot"
        $path = Join-Path $playerDirectory 'multiplayerrecovery.json'
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing multiplayer recovery evidence: $path" }
        $evidence = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if (-not $evidence.Passed -or $evidence.Mode -ne 'Multiplayer' -or
            $evidence.ContentHash -cne $ContentHash -or $evidence.RulesetHash -cne $RulesetHash -or
            $evidence.Slot -ne $slot -or $evidence.EntityId -lt 1 -or $evidence.PeerEntityId -lt 1 -or
            $evidence.EntityId -eq $evidence.PeerEntityId -or -not $evidence.ClockGated -or
            $evidence.ReceiptSequence -lt 1 -or $evidence.AppliedTick -lt $evidence.ReconnectTick -or
            [string]::IsNullOrWhiteSpace($evidence.DisconnectedSnapshotFingerprint)) {
            throw "Invalid multiplayer recovery evidence for player $slot."
        }
        $offlinePath = Join-Path $playerDirectory 'multiplayerrecovery-offline-ready.json'
        $rejoinedPath = Join-Path $playerDirectory 'multiplayerrecovery-rejoined.json'
        if (-not (Test-Path -LiteralPath $offlinePath) -or -not (Test-Path -LiteralPath $rejoinedPath)) {
            throw "Multiplayer recovery barriers are incomplete for player $slot."
        }
        $offline = Get-Content -LiteralPath $offlinePath -Raw | ConvertFrom-Json
        $rejoined = Get-Content -LiteralPath $rejoinedPath -Raw | ConvertFrom-Json
        if (-not $offline.Passed -or $offline.OfflineSeconds -lt 2 -or
            $rejoined.EntityId -ne $evidence.EntityId -or $rejoined.PeerEntityId -ne $evidence.PeerEntityId) {
            throw "Multiplayer recovery did not prove bounded frozen offline/restored state for player $slot."
        }
        $commandPath = Join-Path $playerDirectory ("multiplayerrecovery-command-{0}.json" -f $evidence.Sequence)
        if (-not (Test-Path -LiteralPath $commandPath)) { throw "Missing post-reconnect command evidence for player $slot." }
        $command = Get-Content -LiteralPath $commandPath -Raw | ConvertFrom-Json
        if (-not $command.Passed -or $command.PreviousSequence -ne ($evidence.Sequence - 1)) {
            throw "Post-reconnect command evidence did not prove the next client sequence for player $slot."
        }
        $final += $evidence
    }
    if ($final[0].EntityId -ne $final[1].PeerEntityId -or $final[0].PeerEntityId -ne $final[1].EntityId -or
        $final[0].EntityId -eq $final[0].PeerEntityId -or $final[1].EntityId -eq $final[1].PeerEntityId) {
        throw 'MultiplayerRecovery clients disagree about the complete roster or duplicate an entity.'
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
    if ($MultiplayerRecovery) {
        $parameters.Multiplayer = $true
        $parameters.PersistenceMode = 'cohort'
        $parameters.QuitAfterSeconds = 75
        & $launcher @parameters -AcceptancePhase MultiplayerCreate
        $created = Read-ModeProcessEvidence 'MultiplayerCreate'
        & $launcher @parameters -AcceptancePhase MultiplayerResume
        $resumed = Read-ModeProcessEvidence 'MultiplayerResume'
        Assert-FreshProcessEvidence $created $resumed 2
        $parameters.QuitAfterSeconds = 105
        & $launcher @parameters -AcceptancePhase MultiplayerRecovery
        $recovery = Read-ModeProcessEvidence 'MultiplayerRecovery'
        Assert-MultiplayerRecoveryProcessEvidence $recovery
        Assert-MultiplayerRecoveryEvidence $sessionDirectory $recovery.ContentHash $recovery.RulesetHash
        Write-Host "PASS: bounded actual-Godot complete-cohort multiplayer host-loss/reconnect with two retained clients, stable roster, post-reconnect accepted controls, canonical receipts, and clean exits. Evidence: $sessionDirectory"
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
