[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ProfileDirectory,
    [string]$GodotExecutable,
    [switch]$CommandTimingTrace,
    [ValidateRange(1, 3600)] [int]$QuitAfterSeconds = 3600,
    [ValidateSet('Debug', 'Release')] [string]$ServerBuildConfiguration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$profileRoot = (Resolve-Path -LiteralPath $ProfileDirectory).ProviderPath
$sessionDirectory = Join-Path $profileRoot ('sessions/progression-' + [guid]::NewGuid().ToString('N'))
$launcher = Join-Path $repositoryRoot 'godot/run-local-client.ps1'
$arguments = @{
    CathedralNativeProgressionProfileDirectory = $profileRoot
    PersistenceMode = 'cohort'; Headless = $true; Automated = $true
    SessionDirectory = $sessionDirectory; QuitAfterSeconds = $QuitAfterSeconds
    ServerBuildConfiguration = $ServerBuildConfiguration
}
if ($GodotExecutable) { $arguments.GodotExecutable = $GodotExecutable }
if ($CommandTimingTrace) { $arguments.CommandTimingTrace = $true }

Write-Output "Native progression Journey session: $sessionDirectory"
& $launcher @arguments -AcceptancePhase CathedralNativeProgressionJourney
if ($LASTEXITCODE -ne 0) { throw "Native progression Journey failed with exit $LASTEXITCODE." }
$journey = Get-Content -LiteralPath (Join-Path $sessionDirectory 'cathedralnativeprogressionjourney.json') -Raw | ConvertFrom-Json
if ($journey.Passed -ne $true -or $journey.LevelId -ne 0 -or $null -eq $journey.NativeProgression) {
    throw 'The native progression Journey did not retain a passed Town checkpoint.'
}
if ($null -eq $journey.GoldCollection -or $null -eq $journey.HealerPreparation -or
    $journey.HealerPreparation.PurchaseVerified -ne $true -or @($journey.HealerPreparation.Purchases).Count -lt 1 -or
    @($journey.GoldCollection.Pickups).Count -lt 1) {
    throw 'The native progression Journey did not verify an ordinary Pepin purchase with earned Gold evidence.'
}

$l1 = $journey.NativeProgression.L1
$l2 = $journey.NativeProgression.L2
if ($null -eq $l1 -or @($l1.Kills).Count -lt 1 -or $l1.StableItemId -le 0 -or
    $null -eq $l2 -or @($l2.Kills).Count -lt 1 -or $l2.ExperienceAfter -le $l2.ExperienceBefore -or
    $l2.WorkUsed -le 0 -or $l2.WorkUsed -gt 576 -or $null -eq $l2.EntryGroundItemIds -or
    $null -eq $l2.ObservedNewDropEntityIds -or
    @($l2.ObservedNewDropEntityIds | Where-Object { $l2.EntryGroundItemIds -contains $_ }).Count -gt 0) {
    throw 'The native progression Journey did not prove L1 equipment and bounded native L2 combat with XP.'
}
foreach ($kill in @($l2.Kills)) {
    $matchingCorpses = @($kill.EncounterCorpses | Where-Object { $_.EntityId -eq $kill.MonsterEntityId })
    if ($kill.MonsterEntityId -le 0 -or $kill.ExperienceAfter -le $kill.ExperienceBefore -or
        $matchingCorpses.Count -ne 1 -or @($kill.AcceptedShots).Count -lt 1) {
        throw 'A native L2 kill lacks its exact corpse, XP gain or accepted-shot evidence.'
    }
    foreach ($shot in @($kill.AcceptedShots)) {
        if ($shot.TargetEntityId -ne $kill.MonsterEntityId -or $shot.Sequence -le 0 -or
            $shot.AppliedTick -le 0) {
            throw 'A native L2 accepted shot does not bind to its defeated target and applied receipt.'
        }
    }
}

# Preserve the successful Journey in place so a Resume-only correction never replays combat.
$resumeDirectory = Join-Path $profileRoot ('sessions/progression-resume-' + [guid]::NewGuid().ToString('N'))
Copy-Item -LiteralPath $sessionDirectory -Destination $resumeDirectory -Recurse -ErrorAction Stop
$arguments.SessionDirectory = $resumeDirectory
Write-Output "Native progression Resume session: $resumeDirectory"
& $launcher @arguments -AcceptancePhase CathedralNativeProgressionResume
if ($LASTEXITCODE -ne 0) { throw "Native progression Resume failed with exit $LASTEXITCODE." }
$resume = Get-Content -LiteralPath (Join-Path $resumeDirectory 'cathedralnativeprogressionresume.json') -Raw | ConvertFrom-Json
if ($resume.Passed -ne $true -or $resume.LevelId -ne 0 -or $resume.ExactColdBaseline -ne $true -or
    $resume.BothFloorsRetained -ne $true -or $null -eq $resume.Healer) {
    throw 'The native progression Resume did not verify its cold baseline, both floors and Town service.'
}
$healerContinuation = $resume.HealerStockContinuation
if ($null -eq $healerContinuation -or $healerContinuation.PurchaseVerified -isnot [bool] -or
    $healerContinuation.RetainedStockMatched -ne $true -or
    $healerContinuation.Stock.Hash -cne $journey.NativeProgression.HealerStock.Hash -or
    ($healerContinuation.PurchaseVerified -eq $true -and @($healerContinuation.Purchases).Count -ne 1) -or
    ($healerContinuation.PurchaseVerified -eq $false -and @($healerContinuation.Purchases).Count -ne 0)) {
    throw 'The native progression Resume did not preserve pinned stock or report actual resumed purchasing honestly.'
}

# Reuse the existing owned-process validator without starting another application or rebuilding it.
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $repositoryRoot 'tools/test-godot-session.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'The shared process validator source did not parse.' }
foreach ($name in @('Read-ModeProcessEvidence', 'Assert-FreshProcessEvidence')) {
    $definitions = @($ast.FindAll({ param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name
    }, $true))
    if ($definitions.Count -ne 1) { throw "Expected one shared process validator: $name" }
    Invoke-Expression $definitions[0].Extent.Text
}
$PersistenceMode = 'cohort'
$journeyProcess = Read-ModeProcessEvidence 'CathedralNativeProgressionJourney'
$sessionDirectory = $resumeDirectory
$resumeProcess = Read-ModeProcessEvidence 'CathedralNativeProgressionResume'
Assert-FreshProcessEvidence $journeyProcess $resumeProcess 1
if ($journeyProcess.ServerBuildConfiguration -cne $ServerBuildConfiguration -or
    $resumeProcess.ServerBuildConfiguration -cne $ServerBuildConfiguration) {
    throw 'Native progression Journey and Resume did not use the selected server build configuration.'
}
foreach ($field in @('ContentHash', 'RulesetHash', 'ProfileHash', 'ContentTreeHash', 'PopulationTreeHash', 'RuntimeTreeHash')) {
    if ([string]$journeyProcess.$field -cnotmatch '^[0-9A-Fa-f]{64}$' -or
        [string]$resumeProcess.$field -cnotmatch '^[0-9A-Fa-f]{64}$' -or
        $journeyProcess.$field -cne $resumeProcess.$field) { throw "Native progression pair changed $field." }
}
foreach ($process in @($journeyProcess, $resumeProcess)) {
    if ($process.ProfileKind -cne 'cathedral-native-progression-playtest' -or
        $process.AdapterId -cne 'devilution.diablo.cathedral-native-progression.v1') {
        throw 'The native progression pair did not use its separate explicit adapter/profile.'
    }
}
foreach ($evidence in @($journeyProcess, $resumeProcess)) {
    $owned = @([pscustomobject]@{ ProcessId = $evidence.HostProcessId; StartedUtc = $evidence.HostStartedUtc }) + @($evidence.Clients)
    foreach ($identity in $owned) {
        try { $process = [Diagnostics.Process]::GetProcessById([int]$identity.ProcessId) }
        catch [ArgumentException] { continue }
        try {
            if ($process.StartTime.ToUniversalTime() -eq ([DateTimeOffset]$identity.StartedUtc).UtcDateTime) {
                throw "An owned native progression process is still running: $($identity.ProcessId)."
            }
        } finally { $process.Dispose() }
    }
}
Write-Output 'Native Town/L1 equipment and earned Gold, L2 first combat, fresh-host resume and retained revisit passed.'
