<# Batch 1.5b: two clients fight on native multiplayer L1, then both resume on a fresh host. -Windowed shows both clients and saves each one's capture of its peer. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ProfileDirectory,
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

function Read-NativeMultiplayerEvidence([string]$Phase) {
    @(foreach ($slot in 1, 2) {
        Get-Content -LiteralPath (Join-Path $sessionDirectory "player-$slot/$($Phase.ToLowerInvariant()).json") -Raw | ConvertFrom-Json
    })
}

Write-Output "Native multiplayer session: $sessionDirectory"
& $launcher @arguments -AcceptancePhase NativeMultiplayerJourney
if ($LASTEXITCODE -ne 0) { throw "Native multiplayer Journey failed with exit $LASTEXITCODE." }
$journey = Read-NativeMultiplayerEvidence 'NativeMultiplayerJourney'
# Warrior in slot 1 and Rogue in slot 2, each seeing the other and each earning experience on L1.
foreach ($slot in 0, 1) {
    $evidence = $journey[$slot]
    if ($evidence.Passed -ne $true -or $evidence.Slot -ne $slot + 1 -or $evidence.ClassId -ne $slot -or
        $evidence.PeerClassId -ne 1 - $slot -or $evidence.LevelId -ne 1 -or
        $evidence.PeerEntityId -ne $journey[1 - $slot].EntityId -or
        $evidence.Fight.ExperienceAfter -le $evidence.Fight.ExperienceBefore -or
        ($Windowed -and -not $evidence.PeerDraw.Capture)) {
        throw "Player $($slot + 1) lacks its shared L1 fight evidence."
    }
}

& $launcher @arguments -AcceptancePhase NativeMultiplayerResume
if ($LASTEXITCODE -ne 0) { throw "Native multiplayer Resume failed with exit $LASTEXITCODE." }
$resume = Read-NativeMultiplayerEvidence 'NativeMultiplayerResume'
foreach ($slot in 0, 1) {
    if ($resume[$slot].Passed -ne $true -or $resume[$slot].EntityId -ne $journey[$slot].EntityId -or
        $resume[$slot].LevelId -ne 1 -or $resume[$slot].Checkpoint.Experience -ne $journey[$slot].Checkpoint.Experience) {
        throw "Player $($slot + 1) did not resume its L1 checkpoint on the fresh host."
    }
}
Write-Output 'Native multiplayer Journey and fresh-host Resume passed: Warrior and Rogue fought on L1 and both resumed.'
