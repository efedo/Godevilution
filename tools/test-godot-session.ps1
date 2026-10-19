[CmdletBinding()]
param([string]$GodotExecutable, [ValidateSet('legacy', 'cohort')] [string]$PersistenceMode = 'legacy', [switch]$Visible, [switch]$Journey, [switch]$TownLoop, [switch]$Lifecycle, [switch]$Multiplayer, [switch]$MultiplayerRecovery, [switch]$MultiplayerPendingRecovery, [string]$CathedralProfileDirectory, [string]$CathedralDepthProfileDirectory, [string]$CathedralDoorProfileDirectory, [string]$CathedralNativeProfileDirectory, [ValidateSet('default', 'atlas', 'semantic', 'missing', 'undersized', 'invalid')] [string]$PresentationPackProfile = 'default', [switch]$CommandTimingTrace, [switch]$Cooperative, [switch]$CathedralNativeChest, [string]$CathedralNativeChestSessionDirectory, [switch]$CathedralNativeSarcophagus, [string]$CathedralNativeSarcophagusSessionDirectory, [switch]$CathedralNativeBarrel, [string]$CathedralNativeBarrelSessionDirectory, [switch]$CathedralNativeTrap, [string]$CathedralNativeTrapSessionDirectory)

$ErrorActionPreference = 'Stop'
$PersistenceMode = $PersistenceMode.ToLowerInvariant()
$PresentationPackProfile = $PresentationPackProfile.ToLowerInvariant()
$cathedralNativeProfile = -not [string]::IsNullOrWhiteSpace($CathedralNativeProfileDirectory)
$cathedralNativeTrap = [bool]$CathedralNativeTrap
if ($cathedralNativeTrap) {
    $trapConflicts = @('Visible', 'Journey', 'TownLoop', 'Lifecycle', 'Multiplayer',
        'MultiplayerRecovery', 'MultiplayerPendingRecovery', 'Cooperative', 'CathedralNativeChest',
        'CathedralNativeSarcophagus', 'CathedralNativeBarrel', 'CathedralNativeChestSessionDirectory',
        'CathedralNativeSarcophagusSessionDirectory', 'CathedralNativeBarrelSessionDirectory',
        'CathedralProfileDirectory', 'CathedralDepthProfileDirectory', 'CathedralDoorProfileDirectory',
        'CommandTimingTrace')
    if (-not $cathedralNativeProfile -or $PersistenceMode -cne 'cohort' -or
        $PresentationPackProfile -ne 'default' -or
        @($trapConflicts | Where-Object { $PSBoundParameters.ContainsKey($_) -and $PSBoundParameters[$_] }).Count -ne 0 -or
        [string]::IsNullOrWhiteSpace($CathedralNativeTrapSessionDirectory) -or
        -not [IO.Path]::IsPathFullyQualified($CathedralNativeTrapSessionDirectory)) {
        throw 'Native trap acceptance requires the pinned profile, copied Town cohort, and headless single-player mode.'
    }
    $CathedralNativeTrapSessionDirectory = [IO.Path]::GetFullPath($CathedralNativeTrapSessionDirectory)
} elseif (-not [string]::IsNullOrWhiteSpace($CathedralNativeTrapSessionDirectory)) {
    throw 'CathedralNativeTrapSessionDirectory is only valid with -CathedralNativeTrap.'
}
$cathedralNativeBarrel = [bool]$CathedralNativeBarrel
if ($cathedralNativeBarrel) {
    $barrelConflicts = @('Visible', 'Journey', 'TownLoop', 'Lifecycle', 'Multiplayer',
        'MultiplayerRecovery', 'MultiplayerPendingRecovery', 'Cooperative', 'SessionMenu',
        'TrapDisarm', 'WarriorRepair', 'BardIdentify', 'MonkSearch', 'CathedralNativeChest',
        'CathedralNativeSarcophagus', 'CathedralNativeChestSessionDirectory', 'CathedralNativeSarcophagusSessionDirectory',
        'CathedralProfileDirectory', 'CathedralDepthProfileDirectory', 'CathedralDoorProfileDirectory', 'CommandTimingTrace')
    if (-not $cathedralNativeProfile -or $PersistenceMode -cne 'cohort' -or
        $PresentationPackProfile -ne 'default' -or
        @($barrelConflicts | Where-Object { $PSBoundParameters.ContainsKey($_) -and $PSBoundParameters[$_] }).Count -ne 0 -or
        [string]::IsNullOrWhiteSpace($CathedralNativeBarrelSessionDirectory) -or
        -not [IO.Path]::IsPathFullyQualified($CathedralNativeBarrelSessionDirectory)) {
        throw 'Native barrel acceptance requires a pinned profile, copied sarcophagus Town cohort and headless single-player mode.'
    }
    $CathedralNativeBarrelSessionDirectory = [IO.Path]::GetFullPath($CathedralNativeBarrelSessionDirectory)
} elseif (-not [string]::IsNullOrWhiteSpace($CathedralNativeBarrelSessionDirectory)) {
    throw 'CathedralNativeBarrelSessionDirectory is only valid with -CathedralNativeBarrel.'
}
$cathedralNativeSarcophagus = [bool]$CathedralNativeSarcophagus
if ($cathedralNativeSarcophagus) {
    $sarcophagusConflicts = @('Visible', 'Journey', 'TownLoop', 'Lifecycle', 'Multiplayer',
        'MultiplayerRecovery', 'MultiplayerPendingRecovery', 'Cooperative', 'SessionMenu',
        'TrapDisarm', 'WarriorRepair', 'BardIdentify', 'MonkSearch', 'CathedralNativeChest',
        'CathedralProfileDirectory', 'CathedralDepthProfileDirectory', 'CathedralDoorProfileDirectory', 'CommandTimingTrace')
    if (-not $cathedralNativeProfile -or $PersistenceMode -cne 'cohort' -or
        $PresentationPackProfile -ne 'default' -or
        @($sarcophagusConflicts | Where-Object { $PSBoundParameters.ContainsKey($_) -and $PSBoundParameters[$_] }).Count -ne 0 -or
        [string]::IsNullOrWhiteSpace($CathedralNativeSarcophagusSessionDirectory) -or
        -not [IO.Path]::IsPathFullyQualified($CathedralNativeSarcophagusSessionDirectory)) {
        throw 'Native sarcophagus acceptance requires its pinned profile, copied absolute Town cohort, and headless single-player mode.'
    }
    $CathedralNativeSarcophagusSessionDirectory = [IO.Path]::GetFullPath($CathedralNativeSarcophagusSessionDirectory)
} elseif (-not [string]::IsNullOrWhiteSpace($CathedralNativeSarcophagusSessionDirectory)) {
    throw 'CathedralNativeSarcophagusSessionDirectory is only valid with -CathedralNativeSarcophagus.'
}
$cathedralNativeChest = [bool]$CathedralNativeChest
if ($cathedralNativeChest) {
    if (-not $cathedralNativeProfile -or $PersistenceMode -cne 'cohort' -or $Visible -or
        $Journey -or $TownLoop -or $Lifecycle -or $Multiplayer -or $MultiplayerRecovery -or $MultiplayerPendingRecovery -or
        $Cooperative -or
        $CathedralProfileDirectory -or $CathedralDepthProfileDirectory -or $CathedralDoorProfileDirectory -or
        $PresentationPackProfile -ne 'default' -or $CommandTimingTrace -or
        [string]::IsNullOrWhiteSpace($CathedralNativeChestSessionDirectory) -or
        -not [IO.Path]::IsPathFullyQualified($CathedralNativeChestSessionDirectory)) {
        throw 'Native chest acceptance requires the pinned profile, a copied absolute cohort session, and headless single-player mode.'
    }
    $CathedralNativeChestSessionDirectory = [IO.Path]::GetFullPath($CathedralNativeChestSessionDirectory)
} elseif (-not [string]::IsNullOrWhiteSpace($CathedralNativeChestSessionDirectory)) {
    throw 'CathedralNativeChestSessionDirectory is only valid with -CathedralNativeChest.'
}
if ($cathedralNativeProfile -and ($PersistenceMode -cne 'cohort' -or $Visible -or
    $Journey -or $TownLoop -or $Lifecycle -or $Multiplayer -or $MultiplayerRecovery -or $MultiplayerPendingRecovery -or
    $Cooperative -or $CathedralProfileDirectory -or $CathedralDepthProfileDirectory -or $CathedralDoorProfileDirectory -or
    $PresentationPackProfile -ne 'default' -or $CommandTimingTrace)) {
    throw 'Native Cathedral journey/resume requires its pinned profile, headless cohort persistence, and no other acceptance mode.'
}
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
$sessionDirectory = if ($cathedralNativeTrap) {
    $nativeSessionsRoot = Join-Path ([IO.Path]::GetFullPath($CathedralNativeProfileDirectory)) 'sessions'
    $nativeTrapSessionRoot = [IO.Path]::GetFullPath($CathedralNativeTrapSessionDirectory)
    $sessionsPrefix = $nativeSessionsRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $nativeTrapSessionRoot.StartsWith($sessionsPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath (Join-Path $nativeTrapSessionRoot 'resume-token.txt') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $nativeTrapSessionRoot 'save/authoritative-cohort.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $nativeTrapSessionRoot 'cathedral-native-population-session.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $nativeTrapSessionRoot 'cathedralnativebarreljourney.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $nativeTrapSessionRoot 'cathedralnativebarrelresume.json') -PathType Leaf)) {
        throw 'Native trap acceptance requires a copied passed R14 Town checkpoint under the pinned profile sessions directory.'
    }
    $r14Resume = Get-Content -LiteralPath (Join-Path $nativeTrapSessionRoot 'cathedralnativebarrelresume.json') -Raw | ConvertFrom-Json
    if ($r14Resume.Passed -isnot [bool] -or -not $r14Resume.Passed -or
        $r14Resume.LevelId -ne 0 -or [uint64]$r14Resume.Sequence -ne 539 -or
        $r14Resume.Visible -isnot [bool] -or $r14Resume.Visible) {
        throw 'Native trap acceptance requires the exact passed R14 Town cursor-539 checkpoint.'
    }
    $nativeTrapSessionRoot
} elseif ($cathedralNativeBarrel) {
    $barrelSessionsRoot = Join-Path ([IO.Path]::GetFullPath($CathedralNativeProfileDirectory)) 'sessions'
    $barrelSessionRoot = [IO.Path]::GetFullPath($CathedralNativeBarrelSessionDirectory)
    if (-not $barrelSessionRoot.StartsWith($barrelSessionsRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath (Join-Path $barrelSessionRoot 'resume-token.txt') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $barrelSessionRoot 'save/authoritative-cohort.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $barrelSessionRoot 'cathedral-native-population-session.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $barrelSessionRoot 'cathedralnativesarcophagusresume.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $barrelSessionRoot 'cathedralnativesarcophagusjourney.json') -PathType Leaf)) {
        throw 'Native barrel acceptance requires a copied passed sarcophagus Town cohort under the pinned profile.'
    }
    $barrelSessionRoot
} elseif ($cathedralNativeSarcophagus) {
    $nativeSessionsRoot = Join-Path ([IO.Path]::GetFullPath($CathedralNativeProfileDirectory)) 'sessions'
    $sarcophagusSessionRoot = [IO.Path]::GetFullPath($CathedralNativeSarcophagusSessionDirectory)
    if (-not $sarcophagusSessionRoot.StartsWith($nativeSessionsRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath (Join-Path $sarcophagusSessionRoot 'resume-token.txt') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $sarcophagusSessionRoot 'save/authoritative-cohort.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $sarcophagusSessionRoot 'cathedral-native-population-session.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $sarcophagusSessionRoot 'cathedralnativechestresume.json') -PathType Leaf)) {
        throw 'Native sarcophagus acceptance requires a copied passed chest Town cohort under its pinned profile sessions directory.'
    }
    $sarcophagusSessionRoot
} elseif ($cathedralNativeChest) {
    $nativeProfileRoot = [IO.Path]::GetFullPath($CathedralNativeProfileDirectory).TrimEnd('\', '/')
    $nativeSessionsRoot = Join-Path $nativeProfileRoot 'sessions'
    $nativeChestSessionRoot = [IO.Path]::GetFullPath($CathedralNativeChestSessionDirectory)
    $sessionsPrefix = $nativeSessionsRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $nativeChestSessionRoot.StartsWith($sessionsPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath (Join-Path $nativeChestSessionRoot 'resume-token.txt') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $nativeChestSessionRoot 'cathedral-native-population-session.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $nativeChestSessionRoot 'save/authoritative-cohort.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $nativeChestSessionRoot 'cathedralnativeresume.json') -PathType Leaf)) {
        throw 'Native chest acceptance requires the supplied copied Town cohort, its native profile marker, and the passed final R3 resume evidence.'
    }
    $nativeChestSessionRoot
} elseif ($CathedralProfileDirectory -or $CathedralDepthProfileDirectory -or $CathedralDoorProfileDirectory -or $CathedralNativeProfileDirectory) {
    $profileDirectory = if ($CathedralNativeProfileDirectory) { $CathedralNativeProfileDirectory } elseif ($CathedralDepthProfileDirectory) { $CathedralDepthProfileDirectory } elseif ($CathedralDoorProfileDirectory) { $CathedralDoorProfileDirectory } else { $CathedralProfileDirectory }
    Join-Path ([IO.Path]::GetFullPath($profileDirectory)) ('sessions/acceptance-' + [guid]::NewGuid().ToString('N'))
} else { Join-Path ([System.IO.Path]::GetTempPath()) ('Godevilution-Scene-Acceptance-' + [guid]::NewGuid().ToString('N')) }
$launcher = Join-Path $repositoryRoot 'godot-client/run-local-client.ps1'
$parameters = @{ SessionDirectory = $sessionDirectory; PersistenceMode = $PersistenceMode; Headless = -not $Visible; QuitAfterSeconds = 45 }
if ($CommandTimingTrace) { $parameters.CommandTimingTrace = $true }
if ($CathedralNativeProfileDirectory) { $parameters.CathedralNativeProfileDirectory = $CathedralNativeProfileDirectory }
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
    if ($Phase -in @('CooperativeJourney', 'CooperativeResume', 'CathedralNativeJourney', 'CathedralNativeResume', 'CathedralNativeChestJourney', 'CathedralNativeChestResume', 'CathedralNativeSarcophagusJourney', 'CathedralNativeSarcophagusResume', 'CathedralNativeBarrelJourney', 'CathedralNativeBarrelResume', 'CathedralNativeTrapJourney', 'CathedralNativeTrapResume') -and
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
function Assert-NativeEvidenceHash([object]$Value, [string]$Name) {
    if ($Value -isnot [string] -or [string]$Value -cnotmatch '^[0-9a-fA-F]{64}$') {
        throw "Native Cathedral evidence omitted a valid $Name SHA-256 value."
    }
}
function Assert-NativeEvidenceInteger([object]$Value, [string]$Name, [long]$Minimum = 0, [long]$Maximum = [long]::MaxValue) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt $Minimum -or $Value -gt $Maximum) {
        throw "Native Cathedral evidence contains an invalid $Name integer."
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
    if ($CathedralNativeTrap) {
        $parameters.QuitAfterSeconds = 1200
        & $launcher @parameters -AcceptancePhase CathedralNativeTrapJourney
        $trapJourneyProcess = Read-ModeProcessEvidence 'CathedralNativeTrapJourney'
        $trapCheckpointPath = Join-Path (Split-Path -Parent $nativeTrapSessionRoot) ('native-trap-town-checkpoint-' + [guid]::NewGuid().ToString('N'))
        if (Test-Path -LiteralPath $trapCheckpointPath) { throw 'The unique native trap checkpoint destination already exists.' }
        New-Item -ItemType Directory -Path $trapCheckpointPath -ErrorAction Stop | Out-Null
        foreach ($checkpointEntry in (Get-ChildItem -LiteralPath $nativeTrapSessionRoot -Force)) {
            if ($checkpointEntry.Name -ceq 'launcher.lock') { continue }
            Copy-Item -LiteralPath $checkpointEntry.FullName -Destination $trapCheckpointPath -Recurse
        }
        if (-not (Test-Path -LiteralPath (Join-Path $trapCheckpointPath 'resume-token.txt') -PathType Leaf) -or
            -not (Test-Path -LiteralPath (Join-Path $trapCheckpointPath 'save/authoritative-cohort.json') -PathType Leaf)) {
            throw 'The preserved native trap Town checkpoint is incomplete.'
        }
        Write-Host "Preserved native trap Town checkpoint: $trapCheckpointPath"
        $sessionDirectory = $trapCheckpointPath
        $parameters.SessionDirectory = $trapCheckpointPath
        $parameters.QuitAfterSeconds = 600
        & $launcher @parameters -AcceptancePhase CathedralNativeTrapResume
        $trapResumeProcess = Read-ModeProcessEvidence 'CathedralNativeTrapResume'
        Assert-FreshProcessEvidence $trapJourneyProcess $trapResumeProcess 1
        foreach ($field in @('ContentHash', 'RulesetHash', 'ProfileHash', 'ContentTreeHash',
            'PopulationTreeHash', 'RuntimeTreeHash', 'ProfileKind', 'AdapterId')) {
            if ($trapJourneyProcess.$field -cne $trapResumeProcess.$field) {
                throw "Native trap resume changed its pinned $field identity."
            }
        }
        $trapProfilePath = Join-Path $CathedralNativeProfileDirectory 'cathedral-native-population-playtest.json'
        $trapProfileHash = (Get-FileHash -LiteralPath $trapProfilePath -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($trapProfileHash -cne 'A992857810C51CE2A802588D8F3386D94D4C6AA44127394553D1E797B9F82FC1' -or
            $trapJourneyProcess.ProfileHash -cne $trapProfileHash -or
            $trapJourneyProcess.ProfileKind -cne 'cathedral-native-population-playtest' -or
            $trapJourneyProcess.AdapterId -cne 'devilution.diablo.cathedral-native-population.v1') {
            throw 'Native trap acceptance did not use the pinned population profile.'
        }
        $trapPriorEvidence = Get-Content -LiteralPath (Join-Path $trapCheckpointPath 'cathedralnativebarrelresume.json') -Raw | ConvertFrom-Json
        $trapJourneyEvidence = Get-Content -LiteralPath (Join-Path $trapCheckpointPath 'cathedralnativetrapjourney.json') -Raw | ConvertFrom-Json
        $trapResumeEvidence = Get-Content -LiteralPath (Join-Path $trapCheckpointPath 'cathedralnativetrapresume.json') -Raw | ConvertFrom-Json
        foreach ($evidence in @($trapJourneyEvidence, $trapResumeEvidence)) {
            if ($evidence.Passed -isnot [bool] -or -not $evidence.Passed -or
                $evidence.Visible -isnot [bool] -or $evidence.Visible -or
                $evidence.Mode -cne 'SinglePlayer' -or $evidence.LevelId -ne 0 -or
                $evidence.ClassId -ne 1 -or $evidence.ContentHash -cne $trapJourneyProcess.ContentHash -or
                $evidence.RulesetHash -cne $trapJourneyProcess.RulesetHash) {
                throw 'Native trap phase did not pass from and return to Town under its pinned headless host.'
            }
            Assert-NativeEvidenceInteger $evidence.EntityId 'native trap player identity' 1 ([uint32]::MaxValue)
            Assert-NativeEvidenceInteger $evidence.Sequence 'native trap command sequence' 1 ([long]::MaxValue)
            Assert-NativeEvidenceInteger $evidence.X 'native trap Town X' 0 111
            Assert-NativeEvidenceInteger $evidence.Y 'native trap Town Y' 0 111
        }
        $trapJourney = $trapJourneyEvidence.NativeTrap
        $trapResume = $trapResumeEvidence.NativeTrap
        if (-not $trapPriorEvidence.Passed -or [uint64]$trapPriorEvidence.Sequence -ne 539 -or
            [uint64]$trapJourney.PriorSequence -ne 539 -or
            [uint64]$trapJourney.Sequence -ne [uint64]$trapJourneyEvidence.Sequence -or
            [uint64]$trapJourney.DisarmSequence -le 539 -or
            [uint64]$trapJourney.DisarmAppliedTick -le 0 -or
            $trapJourney.DisarmIntent -cne 'DisarmTrapRequested' -or
            [uint64]$trapJourney.Sequence -le [uint64]$trapJourney.DisarmSequence -or
            [uint32]$trapJourney.TrapEntityId -ne 1778404889 -or
            $trapJourney.NativeTrapInteractableBefore -isnot [bool] -or -not $trapJourney.NativeTrapInteractableBefore -or
            $trapJourney.NativeTrapInteractableAfter -isnot [bool] -or $trapJourney.NativeTrapInteractableAfter -or
            $trapJourney.NativeObjectId -ne 2 -or $trapJourney.X -ne 57 -or $trapJourney.Y -ne 66 -or
            $trapJourney.DoorStateBefore -ne 0 -or $trapJourney.DoorStateAfter -notin @(1, 2) -or
            [uint64]$trapResume.SavedSequence -ne [uint64]$trapJourney.Sequence -or
            [uint64]$trapResume.FinalSequence -ne [uint64]$trapResumeEvidence.Sequence -or
            [uint64]$trapResume.FinalSequence -le [uint64]$trapJourney.Sequence -or
            $trapResume.ResumedSessionMatched -isnot [bool] -or -not $trapResume.ResumedSessionMatched -or
            $trapResume.L1RetentionVerified -isnot [bool] -or -not $trapResume.L1RetentionVerified -or
            $trapResume.ExactWorldRestoreCheckedBeforeInput -isnot [bool] -or -not $trapResume.ExactWorldRestoreCheckedBeforeInput -or
            $trapResume.LiveStateAllowedToAdvanceOnRevisit -isnot [bool] -or -not $trapResume.LiveStateAllowedToAdvanceOnRevisit -or
            $trapResume.RevisitLevelId -ne 1 -or
            $trapResume.RepeatDisarmCapabilityUnavailable -isnot [bool] -or -not $trapResume.RepeatDisarmCapabilityUnavailable) {
            throw 'Native trap evidence lacks the accepted disarm/open transition or exact fresh-host revisit.'
        }
        $trapHealer = $trapResume.Healer
        if ($trapHealer.HealerId -ne 1 -or $trapHealer.Status -cne 'Accepted' -or
            $trapHealer.RejectReason -cne 'Unspecified' -or $trapHealer.AppliedTick -le 0 -or
            [uint64]$trapHealer.Sequence -le [uint64]$trapJourney.Sequence -or
            [uint64]$trapHealer.Sequence -ge [uint64]$trapResume.FinalSequence -or
            $trapHealer.LifeBefore -le 0 -or $trapHealer.LifeAfter -ne $trapHealer.LifeMaximum -or
            $trapHealer.ManaBefore -ne $trapHealer.ManaAfter -or
            $trapHealer.GoldBefore -ne $trapHealer.GoldAfter -or
            $trapHealer.ExperienceBefore -ne $trapHealer.ExperienceAfter -or
            $trapHealer.StashGoldBefore -ne $trapHealer.StashGoldAfter -or
            ($trapHealer.ItemsBefore | ConvertTo-Json -Depth 10 -Compress) -cne
                ($trapHealer.ItemsAfter | ConvertTo-Json -Depth 10 -Compress) -or
            $trapResume.FinalLife -le 0) {
            throw 'Native trap resume did not heal through the Town service before its live revisit.'
        }
        foreach ($field in @('TrapObjectSha256', 'L1GeometrySha256', 'ProjectilesSha256',
            'WorldItemsSha256', 'CorpsesSha256', 'MonstersSha256')) {
            Assert-NativeEvidenceHash $trapJourney.$field "native trap journey $field"
            Assert-NativeEvidenceHash $trapResume.$field "native trap resume $field"
            if ($trapJourney.$field -cne $trapResume.$field) {
                throw "Native trap resume changed retained $field."
            }
        }
        Write-Host "PASS: Native Rogue DisarmTrap action, ordinary door operation, Town checkpoint and fresh-host L1 retention. Evidence: $trapCheckpointPath"
        return
    }
    if ($CathedralNativeBarrel) {
        $parameters.QuitAfterSeconds = 1200
        & $launcher @parameters -AcceptancePhase CathedralNativeBarrelJourney
        $barrelCreatedProcess = Read-ModeProcessEvidence 'CathedralNativeBarrelJourney'
        $barrelCheckpointPath = Join-Path (Split-Path -Parent $barrelSessionRoot) ('barrel-town-checkpoint-' + [guid]::NewGuid().ToString('N'))
        if (Test-Path -LiteralPath $barrelCheckpointPath) { throw 'The unique barrel checkpoint destination already exists.' }
        New-Item -ItemType Directory -Path $barrelCheckpointPath -ErrorAction Stop | Out-Null
        foreach ($checkpointEntry in (Get-ChildItem -LiteralPath $barrelSessionRoot -Force)) {
            if ($checkpointEntry.Name -ceq 'launcher.lock') { continue }
            Copy-Item -LiteralPath $checkpointEntry.FullName -Destination $barrelCheckpointPath -Recurse
        }
        if (-not (Test-Path -LiteralPath (Join-Path $barrelCheckpointPath 'resume-token.txt') -PathType Leaf) -or
            -not (Test-Path -LiteralPath (Join-Path $barrelCheckpointPath 'save/authoritative-cohort.json') -PathType Leaf)) {
            throw 'The preserved barrel Town checkpoint is incomplete.'
        }
        Write-Host "Preserved barrel Town checkpoint: $barrelCheckpointPath"
        $parameters.QuitAfterSeconds = 600
        & $launcher @parameters -AcceptancePhase CathedralNativeBarrelResume
        $barrelResumedProcess = Read-ModeProcessEvidence 'CathedralNativeBarrelResume'
        Assert-FreshProcessEvidence $barrelCreatedProcess $barrelResumedProcess 1
        foreach ($field in @('ContentHash', 'RulesetHash', 'ProfileHash', 'ContentTreeHash',
            'PopulationTreeHash', 'RuntimeTreeHash', 'ProfileKind', 'AdapterId')) {
            if ($barrelCreatedProcess.$field -cne $barrelResumedProcess.$field) {
                throw "Native barrel resume changed its pinned $field identity."
            }
        }
        $barrelProfilePath = Join-Path $CathedralNativeProfileDirectory 'cathedral-native-population-playtest.json'
        $barrelProfileHash = (Get-FileHash -LiteralPath $barrelProfilePath -Algorithm SHA256).Hash
        if ($barrelProfileHash -cne 'A992857810C51CE2A802588D8F3386D94D4C6AA44127394553D1E797B9F82FC1' -or
            $barrelCreatedProcess.ProfileHash -cne $barrelProfileHash -or
            $barrelCreatedProcess.ProfileKind -cne 'cathedral-native-population-playtest' -or
            $barrelCreatedProcess.AdapterId -cne 'devilution.diablo.cathedral-native-population.v1') {
            throw 'Native barrel acceptance did not use the pinned population profile.'
        }
        $barrelPriorEvidence = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedralnativesarcophagusresume.json') -Raw | ConvertFrom-Json
        $barrelJourneyEvidence = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedralnativebarreljourney.json') -Raw | ConvertFrom-Json
        $barrelResumeEvidence = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedralnativebarrelresume.json') -Raw | ConvertFrom-Json
        foreach ($barrelPhaseEvidence in @($barrelJourneyEvidence, $barrelResumeEvidence)) {
            if ($barrelPhaseEvidence.Passed -isnot [bool] -or -not $barrelPhaseEvidence.Passed -or
                $barrelPhaseEvidence.Visible -isnot [bool] -or $barrelPhaseEvidence.Visible -or
                $barrelPhaseEvidence.Mode -cne 'SinglePlayer' -or $barrelPhaseEvidence.LevelId -ne 0 -or
                $barrelPhaseEvidence.ClassId -ne 1 -or $barrelPhaseEvidence.ContentHash -cne $barrelCreatedProcess.ContentHash -or
                $barrelPhaseEvidence.RulesetHash -cne $barrelCreatedProcess.RulesetHash) {
                throw 'Native barrel phase did not pass in Town under its pinned headless host.'
            }
            Assert-NativeEvidenceInteger $barrelPhaseEvidence.EntityId 'barrel player identity' 1 ([uint32]::MaxValue)
            Assert-NativeEvidenceInteger $barrelPhaseEvidence.Sequence 'barrel command sequence' 1 ([long]::MaxValue)
        }
        $barrelOpenedEvidence = @($barrelJourneyEvidence.NativeBarrel.Opened)
        $barrelLootEvidence = @($barrelOpenedEvidence | ForEach-Object { @($_.Loot) })
        $barrelReleasedEvidence = @($barrelOpenedEvidence | ForEach-Object { @($_.ReleasedSkeletons) })
        if (-not $barrelPriorEvidence.Passed -or
            [uint64]$barrelJourneyEvidence.NativeBarrel.PriorSequence -ne [uint64]$barrelPriorEvidence.Sequence -or
            [uint64]$barrelJourneyEvidence.Sequence -le [uint64]$barrelPriorEvidence.Sequence -or
            [uint64]$barrelJourneyEvidence.NativeBarrel.Sequence -ne [uint64]$barrelJourneyEvidence.Sequence -or
            [uint64]$barrelResumeEvidence.NativeBarrel.SavedSequence -ne [uint64]$barrelJourneyEvidence.Sequence -or
            [uint64]$barrelResumeEvidence.Sequence -le [uint64]$barrelJourneyEvidence.Sequence -or
            [uint64]$barrelResumeEvidence.NativeBarrel.FinalSequence -ne [uint64]$barrelResumeEvidence.Sequence -or
            $barrelResumeEvidence.NativeBarrel.ResumedSessionMatched -isnot [bool] -or
            -not $barrelResumeEvidence.NativeBarrel.ResumedSessionMatched -or
            $barrelJourneyEvidence.EntityId -ne $barrelResumeEvidence.EntityId -or
            $barrelOpenedEvidence.NativeTypeId -notcontains 57 -or $barrelOpenedEvidence.NativeTypeId -notcontains 58 -or
            $barrelJourneyEvidence.NativeBarrel.LootBranchExercised -isnot [bool] -or
            $barrelResumeEvidence.NativeBarrel.LootBranchExercised -isnot [bool] -or
            $barrelJourneyEvidence.NativeBarrel.LootBranchExercised -ne ($barrelLootEvidence.Count -gt 0) -or
            $barrelResumeEvidence.NativeBarrel.LootBranchExercised -ne $barrelJourneyEvidence.NativeBarrel.LootBranchExercised -or
            $barrelResumeEvidence.NativeBarrel.OpenedRetained -ne $barrelOpenedEvidence.Count -or
            $barrelResumeEvidence.NativeBarrel.ExactLootRetained -ne $barrelLootEvidence.Count -or
            $barrelResumeEvidence.NativeBarrel.ReleasedSkeletonCorpsesRetained -ne $barrelReleasedEvidence.Count -or
            $barrelResumeEvidence.NativeBarrel.RepeatDidNotBreakAgain -isnot [bool] -or
            -not $barrelResumeEvidence.NativeBarrel.RepeatDidNotBreakAgain) {
            throw 'Native barrel acceptance lacks old-save admission, both family branches, honest loot coverage or exact cold/repeat retention.'
        }
        foreach ($field in @('GeometrySha256', 'WorldItemsSha256')) {
            Assert-NativeEvidenceHash $barrelJourneyEvidence.NativeBarrel.$field "barrel journey $field"
            Assert-NativeEvidenceHash $barrelResumeEvidence.NativeBarrel.$field "barrel resume $field"
            if ($barrelJourneyEvidence.NativeBarrel.$field -cne $barrelResumeEvidence.NativeBarrel.$field) {
                throw "Native barrel resume changed retained $field."
            }
        }
        Write-Host "PASS: Native barrel melee/explosion, Town checkpoint and exact fresh-host collision/item/actor retention. Evidence: $sessionDirectory"
        return
    }

    if ($CathedralNativeSarcophagus) {
        $parameters.QuitAfterSeconds = 900
        & $launcher @parameters -AcceptancePhase CathedralNativeSarcophagusJourney
        $createdProcess = Read-ModeProcessEvidence 'CathedralNativeSarcophagusJourney'
        $parameters.QuitAfterSeconds = 600
        & $launcher @parameters -AcceptancePhase CathedralNativeSarcophagusResume
        $resumedProcess = Read-ModeProcessEvidence 'CathedralNativeSarcophagusResume'
        Assert-FreshProcessEvidence $createdProcess $resumedProcess 1
        foreach ($field in @('ContentHash', 'RulesetHash', 'ProfileHash', 'ContentTreeHash',
            'PopulationTreeHash', 'RuntimeTreeHash', 'ProfileKind', 'AdapterId')) {
            if ($createdProcess.$field -cne $resumedProcess.$field) {
                throw "Native sarcophagus resume changed its pinned $field identity."
            }
        }
        $profilePath = Join-Path $CathedralNativeProfileDirectory 'cathedral-native-population-playtest.json'
        $profileHash = (Get-FileHash -LiteralPath $profilePath -Algorithm SHA256).Hash
        if ($profileHash -cne 'A992857810C51CE2A802588D8F3386D94D4C6AA44127394553D1E797B9F82FC1' -or
            $createdProcess.ProfileHash -cne $profileHash -or
            $createdProcess.ProfileKind -cne 'cathedral-native-population-playtest' -or
            $createdProcess.AdapterId -cne 'devilution.diablo.cathedral-native-population.v1') {
            throw 'Native sarcophagus acceptance did not use the pinned population profile.'
        }
        $prior = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedralnativechestresume.json') -Raw | ConvertFrom-Json
        $sarcJourneyResult = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedralnativesarcophagusjourney.json') -Raw | ConvertFrom-Json
        $sarcResumeResult = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedralnativesarcophagusresume.json') -Raw | ConvertFrom-Json
        foreach ($evidence in @($sarcJourneyResult, $sarcResumeResult)) {
            if ($evidence.Passed -isnot [bool] -or -not $evidence.Passed -or
                $evidence.Visible -isnot [bool] -or $evidence.Visible -or
                $evidence.SpawnBranchExercised -isnot [bool] -or $evidence.SpawnBranchExercised -or
                $evidence.Mode -cne 'SinglePlayer' -or $evidence.LevelId -ne 0 -or
                $evidence.ClassId -ne 1 -or $evidence.ContentHash -cne $createdProcess.ContentHash -or
                $evidence.RulesetHash -cne $createdProcess.RulesetHash) {
                throw 'Native sarcophagus phase did not pass in Town under the pinned headless host.'
            }
            Assert-NativeEvidenceInteger $evidence.EntityId 'sarcophagus player identity' 1 ([uint32]::MaxValue)
            Assert-NativeEvidenceInteger $evidence.Sequence 'sarcophagus command sequence' 1 ([long]::MaxValue)
            Assert-NativeEvidenceInteger $evidence.X 'sarcophagus Town X' 0 111
            Assert-NativeEvidenceInteger $evidence.Y 'sarcophagus Town Y' 0 111
        }
        $opened = @($sarcJourneyResult.NativeSarcophagus.Opened)
        $loot = @($opened | ForEach-Object { @($_.Loot) })
        $released = @($opened | ForEach-Object { @($_.ReleasedSkeletons) })
        if ($prior.Passed -isnot [bool] -or -not $prior.Passed -or
            [uint64]$sarcJourneyResult.NativeSarcophagus.PriorSequence -ne [uint64]$prior.Sequence -or
            [uint64]$sarcJourneyResult.Sequence -le [uint64]$prior.Sequence -or
            [uint64]$sarcJourneyResult.NativeSarcophagus.Sequence -ne [uint64]$sarcJourneyResult.Sequence -or
            [uint64]$sarcResumeResult.NativeSarcophagus.SavedSequence -ne [uint64]$sarcJourneyResult.Sequence -or
            [uint64]$sarcResumeResult.Sequence -le [uint64]$sarcJourneyResult.Sequence -or
            [uint64]$sarcResumeResult.NativeSarcophagus.FinalSequence -ne [uint64]$sarcResumeResult.Sequence -or
            $sarcResumeResult.NativeSarcophagus.ResumedSessionMatched -isnot [bool] -or
            -not $sarcResumeResult.NativeSarcophagus.ResumedSessionMatched -or
            $sarcJourneyResult.EntityId -ne $sarcResumeResult.EntityId -or $opened.Count -lt 1 -or
            ($loot.Count -lt 1 -or $released.Count -ne 0) -or
            $sarcResumeResult.NativeSarcophagus.OpenedRetained -ne $opened.Count -or
            $sarcResumeResult.NativeSarcophagus.ExactLootRetained -ne $loot.Count -or
            $sarcResumeResult.NativeSarcophagus.ReleasedSkeletonCorpsesRetained -ne $released.Count -or
            $sarcResumeResult.NativeSarcophagus.RepeatClickDidNotReopen -isnot [bool] -or
            -not $sarcResumeResult.NativeSarcophagus.RepeatClickDidNotReopen -or
            $sarcResumeResult.NativeSarcophagus.RepeatStatus -notin @('NotQueued', 'Accepted', 'Duplicate', 'Rejected') -or
            $sarcResumeResult.NativeSarcophagus.RepeatRejectReason -notin @('Unspecified', 'NotAllowed', 'InvalidTarget')) {
            throw 'Native sarcophagus acceptance lacks exact old-save admission, visible outcome, fresh resume or no-duplicate evidence.'
        }
        foreach ($field in @('GeometrySha256', 'WorldItemsSha256')) {
            Assert-NativeEvidenceHash $sarcJourneyResult.NativeSarcophagus.$field "sarcophagus journey $field"
            Assert-NativeEvidenceHash $sarcResumeResult.NativeSarcophagus.$field "sarcophagus resume $field"
            if ($sarcJourneyResult.NativeSarcophagus.$field -cne $sarcResumeResult.NativeSarcophagus.$field) {
                throw "Native sarcophagus resume changed retained $field."
            }
        }
        foreach ($operation in $opened) {
            Assert-NativeEvidenceInteger $operation.EntityId 'native sarcophagus identity' 1 ([uint32]::MaxValue)
            if ($operation.TerminalFrame -le $operation.InitialFrame -or
                [uint64]$operation.Sequence -le [uint64]$prior.Sequence) {
                throw 'An ordinary native sarcophagus did not open and advance its animation.'
            }
        }
        Write-Host "PASS: Native sarcophagus interaction, Town checkpoint, fresh-host object/loot/actor retention and no duplicate repeat outcome. Evidence: $sessionDirectory"
        return
    }

    if ($CathedralNativeChest) {
        $parameters.QuitAfterSeconds = 900
        & $launcher @parameters -AcceptancePhase CathedralNativeChestJourney
        $createdProcess = Read-ModeProcessEvidence 'CathedralNativeChestJourney'
        $parameters.QuitAfterSeconds = 600
        & $launcher @parameters -AcceptancePhase CathedralNativeChestResume
        $resumedProcess = Read-ModeProcessEvidence 'CathedralNativeChestResume'
        Assert-FreshProcessEvidence $createdProcess $resumedProcess 1
        foreach ($field in @('ContentHash', 'RulesetHash', 'ProfileHash', 'ContentTreeHash',
            'PopulationTreeHash', 'RuntimeTreeHash', 'ProfileKind', 'AdapterId')) {
            if ($createdProcess.$field -cne $resumedProcess.$field) {
                throw "Native chest resume changed its pinned $field identity."
            }
        }
        $profileMetadataPath = Join-Path $CathedralNativeProfileDirectory 'cathedral-native-population-playtest.json'
        $profileMetadataHash = (Get-FileHash -LiteralPath $profileMetadataPath -Algorithm SHA256).Hash.ToUpperInvariant()
        $profileMetadata = Get-Content -LiteralPath $profileMetadataPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        if ($profileMetadataHash -cne 'A992857810C51CE2A802588D8F3386D94D4C6AA44127394553D1E797B9F82FC1' -or
            $createdProcess.ProfileKind -cne 'cathedral-native-population-playtest' -or
            $createdProcess.AdapterId -cne 'devilution.diablo.cathedral-native-population.v1' -or
            $createdProcess.ProfileHash -cne $profileMetadataHash -or
            $profileMetadata.nativePopulation -isnot [bool] -or -not $profileMetadata.nativePopulation -or
            $profileMetadata.nativePopulationClaim -isnot [bool] -or $profileMetadata.nativePopulationClaim -or
            $profileMetadata.populationPolicy -cne 'original-diablo-single-player-normal-rogue-l1-main' -or
            $profileMetadata.contentManifestHash -cne $createdProcess.ContentHash -or
            $profileMetadata.packFileHash -cne $createdProcess.ContentTreeHash -or
            $profileMetadata.populationPackFileHash -cne $createdProcess.PopulationTreeHash -or
            $profileMetadata.runtimePackFileHash -cne $createdProcess.RuntimeTreeHash -or
            $profileMetadata.populationMetadataFingerprint -notmatch '^[0-9A-Fa-f]{64}$') {
            throw 'Native chest process identity does not match the pinned unclaimed population profile.'
        }
        $journeyPath = Join-Path $sessionDirectory 'cathedralnativechestjourney.json'
        $resumePath = Join-Path $sessionDirectory 'cathedralnativechestresume.json'
        $priorResumePath = Join-Path $sessionDirectory 'cathedralnativeresume.json'
        foreach ($path in @($journeyPath, $resumePath, $priorResumePath)) {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "Native chest acceptance did not retain required checkpoint evidence: $path"
            }
        }
        $journeyEvidence = Get-Content -LiteralPath $journeyPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        $resumeEvidence = Get-Content -LiteralPath $resumePath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        $priorResume = Get-Content -LiteralPath $priorResumePath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        $journeyChest = $journeyEvidence.NativeChest
        $resumeChest = $resumeEvidence.NativeChest
        if ($journeyEvidence.Passed -isnot [bool] -or -not $journeyEvidence.Passed -or
            $resumeEvidence.Passed -isnot [bool] -or -not $resumeEvidence.Passed -or
            $resumeChest.Passed -isnot [bool] -or -not $resumeChest.Passed -or
            $journeyEvidence.Mode -cne 'SinglePlayer' -or $resumeEvidence.Mode -cne 'SinglePlayer' -or
            $journeyEvidence.Visible -isnot [bool] -or $journeyEvidence.Visible -or
            $resumeEvidence.Visible -isnot [bool] -or $resumeEvidence.Visible -or
            $journeyEvidence.ContentHash -cne $createdProcess.ContentHash -or
            $resumeEvidence.ContentHash -cne $createdProcess.ContentHash -or
            $journeyEvidence.RulesetHash -cne $createdProcess.RulesetHash -or
            $resumeEvidence.RulesetHash -cne $createdProcess.RulesetHash -or
            $journeyEvidence.LevelId -ne 0 -or $resumeEvidence.LevelId -ne 0 -or
            $journeyEvidence.JourneyReturnedToTown -isnot [bool] -or -not $journeyEvidence.JourneyReturnedToTown) {
            throw 'Native chest phase evidence did not pass at the Town save boundary under the pinned headless host.'
        }
        foreach ($hashField in @('ContentHash', 'RulesetHash')) {
            Assert-NativeEvidenceHash $journeyEvidence.$hashField "chest journey $hashField"
            Assert-NativeEvidenceHash $resumeEvidence.$hashField "chest resume $hashField"
        }
        foreach ($document in @($journeyEvidence, $resumeEvidence)) {
            Assert-NativeEvidenceInteger $document.EntityId 'chest player entity ID' 1 ([long]::MaxValue)
            Assert-NativeEvidenceInteger $document.ClassId 'chest player class ID' 1 ([long]::MaxValue)
            Assert-NativeEvidenceInteger $document.X 'chest player X coordinate' 0 111
            Assert-NativeEvidenceInteger $document.Y 'chest player Y coordinate' 0 111
            Assert-NativeEvidenceInteger $document.Sequence 'chest phase sequence' 1 ([long]::MaxValue)
        }
        $expectedPriorSequence = [uint64]$priorResume.NativePopulation.FinalSequence
        if ($priorResume.Passed -isnot [bool] -or -not $priorResume.Passed -or
            $journeyChest.AdapterId -cne $createdProcess.AdapterId -or
            $journeyChest.PopulationPolicy -cne $profileMetadata.populationPolicy -or
            $journeyChest.PopulationMetadataFingerprint -cne $profileMetadata.populationMetadataFingerprint -or
            $journeyChest.ContentHash -cne $createdProcess.ContentHash -or
            $journeyChest.RulesetHash -cne $createdProcess.RulesetHash -or
            [uint64]$journeyChest.PriorTownSequence -ne $expectedPriorSequence -or
            [uint64]$journeyChest.Sequence -ne [uint64]$journeyEvidence.Sequence -or
            [uint64]$journeyChest.Sequence -le $expectedPriorSequence -or
            $journeyChest.LevelId -ne 0 -or $journeyChest.EntityId -ne $journeyEvidence.EntityId -or
            $journeyChest.ClassId -ne 1 -or $journeyChest.X -ne $journeyEvidence.X -or $journeyChest.Y -ne $journeyEvidence.Y -or
            $journeyChest.EquipmentSeed -ne $priorResume.NativePopulation.RepairItemSeed -or
            $journeyChest.EquipmentStableItemId -ne $priorResume.NativePopulation.StableItemId -or
            $journeyChest.EquipmentStateSha256 -cne $priorResume.NativePopulation.FinalItemStateSha256 -or
            $journeyChest.Gold -ne $priorResume.NativePopulation.Gold -or
            $journeyChest.StashGold -ne $priorResume.NativePopulation.StashGold) {
            throw 'The native chest journey did not start from the passed final R3 Town checkpoint.'
        }
        Assert-NativeEvidenceInteger $journeyChest.PriorTownSequence 'prior Town sequence' 1 ([long]::MaxValue)
        Assert-NativeEvidenceInteger $journeyChest.Sequence 'chest Town sequence' 1 ([long]::MaxValue)
        Assert-NativeEvidenceHash $journeyChest.L1GeometrySha256 'chest L1 geometry hash'
        Assert-NativeEvidenceHash $journeyChest.L1WorldItemsSha256 'chest L1 item projection hash'
        $openedChests = @($journeyChest.OpenedChests)
        $chestLoot = @($openedChests | ForEach-Object { @($_.Loot) })
        $lootEntityIds = @($chestLoot | ForEach-Object { $_.EntityId })
        if ($openedChests.Count -lt 1 -or $chestLoot.Count -lt 1 -or
            $lootEntityIds.Count -ne (@($lootEntityIds | Select-Object -Unique).Count)) {
            throw 'Native chest acceptance did not prove a visible generated item with unique world identity.'
        }
        foreach ($chest in $openedChests) {
            Assert-NativeEvidenceInteger $chest.EntityId 'native chest entity ID' 1 ([long]::MaxValue)
            Assert-NativeEvidenceInteger $chest.NativeObjectId 'native chest object type' 5 7
            Assert-NativeEvidenceInteger $chest.Sequence 'chest-open sequence' ($expectedPriorSequence + 1) ([long]::MaxValue)
            if ($chest.AnimationFrameAfter -ne ($chest.AnimationFrameBefore + 2)) {
                throw 'A native chest did not preserve the ordinary one-way animation transition.'
            }
            foreach ($loot in @($chest.Loot)) {
                Assert-NativeEvidenceInteger $loot.EntityId 'generated item entity ID' 1 ([long]::MaxValue)
                Assert-NativeEvidenceInteger $loot.ItemSeed 'generated item seed' 0 ([long]::MaxValue)
                Assert-NativeEvidenceInteger $loot.StableItemId 'generated stable item ID' 1 ([long]::MaxValue)
                Assert-NativeEvidenceInteger $loot.NativeItemIndex 'generated native item index' 0 ([long]::MaxValue)
                Assert-NativeEvidenceHash $loot.ItemStateSha256 'generated item state hash'
                Assert-NativeEvidenceHash $loot.WorldItemSha256 'generated world item hash'
            }
        }
        if ($resumeChest.ResumedSessionMatched -isnot [bool] -or -not $resumeChest.ResumedSessionMatched -or
            [uint64]$resumeChest.SavedSequence -ne [uint64]$journeyEvidence.Sequence -or
            $resumeChest.OpenedChestsRetained -ne $openedChests.Count -or
            $resumeChest.VisibleLootRetainedExactlyOnce -ne $chestLoot.Count -or
            $resumeChest.L1RetentionVerified -isnot [bool] -or -not $resumeChest.L1RetentionVerified -or
            $resumeChest.L1GeometrySha256 -cne $journeyChest.L1GeometrySha256 -or
            $resumeChest.L1WorldItemsSha256 -cne $journeyChest.L1WorldItemsSha256 -or
            $resumeChest.RepeatClickDidNotReopen -isnot [bool] -or -not $resumeChest.RepeatClickDidNotReopen -or
            $resumeChest.RepeatRandomStateCheckPassed -isnot [bool] -or
            -not $resumeChest.RepeatRandomStateCheckPassed -or
            $resumeChest.RepeatWorldItemsSha256 -cne $journeyChest.L1WorldItemsSha256 -or
            $resumeChest.RepeatDisposition -notin @('QueuedOrdinaryClick', 'ClientDidNotQueueOpenedChest') -or
            $resumeChest.RepeatClickStatus -notin @('NotQueued', 'Accepted', 'Duplicate', 'Rejected') -or
            $resumeChest.RepeatClickRejectReason -notin @('Unspecified', 'NotAllowed', 'InvalidTarget') -or
            $resumeChest.TownReturned -isnot [bool] -or -not $resumeChest.TownReturned -or
            [uint64]$resumeChest.FinalSequence -ne [uint64]$resumeEvidence.Sequence -or
            $resumeEvidence.EntityId -ne $journeyEvidence.EntityId -or
            $resumeEvidence.ClassId -ne $journeyEvidence.ClassId -or
            [uint64]$resumeEvidence.Sequence -le [uint64]$resumeChest.SavedSequence -or
            $resumeChest.FinalGold -ne $journeyChest.Gold -or
            $resumeChest.FinalStashGold -ne $journeyChest.StashGold -or
            $resumeChest.FinalLife -ne $journeyChest.Life -or
            $resumeChest.FinalMana -ne $journeyChest.Mana -or
            $resumeChest.FinalExperience -ne $journeyChest.Experience -or
            $resumeChest.FinalInventorySha256 -cne $journeyChest.InventorySha256 -or
            $resumeChest.FinalBeltSha256 -cne $journeyChest.BeltSha256 -or
            $resumeChest.FinalEquipmentSha256 -cne $journeyChest.EquipmentSha256 -or
            $resumeChest.FinalEquipmentStateSha256 -cne $journeyChest.EquipmentStateSha256 -or
            $resumeChest.FinalL1WorldItemsSha256 -cne $journeyChest.L1WorldItemsSha256) {
            throw 'The chest fresh-host resume did not retain the Town save, L1 object/loot state, or no-duplicate repeat result.'
        }
        foreach ($hashField in @('L1GeometrySha256', 'L1WorldItemsSha256', 'RepeatWorldItemsSha256',
            'FinalInventorySha256', 'FinalBeltSha256', 'FinalEquipmentSha256',
            'FinalEquipmentStateSha256', 'FinalL1WorldItemsSha256')) {
            Assert-NativeEvidenceHash $resumeChest.$hashField "chest resume $hashField"
        }
        Write-Host "PASS: Native Cathedral ordinary chest loot, Town checkpoint, fresh-host L1 retention, and no duplicate loot from repeat clicks. Evidence: $sessionDirectory"
        return
    }
    if ($CathedralNativeProfileDirectory) {
        $parameters.QuitAfterSeconds = 900
        & $launcher @parameters -AcceptancePhase CathedralNativeJourney
        $createdProcess = Read-ModeProcessEvidence 'CathedralNativeJourney'
        $parameters.QuitAfterSeconds = 600
        & $launcher @parameters -AcceptancePhase CathedralNativeResume
        $resumedProcess = Read-ModeProcessEvidence 'CathedralNativeResume'
        Assert-FreshProcessEvidence $createdProcess $resumedProcess 1
        foreach ($field in @('ContentHash', 'RulesetHash', 'ProfileHash', 'ContentTreeHash',
            'PopulationTreeHash', 'RuntimeTreeHash', 'ProfileKind', 'AdapterId')) {
            if ($createdProcess.$field -cne $resumedProcess.$field) {
                throw "Native Cathedral resume changed its pinned $field identity."
            }
        }
        if ($createdProcess.ProfileKind -cne 'cathedral-native-population-playtest' -or
            $createdProcess.AdapterId -cne 'devilution.diablo.cathedral-native-population.v1') {
            throw 'Native Cathedral process evidence does not identify the pinned population profile.'
        }
        $profileMetadataPath = Join-Path $CathedralNativeProfileDirectory 'cathedral-native-population-playtest.json'
        $profileMetadataHash = (Get-FileHash -LiteralPath $profileMetadataPath -Algorithm SHA256).Hash.ToUpperInvariant()
        $profileMetadata = Get-Content -LiteralPath $profileMetadataPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        if ($profileMetadataHash -cne 'A992857810C51CE2A802588D8F3386D94D4C6AA44127394553D1E797B9F82FC1' -or
            $profileMetadata.adapterId -cne $createdProcess.AdapterId -or
            $profileMetadata.nativePopulation -isnot [bool] -or -not $profileMetadata.nativePopulation -or
            $profileMetadata.nativePopulationClaim -isnot [bool] -or $profileMetadata.nativePopulationClaim -or
            $profileMetadata.populationPolicy -cne 'original-diablo-single-player-normal-rogue-l1-main' -or
            $createdProcess.ProfileHash -cne $profileMetadataHash -or
            $profileMetadata.contentManifestHash -cne $createdProcess.ContentHash -or
            $profileMetadata.packFileHash -cne $createdProcess.ContentTreeHash -or
            $profileMetadata.populationPackFileHash -cne $createdProcess.PopulationTreeHash -or
            $profileMetadata.runtimePackFileHash -cne $createdProcess.RuntimeTreeHash -or
            $profileMetadata.populationMetadataFingerprint -notmatch '^[0-9A-Fa-f]{64}$') {
            throw 'Native Cathedral process hashes do not bind to the reviewed profile metadata and content packs.'
        }
        $journeyPath = Join-Path $sessionDirectory 'cathedralnativejourney.json'
        $resumePath = Join-Path $sessionDirectory 'cathedralnativeresume.json'
        if (-not (Test-Path -LiteralPath $journeyPath -PathType Leaf) -or
            -not (Test-Path -LiteralPath $resumePath -PathType Leaf)) {
            throw 'Native Cathedral acceptance did not retain separate journey and resume evidence.'
        }
        $journeyEvidence = Get-Content -LiteralPath $journeyPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        $resumeEvidence = Get-Content -LiteralPath $resumePath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        if ($journeyEvidence.Passed -isnot [bool] -or -not $journeyEvidence.Passed -or
            $resumeEvidence.Passed -isnot [bool] -or -not $resumeEvidence.Passed -or
            $resumeEvidence.NativePopulation.Passed -isnot [bool] -or -not $resumeEvidence.NativePopulation.Passed) {
            throw 'Native Cathedral journey or cold-resume acceptance evidence did not pass.'
        }
        $journeyPopulation = $journeyEvidence.NativePopulation
        $resumePopulation = $resumeEvidence.NativePopulation
        foreach ($document in @($journeyEvidence, $resumeEvidence)) {
            if ($document.Mode -cne 'SinglePlayer' -or $document.Visible -isnot [bool] -or $document.Visible -or
                $document.ContentHash -cne $createdProcess.ContentHash -or
                $document.RulesetHash -cne $createdProcess.RulesetHash) {
                throw 'Native Cathedral phase evidence does not match the headless single-player host identity.'
            }
            Assert-NativeEvidenceInteger $document.EntityId 'player entity ID' 1 ([uint32]::MaxValue)
            Assert-NativeEvidenceInteger $document.ClassId 'player class ID' 1 ([uint32]::MaxValue)
            Assert-NativeEvidenceInteger $document.LevelId 'player level ID' 0 4
            Assert-NativeEvidenceInteger $document.X 'player X coordinate' 0 111
            Assert-NativeEvidenceInteger $document.Y 'player Y coordinate' 0 111
            Assert-NativeEvidenceInteger $document.Sequence 'phase sequence' 1 ([long]::MaxValue)
        }
        foreach ($hashField in @('ContentHash', 'RulesetHash')) {
            Assert-NativeEvidenceHash $journeyEvidence.$hashField "journey $hashField"
            Assert-NativeEvidenceHash $resumeEvidence.$hashField "resume $hashField"
        }
        if ($journeyEvidence.Mode -cne 'SinglePlayer' -or $journeyEvidence.LevelId -ne 0 -or
            $journeyEvidence.JourneyReturnedToTown -isnot [bool] -or -not $journeyEvidence.JourneyReturnedToTown -or
            $journeyEvidence.ClassId -ne 1 -or
            $journeyPopulation.AdapterId -cne $profileMetadata.adapterId -or
            $journeyPopulation.PopulationPolicy -cne $profileMetadata.populationPolicy -or
            $journeyPopulation.PopulationMetadataFingerprint -cne $profileMetadata.populationMetadataFingerprint -or
            $journeyPopulation.ContentHash -cne $createdProcess.ContentHash -or
            $journeyPopulation.RulesetHash -cne $createdProcess.RulesetHash -or
            $journeyPopulation.EntityId -ne $journeyEvidence.EntityId -or
            $journeyPopulation.ClassId -ne $journeyEvidence.ClassId -or
            $journeyPopulation.LevelId -ne $journeyEvidence.LevelId -or
            $journeyPopulation.X -ne $journeyEvidence.X -or $journeyPopulation.Y -ne $journeyEvidence.Y -or
            $journeyPopulation.Sequence -ne $journeyEvidence.Sequence) {
            throw 'The saved native journey checkpoint does not match its phase or pinned Rogue profile.'
        }
        foreach ($field in @('Life', 'LifeMaximum', 'Mana', 'ManaMaximum', 'Experience', 'Gold', 'StashGold',
            'MonsterEntityId', 'MonsterId', 'DropEntityId', 'ItemSeed', 'StableItemId', 'NativeItemIndex', 'EquipmentSlot')) {
            Assert-NativeEvidenceInteger $journeyPopulation.$field "journey $field" 0 ([long]::MaxValue)
        }
        if ($journeyPopulation.Life -lt 1 -or $journeyPopulation.Life -gt $journeyPopulation.LifeMaximum -or
            $journeyPopulation.Mana -lt 0 -or $journeyPopulation.Mana -gt $journeyPopulation.ManaMaximum -or
            $journeyPopulation.MonsterEntityId -eq 0 -or $journeyPopulation.MonsterId -eq 0 -or
            $journeyPopulation.DropEntityId -eq 0 -or $journeyPopulation.ItemSeed -eq 0 -or
            $journeyPopulation.StableItemId -eq 0 -or [string]::IsNullOrWhiteSpace([string]$journeyPopulation.ItemName)) {
            throw 'The saved native journey does not identify a live Rogue, defeated actor and earned item.'
        }
        foreach ($field in @('ItemStateSha256', 'InventorySha256', 'BeltSha256', 'EquipmentSha256', 'GeometrySha256')) {
            Assert-NativeEvidenceHash $journeyPopulation.$field "journey $field"
        }

        $savedSequence = $journeyPopulation.Sequence
        $restored = $resumePopulation.RestoredBaseline
        if ($resumeEvidence.LevelId -ne 0 -or
            $resumePopulation.L1RetentionVerified -isnot [bool] -or -not $resumePopulation.L1RetentionVerified -or
            $resumePopulation.L1RevisitLevelId -ne 1 -or
            $resumePopulation.L1GeometrySha256 -cne $journeyPopulation.GeometrySha256 -or
            $resumeEvidence.EntityId -ne $journeyEvidence.EntityId -or
            $resumeEvidence.ClassId -ne $journeyEvidence.ClassId -or
            $resumePopulation.AdapterId -cne $profileMetadata.adapterId -or
            $resumePopulation.PopulationPolicy -cne $profileMetadata.populationPolicy -or
            $resumePopulation.PopulationMetadataFingerprint -cne $profileMetadata.populationMetadataFingerprint -or
            $resumePopulation.ProfileContentManifestHash -cne $createdProcess.ContentHash -or
            $resumePopulation.SavedSequence -ne $savedSequence -or
            $resumePopulation.SavedObservedSequence -ne $savedSequence -or
            $null -eq $restored) {
            throw 'The fresh-host resume does not identify the saved native journey baseline.'
        }
        $baselinePairs = @(
            [pscustomobject]@{ Restored='EntityId'; Saved='EntityId' },
            [pscustomobject]@{ Restored='ClassId'; Saved='ClassId' },
            [pscustomobject]@{ Restored='CharacterLevel'; Saved='CharacterLevel' },
            [pscustomobject]@{ Restored='LevelId'; Saved='LevelId' },
            [pscustomobject]@{ Restored='X'; Saved='X' }, [pscustomobject]@{ Restored='Y'; Saved='Y' },
            [pscustomobject]@{ Restored='Life'; Saved='Life' }, [pscustomobject]@{ Restored='LifeMaximum'; Saved='LifeMaximum' },
            [pscustomobject]@{ Restored='Mana'; Saved='Mana' }, [pscustomobject]@{ Restored='ManaMaximum'; Saved='ManaMaximum' },
            [pscustomobject]@{ Restored='Experience'; Saved='Experience' }, [pscustomobject]@{ Restored='Gold'; Saved='Gold' },
            [pscustomobject]@{ Restored='StashGold'; Saved='StashGold' },
            [pscustomobject]@{ Restored='InventorySha256'; Saved='InventorySha256' },
            [pscustomobject]@{ Restored='BeltSha256'; Saved='BeltSha256' },
            [pscustomobject]@{ Restored='EquipmentSha256'; Saved='EquipmentSha256' },
            [pscustomobject]@{ Restored='GeometrySha256'; Saved='GeometrySha256' })
        foreach ($pair in $baselinePairs) {
            if ($restored.($pair.Restored) -cne $journeyPopulation.($pair.Saved)) {
                throw "Fresh-host native resume changed the saved $($pair.Saved) baseline."
            }
        }
        $firstSequence = $resumePopulation.FirstAcceptedReturnCommandSequence
        Assert-NativeEvidenceInteger $firstSequence 'first resumed command sequence' 1 ([long]::MaxValue)
        Assert-NativeEvidenceInteger $resumePopulation.FirstAcceptedReturnCommandAppliedTick 'first resumed command applied tick' 1 ([long]::MaxValue)
        Assert-NativeEvidenceInteger $resumePopulation.FirstAcceptedReturnCommandX 'first resumed command X' 0 111
        Assert-NativeEvidenceInteger $resumePopulation.FirstAcceptedReturnCommandY 'first resumed command Y' 0 111
        if ([decimal]$firstSequence -ne ([decimal]$savedSequence + 1) -or
            $resumePopulation.FirstAcceptedReturnCommandStatus -cne 'Accepted' -or
            $resumePopulation.FirstAcceptedReturnCommandRejectReason -cne 'Unspecified' -or
            [Math]::Max([Math]::Abs([long]$resumePopulation.FirstAcceptedReturnCommandX - [long]$restored.X),
                [Math]::Abs([long]$resumePopulation.FirstAcceptedReturnCommandY - [long]$restored.Y)) -ne 1) {
            throw 'The first fresh-host Town route step is not the exact accepted N+1 one-cell command.'
        }
        Assert-NativeEvidenceInteger $resumePopulation.L1RevisitLevelId 'resumed L1 revisit level' 1 1
        Assert-NativeEvidenceHash $resumePopulation.L1GeometrySha256 'resumed L1 geometry'
        Assert-NativeEvidenceInteger $resumePopulation.MonsterEntityId 'resumed monster entity ID' 1 ([long]::MaxValue)
        Assert-NativeEvidenceInteger $resumePopulation.MonsterId 'resumed monster ID' 1 ([long]::MaxValue)
        Assert-NativeEvidenceInteger $resumePopulation.ClaimedDropEntityId 'claimed drop entity ID' 1 ([long]::MaxValue)
        Assert-NativeEvidenceInteger $resumePopulation.ClaimedDropSeed 'claimed drop seed' 1 ([long]::MaxValue)
        if ($resumePopulation.MonsterEntityId -ne $journeyPopulation.MonsterEntityId -or
            $resumePopulation.MonsterId -ne $journeyPopulation.MonsterId -or
            $resumePopulation.ClaimedDropEntityId -ne $journeyPopulation.DropEntityId -or
            $resumePopulation.ClaimedDropSeed -ne $journeyPopulation.ItemSeed -or
            $resumePopulation.StableItemId -ne $journeyPopulation.StableItemId -or
            $resumePopulation.NativeItemIndex -ne $journeyPopulation.NativeItemIndex -or
            $resumePopulation.ItemName -cne $journeyPopulation.ItemName -or
            $resumePopulation.EquipmentSlot -ne $journeyPopulation.EquipmentSlot -or
            $resumePopulation.ItemStateSha256 -cne $journeyPopulation.ItemStateSha256 -or
            $resumePopulation.CorpseRetained -isnot [bool] -or -not $resumePopulation.CorpseRetained -or
            $resumePopulation.ClaimedLootAbsent -isnot [bool] -or -not $resumePopulation.ClaimedLootAbsent -or
            $resumePopulation.TownReturned -isnot [bool] -or -not $resumePopulation.TownReturned -or
            $resumePopulation.StashRoundTripComplete -isnot [bool] -or -not $resumePopulation.StashRoundTripComplete) {
            throw 'Fresh-host native resume changed the retained monster, earned loot, equipment or Town/stash result.'
        }
        Assert-NativeEvidenceHash $resumePopulation.StashItemStateSha256 'stashed item state'
        Assert-NativeEvidenceHash $resumePopulation.FinalItemStateSha256 'final item state'
        Assert-NativeEvidenceHash $resumePopulation.RepairItemStateBeforeSha256 'Smith item state before service'
        Assert-NativeEvidenceHash $resumePopulation.RepairItemStateAfterSha256 'Smith item state after service'
        foreach ($field in @('RepairItemSeed', 'RepairQuotePrice', 'RepairGoldBefore', 'RepairGoldAfter',
            'RepairDurabilityBefore', 'RepairDurabilityAfter', 'RepairMaximumDurability', 'Gold', 'StashGold',
            'SmithStoreId', 'FinalSequence', 'StashCell')) {
            Assert-NativeEvidenceInteger $resumePopulation.$field "resume $field" 0 ([long]::MaxValue)
        }
        if ($resumePopulation.RepairItemSeed -ne $journeyPopulation.ItemSeed -or
            $resumePopulation.StashItemStateSha256 -cne $journeyPopulation.ItemStateSha256 -or
            $resumePopulation.RepairItemStateBeforeSha256 -cne $resumePopulation.StashItemStateSha256 -or
            $resumePopulation.RepairItemStateAfterSha256 -cne $resumePopulation.FinalItemStateSha256 -or
            $resumePopulation.RepairGoldBefore -ne $restored.Gold -or
            $resumePopulation.Gold -ne $resumePopulation.RepairGoldAfter -or
            $resumePopulation.StashGold -ne $journeyPopulation.StashGold -or
            $resumePopulation.FinalSequence -ne $resumeEvidence.Sequence -or
            $resumePopulation.FinalSequence -lt $firstSequence -or $resumePopulation.SmithStoreId -eq 0 -or
            $resumePopulation.RepairGoldBefore -lt $resumePopulation.RepairGoldAfter -or
            $resumePopulation.RepairDurabilityBefore -gt $resumePopulation.RepairMaximumDurability -or
            $resumePopulation.RepairDurabilityAfter -gt $resumePopulation.RepairMaximumDurability) {
            throw 'Native resume changed its stashed item, seed, Town currency or final sequence.'
        }
        if ($resumePopulation.RepairCommandAccepted -isnot [bool] -or $resumePopulation.RepairButtonDisabled -isnot [bool]) {
            throw 'Native Smith evidence omitted the service acceptance/disabled state.'
        }
        if ($resumePopulation.RepairCommandAccepted) {
            if ($resumePopulation.RepairButtonDisabled -or $resumePopulation.RepairQuoteReason -cne 'Unspecified' -or
                $resumePopulation.RepairQuotePrice -eq 0 -or
                ([decimal]$resumePopulation.RepairGoldBefore - [decimal]$resumePopulation.RepairGoldAfter) -ne $resumePopulation.RepairQuotePrice -or
                $resumePopulation.RepairDurabilityBefore -ge $resumePopulation.RepairMaximumDurability -or
                $resumePopulation.RepairDurabilityAfter -ne $resumePopulation.RepairMaximumDurability) {
                throw 'Accepted native Smith service did not charge the quoted amount and repair the earned item.'
            }
        } elseif ($resumePopulation.RepairButtonDisabled -ne $true -or
            $resumePopulation.RepairQuoteReason -cne 'NotAllowed' -or $resumePopulation.RepairQuotePrice -ne 0 -or
            $resumePopulation.RepairGoldBefore -ne $resumePopulation.RepairGoldAfter -or
            $resumePopulation.RepairDurabilityBefore -ne $resumePopulation.RepairDurabilityAfter -or
            $resumePopulation.RepairItemStateBeforeSha256 -cne $resumePopulation.RepairItemStateAfterSha256) {
            throw 'Refused native Smith service changed the quote, currency or earned item.'
        }

        $cohortPath = Join-Path (Join-Path $sessionDirectory 'save') 'authoritative-cohort.json'
        if (-not (Test-Path -LiteralPath $cohortPath -PathType Leaf)) {
            throw 'Native resume did not retain the final complete-cohort checkpoint.'
        }
        $cohort = Get-Content -LiteralPath $cohortPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        if ($cohort.FormatVersion -ne 1 -or $cohort.Identity.GameMode -ne 0 -or
            $cohort.Identity.ContentManifestHash -cne $resumedProcess.ContentHash -or
            $cohort.Identity.RulesetIdentityHash -cne $resumedProcess.RulesetHash -or
            @($cohort.Participants).Count -ne 1) {
            throw 'The final native cohort checkpoint does not match the resumed single-player host.'
        }
        $participant = @($cohort.Participants | Where-Object { $_.EntityId -eq $journeyPopulation.EntityId })
        if ($participant.Count -ne 1 -or $null -eq $participant[0].CommandReceipts) {
            throw 'The final native cohort checkpoint has no unique saved Rogue receipt ledger.'
        }
        $returnReceipts = @($participant[0].CommandReceipts | Where-Object {
            $_.ClientSequence -eq $firstSequence -and
            $_.AppliedTick -eq $resumePopulation.FirstAcceptedReturnCommandAppliedTick
        })
        if ($returnReceipts.Count -ne 1 -or $returnReceipts[0].ServerReceiptSequence -lt 1 -or
            $returnReceipts[0].Status -ne 1 -or $returnReceipts[0].RejectReason -ne 0) {
            throw 'The persisted cohort ledger lacks the exact accepted, unspecified N+1 Town route-step receipt.'
        }
        Write-Host "PASS: actual headless native Cathedral Town checkpoint, fresh-host cohort resume and retained L1 revisit. Evidence: $sessionDirectory"
        return
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
