[CmdletBinding()]
param([string]$GodotExecutable, [ValidateSet('legacy', 'cohort')] [string]$PersistenceMode = 'legacy', [switch]$Visible, [switch]$Journey, [switch]$TownLoop, [switch]$Lifecycle, [switch]$Multiplayer, [switch]$MultiplayerRecovery, [switch]$MultiplayerPendingRecovery, [string]$CathedralProfileDirectory, [string]$CathedralDepthProfileDirectory, [string]$CathedralDoorProfileDirectory, [ValidateSet('default', 'atlas', 'semantic', 'missing', 'undersized', 'invalid')] [string]$PresentationPackProfile = 'default', [switch]$CommandTimingTrace, [switch]$Cooperative)

$ErrorActionPreference = 'Stop'
$PersistenceMode = $PersistenceMode.ToLowerInvariant()
$PresentationPackProfile = $PresentationPackProfile.ToLowerInvariant()
$cooperative = [bool]$Cooperative
if ($cooperative) {
    if ($Multiplayer) { throw 'Choose -Cooperative or -Multiplayer, not both.' }
    $Multiplayer = $true
}
$MultiplayerRecovery = [bool]$MultiplayerRecovery
if ($MultiplayerRecovery -and $MultiplayerPendingRecovery) { throw 'Choose settled or pending-command recovery, never both.' }
if ($MultiplayerRecovery -or $MultiplayerPendingRecovery) { $Multiplayer = $true }
if (($MultiplayerRecovery -or $MultiplayerPendingRecovery) -and $PersistenceMode -cne 'cohort') {
    throw 'MultiplayerRecovery requires -PersistenceMode cohort.'
}
if ($CathedralDepthProfileDirectory -and ($CathedralProfileDirectory -or $Journey -or $TownLoop -or $Lifecycle -or $Multiplayer)) {
    throw 'Cathedral depth acceptance requires its own compiled profile.'
}
if ($CathedralDoorProfileDirectory -and ($CathedralProfileDirectory -or $CathedralDepthProfileDirectory -or $Journey -or $TownLoop -or $Lifecycle -or $Multiplayer -or $MultiplayerRecovery -or $MultiplayerPendingRecovery)) {
    throw 'Cathedral door acceptance requires its own compiled profile.'
}
if ($Journey -and $TownLoop) { throw 'Choose either -Journey or -TownLoop.' }
if ($cooperative -and ($Journey -or $TownLoop -or $Lifecycle -or $MultiplayerRecovery -or $MultiplayerPendingRecovery)) {
    throw 'Cooperative acceptance is a separate town-loop-preview journey/resume profile.'
}
if ($cooperative -and -not ($PersistenceMode -eq 'cohort' -and $Multiplayer)) {
    throw 'Cooperative acceptance requires two clients and complete-cohort persistence.'
}
if ($Lifecycle -and ($Journey -or $TownLoop -or $Multiplayer)) { throw 'Lifecycle acceptance is a separate Sorcerer preview profile.' }
if ($Multiplayer -and ($Journey -or $TownLoop -or $Lifecycle) -and -not $cooperative) { throw 'Multiplayer acceptance is a separate base-content profile.' }
if ($CathedralProfileDirectory -and ($Journey -or $TownLoop -or $Lifecycle -or $Multiplayer)) {
    throw 'Cathedral acceptance is a separate compiled single-player playtest profile.'
}
if ($CathedralDoorProfileDirectory -and $PresentationPackProfile -ne 'default') {
    throw 'Cathedral door acceptance cannot combine with presentation-pack fixtures.'
}
if ($PresentationPackProfile -ne 'default' -and ($Journey -or $TownLoop -or $Lifecycle -or $Multiplayer -or $CathedralProfileDirectory -or $CathedralDepthProfileDirectory -or $CathedralDoorProfileDirectory)) {
    throw 'Presentation pack fixtures require the separate base single-player acceptance path.'
}
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$sessionDirectory = if ($CathedralProfileDirectory -or $CathedralDepthProfileDirectory -or $CathedralDoorProfileDirectory) {
    $profileDirectory = if ($CathedralDepthProfileDirectory) { $CathedralDepthProfileDirectory } elseif ($CathedralDoorProfileDirectory) { $CathedralDoorProfileDirectory } else { $CathedralProfileDirectory }
    Join-Path ([IO.Path]::GetFullPath($profileDirectory)) ('sessions/acceptance-' + [guid]::NewGuid().ToString('N'))
} else { Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Scene-Acceptance-' + [guid]::NewGuid().ToString('N')) }
$launcher = Join-Path $repositoryRoot 'godot/run-local-client.ps1'
$parameters = @{ SessionDirectory = $sessionDirectory; PersistenceMode = $PersistenceMode; Headless = -not $Visible; QuitAfterSeconds = 45 }
if ($CommandTimingTrace) { $parameters.CommandTimingTrace = $true }
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
    if ($Phase -in @('CooperativeJourney', 'CooperativeResume') -and
        ([string]$evidence.ContentHash -cnotmatch '^[0-9a-f]{64}$' -or
         [string]$evidence.RulesetHash -cnotmatch '^[0-9a-f]{64}$')) {
        throw "The $Phase process evidence omitted validated host content/ruleset identity."
    }
    if ($Phase -eq 'Recovery') {
        if ($null -eq $evidence.StoppedProcessId -or [string]::IsNullOrWhiteSpace($evidence.StoppedHostStartedUtc) -or
            [string]::IsNullOrWhiteSpace($evidence.StoppedHostEndedUtc) -or $null -eq $evidence.StoppedHostExitCode -or
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
function Assert-RecoveryProcessEvidence($Evidence, $StoppedMarker) {
    # An observed terminal native exit, not a requested Kill, permits replacement.
    foreach ($identity in @($Evidence.HostProcessId, $Evidence.StoppedProcessId,
        $Evidence.RestartedProcessId, $Evidence.GodotProcessId)) {
        if (($identity -isnot [int] -and $identity -isnot [long]) -or $identity -lt 1 -or $identity -gt [int]::MaxValue) {
            throw 'Recovery process evidence contains an invalid process identity.'
        }
    }
    if (($Evidence.StoppedHostExitCode -isnot [int] -and $Evidence.StoppedHostExitCode -isnot [long]) -or
        $Evidence.StoppedHostExitCode -lt [int]::MinValue -or $Evidence.StoppedHostExitCode -gt [int]::MaxValue) {
        throw 'Recovery process evidence omitted a native terminal exit code.'
    }
    $started = [DateTime]::Parse($Evidence.StoppedHostStartedUtc).ToUniversalTime()
    $ended = [DateTime]::Parse($Evidence.StoppedHostEndedUtc).ToUniversalTime()
    $restarted = [DateTime]::Parse($Evidence.RestartedHostStartedUtc).ToUniversalTime()
    if ($ended -lt $started -or $restarted -lt $ended) {
        throw 'Recovery recorded host replacement before confirmed terminal exit.'
    }
    if ($null -eq $StoppedMarker -or $StoppedMarker.Stopped -isnot [bool] -or -not $StoppedMarker.Stopped -or
        ($StoppedMarker.ProcessId -isnot [int] -and $StoppedMarker.ProcessId -isnot [long]) -or
        ($StoppedMarker.ExitCode -isnot [int] -and $StoppedMarker.ExitCode -isnot [long]) -or
        ($Evidence.Port -isnot [int] -and $Evidence.Port -isnot [long]) -or $Evidence.Port -lt 1 -or $Evidence.Port -gt 65535 -or
        ($StoppedMarker.Port -isnot [int] -and $StoppedMarker.Port -isnot [long]) -or
        $StoppedMarker.ProcessId -ne $Evidence.StoppedProcessId -or
        $StoppedMarker.StartedUtc -cne $Evidence.StoppedHostStartedUtc -or
        $StoppedMarker.EndedUtc -cne $Evidence.StoppedHostEndedUtc -or
        $StoppedMarker.ExitCode -ne $Evidence.StoppedHostExitCode -or $StoppedMarker.Port -ne $Evidence.Port) {
        throw 'Recovery stopped-host marker does not match confirmed terminal evidence.'
    }
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
function Assert-CooperativeEvidence([string]$SessionDirectory, [string]$ContentHash, [string]$RulesetHash, [bool]$ExpectedVisible = $false) {
    $created = @()
    $resumed = @()
    for ($slot = 1; $slot -le 2; $slot++) {
        $playerDirectory = Join-Path $SessionDirectory "player-$slot"
        $journeyPath = Join-Path $playerDirectory 'cooperativejourney.json'
        $resumePath = Join-Path $playerDirectory 'cooperativeresume.json'
        if (-not (Test-Path -LiteralPath $journeyPath -PathType Leaf) -or
            -not (Test-Path -LiteralPath $resumePath -PathType Leaf)) {
            throw "Cooperative evidence is incomplete for player $slot."
        }
        $journey = Get-Content -LiteralPath $journeyPath -Raw | ConvertFrom-Json
        $resume = Get-Content -LiteralPath $resumePath -Raw | ConvertFrom-Json
        foreach ($document in @($journey, $resume)) {
            if (-not ($document.Passed -is [bool]) -or -not $document.Passed -or $document.Mode -ne 'Multiplayer' -or
                $document.ContentHash -cne $ContentHash -or $document.RulesetHash -cne $RulesetHash -or
                $document.Slot -ne $slot -or $document.EntityId -lt 1 -or $document.PeerEntityId -lt 1 -or
                $document.EntityId -eq $document.PeerEntityId -or $document.LevelId -ne 1 -or
                $document.MonsterId -ne 9 -or
                -not ($document.MonsterDefeated -is [bool]) -or -not $document.MonsterDefeated -or $document.DropEntityId -ne 1000009 -or
                $document.DropSeed -ne 6001 -or $document.OwnerEntityId -lt 1 -or
                $document.OwnerInventoryCount -ne 1 -or $document.PeerInventoryCount -ne 0 -or
                -not ($document.DropPresent -is [bool]) -or $document.DropPresent -or $document.ReceiptStatus -notin @('Accepted', 'Duplicate') -or
                $document.PickupStatus -notin @('Accepted', 'Duplicate') -or $document.ReceiptSequence -lt 1 -or
                $document.AppliedTick -lt 1 -or $document.Sequence -lt 1 -or $document.PickupSequence -lt 1 -or $document.PickupReceiptSequence -lt 1 -or
                $document.PickupAppliedTick -lt 1 -or $document.PostMoveX -lt 0 -or $document.PostMoveY -lt 0 -or
                $document.InitialX -lt 0 -or $document.InitialY -lt 0 -or
                $document.InitialPeerX -lt 0 -or $document.InitialPeerY -lt 0 -or
                -not ($document.Visible -is [bool]) -or $document.Visible -ne $ExpectedVisible -or
                -not ($document.DrawObserved -is [bool]) -or ($ExpectedVisible -and -not $document.DrawObserved)) {
                throw "Invalid cooperative state evidence for player $slot."
            }
            if ($document.PostMoveX -ne $document.PositionX -or $document.PostMoveY -ne $document.PositionY) {
                throw "Cooperative final position contradicts movement evidence for player $slot."
            }
        }
        if ($resume.RestoredEntityId -ne $journey.EntityId -or
            $resume.RestoredPeerEntityId -ne $journey.PeerEntityId -or
            $resume.RestoredX -ne $journey.PositionX -or $resume.RestoredY -ne $journey.PositionY -or
            $resume.RestoredPeerX -ne $journey.PeerPositionX -or $resume.RestoredPeerY -ne $journey.PeerPositionY -or
            $resume.OwnerEntityId -ne $journey.OwnerEntityId -or $resume.DropEntityId -ne $journey.DropEntityId -or
            $resume.DropSeed -ne $journey.DropSeed -or $resume.MonsterId -ne $journey.MonsterId) {
            throw "Fresh-host cooperative resume changed the durable world or roster for player $slot."
        }
        if ($resume.InitialX -ne $journey.PositionX -or $resume.InitialY -ne $journey.PositionY -or
            $resume.InitialPeerX -ne $journey.PeerPositionX -or $resume.InitialPeerY -ne $journey.PeerPositionY -or
            $resume.PickupSequence -ne $journey.PickupSequence -or
            $resume.PickupReceiptSequence -ne $journey.PickupReceiptSequence -or
            $resume.PickupAppliedTick -ne $journey.PickupAppliedTick) {
            throw "Fresh-host cooperative resume did not begin from the recorded post-journey state for player $slot."
        }
        if ($resume.PickupStatus -ne $journey.PickupStatus) {
            throw "Fresh-host cooperative resume changed the retained pickup receipt status for player $slot."
        }
        if ($resume.Sequence -le $journey.Sequence -or $resume.ReceiptSequence -le $journey.ReceiptSequence -or
            $resume.AppliedTick -lt $journey.AppliedTick -or
            ($resume.PostMoveX -eq $resume.RestoredX -and $resume.PostMoveY -eq $resume.RestoredY)) {
            throw "Fresh-host cooperative resume did not advance the durable command ledger and position for player $slot."
        }
        $created += $journey
        $resumed += $resume
    }
    if ($created[0].EntityId -ne $created[1].PeerEntityId -or
        $created[0].PeerEntityId -ne $created[1].EntityId -or
        $resumed[0].EntityId -ne $resumed[1].PeerEntityId -or
        $resumed[0].PeerEntityId -ne $resumed[1].EntityId -or
        $created[0].PositionX -ne $created[1].PeerPositionX -or
        $created[0].PositionY -ne $created[1].PeerPositionY -or
        $created[1].PositionX -ne $created[0].PeerPositionX -or
        $created[1].PositionY -ne $created[0].PeerPositionY -or
        $resumed[0].PositionX -ne $resumed[1].PeerPositionX -or
        $resumed[0].PositionY -ne $resumed[1].PeerPositionY -or
        $resumed[1].PositionX -ne $resumed[0].PeerPositionX -or
        $resumed[1].PositionY -ne $resumed[0].PeerPositionY -or
        $created[0].OwnerEntityId -ne $created[1].OwnerEntityId -or
        $resumed[0].OwnerEntityId -ne $resumed[1].OwnerEntityId -or
        $created[0].OwnerEntityId -ne $created[0].EntityId -or
        $created[1].OwnerEntityId -ne $created[0].EntityId -or
        $resumed[0].OwnerEntityId -ne $resumed[0].EntityId -or
        $resumed[1].OwnerEntityId -ne $resumed[0].EntityId -or
        $created[0].PickupReceiptSequence -ne $created[1].PickupReceiptSequence -or
        $created[0].PickupSequence -ne $created[1].PickupSequence -or
        $created[0].PickupAppliedTick -ne $created[1].PickupAppliedTick -or
        $resumed[0].PickupReceiptSequence -ne $resumed[1].PickupReceiptSequence -or
        $resumed[0].PickupSequence -ne $resumed[1].PickupSequence -or
        $resumed[0].PickupAppliedTick -ne $resumed[1].PickupAppliedTick) {
        throw 'Cooperative clients disagree about durable roster or loot ownership.'
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
function Assert-MultiplayerRecoveryEvidence($SessionDirectory, $ContentHash, $RulesetHash, [string]$Prefix = 'multiplayerrecovery') {
    $final = @()
    for ($slot = 1; $slot -le 2; $slot++) {
        $playerDirectory = Join-Path $SessionDirectory "player-$slot"
        $path = Join-Path $playerDirectory ($Prefix + '.json')
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
        $offlinePath = Join-Path $playerDirectory ($Prefix + '-offline-ready.json')
        $rejoinedPath = Join-Path $playerDirectory ($Prefix + '-rejoined.json')
        if (-not (Test-Path -LiteralPath $offlinePath) -or -not (Test-Path -LiteralPath $rejoinedPath)) {
            throw "Multiplayer recovery barriers are incomplete for player $slot."
        }
        $offline = Get-Content -LiteralPath $offlinePath -Raw | ConvertFrom-Json
        $rejoined = Get-Content -LiteralPath $rejoinedPath -Raw | ConvertFrom-Json
        if (-not $offline.Passed -or $offline.OfflineSeconds -lt 2 -or
            $offline.SnapshotFingerprint -cne $evidence.DisconnectedSnapshotFingerprint -or
            $rejoined.EntityId -ne $evidence.EntityId -or $rejoined.PeerEntityId -ne $evidence.PeerEntityId) {
            throw "Multiplayer recovery did not prove bounded frozen offline/restored state for player $slot."
        }
        if ($Prefix -eq 'multiplayerpendingrecovery' -and
            (-not ($evidence.FrozenCommand -is [bool]) -or -not $evidence.FrozenCommand -or
             $evidence.DuplicateStatus -cne 'Duplicate' -or $evidence.RetryTransmissions -lt 1 -or
             $evidence.PendingSequence -ne ($evidence.Sequence - 1) -or
             $evidence.PendingReceiptSequence -ge $evidence.ReceiptSequence -or
             $offline.PendingCount -ne 1 -or $offline.PendingSequence -ne $evidence.PendingSequence -or
             $rejoined.PendingCount -ne 0 -or $rejoined.PendingSequence -ne $evidence.PendingSequence -or
             $rejoined.PendingReceiptSequence -ne $evidence.PendingReceiptSequence -or
             $rejoined.PendingAppliedTick -ne $evidence.PendingAppliedTick)) {
            throw 'Pending recovery did not prove exact original retry/receipt continuation.'
        }
        $commandPath = Join-Path $playerDirectory ("$Prefix-command-{0}.json" -f $evidence.Sequence)
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
$presentationEnvironmentNames = @('GODEVILUTION_PRESENTATION_PACK', 'GODEVILUTION_ACCEPTANCE_PRESENTATION_PACK')
$savedPresentationEnvironment = @{}
foreach ($name in $presentationEnvironmentNames) {
    $savedPresentationEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    # A fixture selection never changes normal launch settings or authoritative content.
    $env:GODEVILUTION_ACCEPTANCE_PRESENTATION_PACK = $PresentationPackProfile
    [Environment]::SetEnvironmentVariable('GODEVILUTION_PRESENTATION_PACK', $null, 'Process')
    if ($PresentationPackProfile -ne 'default') {
        $env:GODEVILUTION_PRESENTATION_PACK = "res://assets/presentation-acceptance-$PresentationPackProfile.json"
    }
    if ($CathedralDoorProfileDirectory) {
        $parameters.CathedralDoorProfileDirectory = $CathedralDoorProfileDirectory
        $parameters.QuitAfterSeconds = 300
        & $launcher @parameters -AcceptancePhase CathedralDoorJourney
        $parameters.QuitAfterSeconds = 180
        & $launcher @parameters -AcceptancePhase CathedralDoorResume
        $created = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedraldoorjourney-processes.json') -Raw | ConvertFrom-Json
        $resumed = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedraldoorresume-processes.json') -Raw | ConvertFrom-Json
        if (($created.HostProcessId -eq $resumed.HostProcessId -and $created.HostStartedUtc -eq $resumed.HostStartedUtc) -or
            ($created.Clients[0].ProcessId -eq $resumed.Clients[0].ProcessId -and $created.Clients[0].StartedUtc -eq $resumed.Clients[0].StartedUtc) -or
            $created.ProfileHash -cne $resumed.ProfileHash -or $created.ContentTreeHash -cne $resumed.ContentTreeHash -or
            $created.ProfileKind -cne 'cathedral-doors-playtest' -or $resumed.ProfileKind -cne 'cathedral-doors-playtest') {
            throw 'Cathedral door resume requires fresh host/client processes and the same isolated door profile.'
        }
        Write-Host "PASS: actual Godot Cathedral mutable-door journey, occupied blocking, close/reopen and fresh-host restore. Evidence: $sessionDirectory"
        return
    }
    if ($CathedralDepthProfileDirectory) {
        $depthProfile = Get-Content -LiteralPath (Join-Path $CathedralDepthProfileDirectory 'cathedral-depths-playtest.json') -Raw | ConvertFrom-Json
        $depthPortalRoute = $depthProfile.townPortalRoute -ceq 'town-portal-preview'
        $parameters.CathedralDepthProfileDirectory = $CathedralDepthProfileDirectory
        $parameters.QuitAfterSeconds = 900
        & $launcher @parameters -AcceptancePhase CathedralDepthJourney
        $parameters.QuitAfterSeconds = if ($depthPortalRoute) { 900 } else { 450 }
        & $launcher @parameters -AcceptancePhase CathedralDepthResume
        $created = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedraldepthjourney-processes.json') -Raw | ConvertFrom-Json
        $resumed = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedraldepthresume-processes.json') -Raw | ConvertFrom-Json
        if (($created.HostProcessId -eq $resumed.HostProcessId -and $created.HostStartedUtc -eq $resumed.HostStartedUtc) -or
            ($created.Clients[0].ProcessId -eq $resumed.Clients[0].ProcessId -and $created.Clients[0].StartedUtc -eq $resumed.Clients[0].StartedUtc) -or
            $created.ProfileHash -cne $resumed.ProfileHash -or $created.ContentTreeHash -cne $resumed.ContentTreeHash) {
            throw 'Cathedral depth resume requires fresh host/client processes and the same isolated profile.'
        }
        $depthState = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedral-depth-state.json') -Raw | ConvertFrom-Json
        if ($depthState.AuthoredDiagnostic) {
            $depthResumeState = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedral-depth-resume-state.json') -Raw | ConvertFrom-Json
            if (-not $depthResumeState.Passed -or $depthResumeState.SavedDepth -ne 2 -or
                -not $depthResumeState.AuthoredDiagnostic -or [string]::IsNullOrWhiteSpace($depthResumeState.GeometryHash) -or
                $depthResumeState.GeometryHash -cne $depthState.GeometryHash) {
                throw 'The authored Cathedral resume record is missing or changed its saved geometry.'
            }
            $gameplay = $depthResumeState.AuthoredDiagnostic
            if (-not $gameplay.ReturnedToTown -or -not $gameplay.RepairButtonDisabled -or
                $gameplay.RepairCommandQueued -or $gameplay.RepairQuoteReason -cne 'NotAllowed' -or
                $gameplay.RepairQuotePrice -ne 0 -or $gameplay.GoldAfterSmith -ne $gameplay.Gold -or
                $gameplay.SwordDurabilityAfterSmith -ne $gameplay.SwordDurability) {
                throw 'The authored Cathedral route did not retain its earned equipment and finish the Town service inspection.'
            }
            if ($depthPortalRoute) {
                $purchase = $depthState.AuthoredDiagnostic.TownPortalRoute
                $portal = $gameplay.TownPortalRoute
                if (-not $purchase -or -not $portal -or -not $portal.Passed -or
                    $purchase.ScrollStableItemId -ne 2119 -or $purchase.ScrollSeed -ne 9100 -or
                    $purchase.ScrollSpellId -ne 39 -or $purchase.ScrollPrice -ne 50 -or
                    $purchase.GoldAfterPurchase -ne 50 -or
                    $portal.ScrollStableItemId -ne $purchase.ScrollStableItemId -or
                    $portal.ScrollSeed -ne $purchase.ScrollSeed -or $portal.ScrollSpellId -ne $purchase.ScrollSpellId -or
                    $portal.ScrollPrice -ne $purchase.ScrollPrice -or $portal.GoldAfterPurchase -ne $purchase.GoldAfterPurchase -or
                    -not $portal.ScrollPreservedAtResume -or -not $portal.ScrollConsumedAfterCast -or
                    $portal.PortalId -le 0 -or $portal.PortalSourceLevelId -ne 1 -or $portal.PortalDestinationLevelId -ne 0 -or
                    -not $portal.ReturnedToTown -or -not $portal.SwordRoundTripComplete -or
                    $portal.SwordStableItemId -ne $gameplay.SwordStableItemId -or
                    [string]::IsNullOrWhiteSpace($portal.SwordStateSha256) -or
                    $portal.SwordStateSha256 -cne $gameplay.SwordStateSha256 -or
                    $null -eq $portal.SwordStashCell -or $portal.SwordStashCell -lt 0 -or
                    $portal.GoldAfterRoundTrip -ne $purchase.GoldAfterPurchase -or
                    $gameplay.GoldAfterSmith -ne $purchase.GoldAfterPurchase -or
                    $null -eq $portal.StashGoldBeforeRoundTrip -or $null -eq $portal.StashGoldAfterRoundTrip -or
                    $portal.StashGoldAfterRoundTrip -ne $portal.StashGoldBeforeRoundTrip) {
                    throw 'The composed Cathedral route did not preserve its purchased scroll, return through its portal and retain the earned sword through stash transfer.'
                }
                Write-Host "PASS: actual Godot Cathedral combat/equipment, cold save/resume, purchased portal return and earned-sword stash round trip. Evidence: $sessionDirectory"
            } else {
                Write-Host "PASS: actual Godot Cathedral combat/loot/equipment, off-level fresh-host restore and Town service inspection. Evidence: $sessionDirectory"
            }
        } else {
            Write-Host "PASS: actual Godot Cathedral depths 1-4 descent/ascent, middle-floor fresh-host restore and native Town return. Evidence: $sessionDirectory"
        }
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
    if ($MultiplayerRecovery -or $MultiplayerPendingRecovery) {
        $parameters.Multiplayer = $true
        $parameters.PersistenceMode = 'cohort'
        $parameters.QuitAfterSeconds = 75
        & $launcher @parameters -AcceptancePhase MultiplayerCreate
        $created = Read-ModeProcessEvidence 'MultiplayerCreate'
        & $launcher @parameters -AcceptancePhase MultiplayerResume
        $resumed = Read-ModeProcessEvidence 'MultiplayerResume'
        Assert-FreshProcessEvidence $created $resumed 2
        $parameters.QuitAfterSeconds = 105
        $recoveryPhase = if ($MultiplayerPendingRecovery) { 'MultiplayerPendingRecovery' } else { 'MultiplayerRecovery' }
        & $launcher @parameters -AcceptancePhase $recoveryPhase
        $recovery = Read-ModeProcessEvidence $recoveryPhase
        Assert-MultiplayerRecoveryProcessEvidence $recovery
        if ($MultiplayerPendingRecovery -and
            (-not $recovery.PendingLiveBoundary.Verified -or -not $recovery.PendingStoppedBoundary.Verified)) {
            throw 'Pending recovery omitted independent live and stopped canonical receipt boundaries.'
        }
        Assert-MultiplayerRecoveryEvidence $sessionDirectory $recovery.ContentHash $recovery.RulesetHash $recoveryPhase.ToLowerInvariant()
        Write-Host "PASS: bounded actual-Godot complete-cohort multiplayer host-loss/reconnect with two retained clients, stable roster, post-reconnect accepted controls, canonical receipts, and clean exits. Evidence: $sessionDirectory"
        return
    }
    if ($cooperative) {
        $parameters.Multiplayer = $true
        $parameters.PreviewContent = $true
        $parameters.PersistenceMode = 'cohort'
        $parameters.QuitAfterSeconds = 300
        & $launcher @parameters -AcceptancePhase CooperativeJourney
        $created = Read-ModeProcessEvidence 'CooperativeJourney'
        $parameters.QuitAfterSeconds = 240
        & $launcher @parameters -AcceptancePhase CooperativeResume
        $resumed = Read-ModeProcessEvidence 'CooperativeResume'
        Assert-FreshProcessEvidence $created $resumed 2
        Assert-CooperativeEvidence $sessionDirectory $created.ContentHash $created.RulesetHash ([bool]$Visible)
        Write-Host "PASS: two actual Godot clients, authored dungeon combat/loot ownership, reverse-order fresh-host complete-cohort resume. Evidence: $sessionDirectory"
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
        $stoppedMarker = Get-Content -LiteralPath (Join-Path $sessionDirectory 'recovery-server-stopped.json') -Raw | ConvertFrom-Json
        Assert-RecoveryProcessEvidence $recovery $stoppedMarker
        Write-Host "PASS: actual Godot create/move/checkpoint/fresh-host resume and server-loss/reconnect. Evidence: $sessionDirectory"
} finally {
    foreach ($name in $presentationEnvironmentNames) {
        [Environment]::SetEnvironmentVariable($name, $savedPresentationEnvironment[$name], 'Process')
    }
    # Retain isolated saves/logs for diagnosis; never modify normal launcher saves.
    Write-Host "Acceptance artifacts retained at $sessionDirectory"
}
