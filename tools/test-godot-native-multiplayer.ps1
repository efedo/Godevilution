<# Batch 1.5b: two clients fight on native multiplayer L1, then both resume on a fresh host. -Windowed shows both clients and saves each one's capture of its peer.
   Batch 2.2b: -TargetLevel 5 -TestParticipant <clvl 8+ Warrior> sends two such Warriors through the Town warp to meet on L5 instead (no fight until Catacombs AI). #>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ProfileDirectory,
    [ValidateSet(1, 5)] [int]$TargetLevel = 1,
    [string]$TestParticipant,
    [string]$GodotExecutable,
    [ValidateRange(1, 3600)] [int]$QuitAfterSeconds = 1800,
    [ValidateSet('Debug', 'Release')] [string]$ServerBuildConfiguration = 'Debug',
    [switch]$Windowed
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$profileRoot = (Resolve-Path -LiteralPath $ProfileDirectory).ProviderPath
if (-not (Test-Path -LiteralPath (Join-Path $profileRoot 'cathedral-native-all-class-town-playtest.json') -PathType Leaf)) {
    throw 'Native multiplayer needs the all-class native Town profile.'
}
$sessionDirectory = Join-Path $profileRoot ('sessions/native-multiplayer-' + [guid]::NewGuid().ToString('N'))
$launcher = Join-Path $repositoryRoot 'godot-client/run-local-client.ps1'
$arguments = @{
    CathedralNativeTownProfileDirectory = $profileRoot; Multiplayer = $true
    PersistenceMode = 'cohort'; Headless = -not $Windowed; Automated = $true
    SessionDirectory = $sessionDirectory; QuitAfterSeconds = $QuitAfterSeconds
    ServerBuildConfiguration = $ServerBuildConfiguration
}
if ($GodotExecutable) { $arguments.GodotExecutable = $GodotExecutable }
if (($TargetLevel -eq 5) -ne [bool]$TestParticipant -or ($TargetLevel -eq 5 -and $Windowed)) {
    throw '-TargetLevel 5 needs a -TestParticipant Warrior and runs headless until Catacombs drawing lands.'
}
if ($TestParticipant) { $arguments.TestParticipantPath = (Resolve-Path -LiteralPath $TestParticipant).ProviderPath }
$env:GODEVILUTION_NATIVE_MULTIPLAYER_TARGET = [string]$TargetLevel
# Slot 2 is a Rogue on L1; the class-bound test participant makes both Warriors on L5.
$peerClasses = if ($TargetLevel -eq 5) { @(0, 0) } else { @(0, 1) }

function Read-NativeMultiplayerEvidence([string]$Phase) {
    @(foreach ($slot in 1, 2) {
        Get-Content -LiteralPath (Join-Path $sessionDirectory "player-$slot/$($Phase.ToLowerInvariant()).json") -Raw | ConvertFrom-Json
    })
}

Write-Output "Native multiplayer session: $sessionDirectory"
& $launcher @arguments -AcceptancePhase NativeMultiplayerJourney
if ($LASTEXITCODE -ne 0) { throw "Native multiplayer Journey failed with exit $LASTEXITCODE." }
$journey = Read-NativeMultiplayerEvidence 'NativeMultiplayerJourney'
# Each participant sees the other and earns experience on the target level.
foreach ($slot in 0, 1) {
    $evidence = $journey[$slot]
    if ($evidence.Passed -ne $true -or $evidence.Slot -ne $slot + 1 -or $evidence.ClassId -ne $peerClasses[$slot] -or
        $evidence.PeerClassId -ne $peerClasses[1 - $slot] -or $evidence.LevelId -ne $TargetLevel -or
        ($TargetLevel -eq 5 -and $evidence.CharacterLevel -lt 8) -or
        $evidence.PeerEntityId -ne $journey[1 - $slot].EntityId -or
        ($TargetLevel -eq 1 -and $evidence.Fight.ExperienceAfter -le $evidence.Fight.ExperienceBefore) -or
        ($Windowed -and -not $evidence.PeerDraw.Capture)) {
        throw "Player $($slot + 1) lacks its shared L$TargetLevel fight evidence."
    }
}

& $launcher @arguments -AcceptancePhase NativeMultiplayerResume
if ($LASTEXITCODE -ne 0) { throw "Native multiplayer Resume failed with exit $LASTEXITCODE." }
$resume = Read-NativeMultiplayerEvidence 'NativeMultiplayerResume'
foreach ($slot in 0, 1) {
    if ($resume[$slot].Passed -ne $true -or $resume[$slot].EntityId -ne $journey[$slot].EntityId -or
        $resume[$slot].LevelId -ne $TargetLevel -or $resume[$slot].Checkpoint.Experience -ne $journey[$slot].Checkpoint.Experience) {
        throw "Player $($slot + 1) did not resume its L$TargetLevel checkpoint on the fresh host."
    }
}
$pair = if ($TargetLevel -eq 5) { 'two clvl 8+ Warriors took the Town warp to L5' } else { 'Warrior and Rogue fought on L1' }
Write-Output "Native multiplayer Journey and fresh-host Resume passed: $pair and both resumed."
