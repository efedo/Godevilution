[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ProfileDirectory,
    [string]$GodotExecutable,
    [ValidateRange(1, 3600)] [int]$QuitAfterSeconds = 3600,
    [ValidateSet('Debug', 'Release')] [string]$ServerBuildConfiguration = 'Debug',
    [switch]$CommandTimingTrace
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$profileRoot = (Resolve-Path -LiteralPath $ProfileDirectory).ProviderPath
$profileMetadataPath = Join-Path $profileRoot 'cathedral-native-depth-progression-playtest.json'
$depthProfileMetadata = Get-Content -LiteralPath $profileMetadataPath -Raw -ErrorAction Stop | ConvertFrom-Json
if ($depthProfileMetadata.formatVersion -ne 3 -or
    $depthProfileMetadata.adapterId -cne 'devilution.diablo.cathedral-native-depth-progression.v1' -or
    $depthProfileMetadata.populationPolicy -cne 'original-diablo-single-player-normal-rogue-town-l1-l4' -or
    $depthProfileMetadata.nativeDepth -ne 4 -or $depthProfileMetadata.nativePopulation -ne $true -or $depthProfileMetadata.nativePopulationClaim -ne $false -or
    [string]::Join(',', [string[]]@($depthProfileMetadata.depthSeeds)) -cne '2588,2588,1,4294967295' -or
    @($depthProfileMetadata.levels).Count -ne 4 -or @($depthProfileMetadata.routes).Count -ne 8 -or @($depthProfileMetadata.doorMappings).Count -ne 4) {
    throw 'The supplied directory is not the explicit native Town/L1-L4 profile.'
}

$sessionDirectory = Join-Path $profileRoot ('sessions/depth-progression-' + [guid]::NewGuid().ToString('N'))
$launcher = Join-Path $repositoryRoot 'godot/run-local-client.ps1'
$arguments = @{
    CathedralNativeDepthProgressionProfileDirectory = $profileRoot
    PersistenceMode = 'cohort'; Headless = $true; Automated = $true
    SessionDirectory = $sessionDirectory; QuitAfterSeconds = $QuitAfterSeconds
    ServerBuildConfiguration = $ServerBuildConfiguration
}
if ($GodotExecutable) { $arguments.GodotExecutable = $GodotExecutable }
if ($CommandTimingTrace) { $arguments.CommandTimingTrace = $true }

Write-Output "Native Town/L1-L4 Journey session: $sessionDirectory"
& $launcher @arguments -AcceptancePhase CathedralNativeDepthProgressionJourney
if ($LASTEXITCODE -ne 0) { throw "Native Town/L1-L4 Journey failed with exit $LASTEXITCODE." }
$journeyPath = Join-Path $sessionDirectory 'cathedralnativedepthprogressionjourney.json'
$journey = Get-Content -LiteralPath $journeyPath -Raw -ErrorAction Stop | ConvertFrom-Json
if ($journey.Passed -ne $true -or $journey.LevelId -ne 0 -or $null -eq $journey.NativeDepthProgression) {
    throw 'The native four-floor Journey did not save a passed Town checkpoint.'
}
$route = $journey.NativeDepthProgression
if ([string]::Join(',', [string[]]@($route.Floors | ForEach-Object { $_.LevelId })) -cne '1,2,3,4' -or
    [string]::Join(',', [string[]]@($route.Combat | ForEach-Object { $_.LevelId })) -cne '2,3,4' -or
    $route.Sequence -ne $journey.Sequence -or [string]$route.PlayerPayload -notmatch '^[A-Za-z0-9+/]+={0,2}$' -or
    [string]$route.WorldHash -cnotmatch '^[0-9A-Fa-f]{64}$' -or
    @($route.Floors | Where-Object { [string]$_.Hash -cnotmatch '^[0-9A-Fa-f]{64}$' }).Count -ne 0) {
    throw 'The native Journey checkpoint is missing exact Town, RNG/player or ordered L1-L4 retention evidence.'
}

$l1 = $route.L1
$gold = $journey.GoldCollection
$healer = $journey.HealerPreparation
if ($null -eq $l1 -or @($l1.Kills).Count -lt 1 -or $l1.DropEntityId -le 0 -or
    $l1.StableItemId -le 0 -or $l1.EquipmentSlot -le 0 -or
    $null -eq $gold -or @($gold.Pickups).Count -lt 1 -or $gold.GoldAfter -le $gold.GoldBefore -or
    $null -eq $healer -or $healer.PurchaseVerified -ne $true -or @($healer.Purchases).Count -lt 1 -or
    $null -eq $route.HealerStock -or [string]$route.HealerStock.Hash -cnotmatch '^[0-9A-Fa-f]{64}$') {
    throw 'The native Journey lacks mandatory earned L1 equipment, Gold or Pepin purchase evidence.'
}
foreach ($pickup in @($gold.Pickups)) {
    if ($pickup.EntityId -le 0 -or $pickup.StableItemId -le 0 -or $pickup.GoldValue -le 0 -or
        $pickup.GoldAfter -le $pickup.GoldBefore -or $pickup.Sequence -le 0 -or $pickup.AppliedTick -le 0) {
        throw 'An L1 Gold pickup is missing its accepted receipt or positive wallet change.'
    }
}
foreach ($purchase in @($healer.Purchases)) {
    if ($purchase.StableItemId -le 0 -or $purchase.Price -le 0 -or
        $purchase.GoldAfter -ge $purchase.GoldBefore -or $purchase.Sequence -le 0 -or $purchase.AppliedTick -le 0) {
        throw 'A Pepin purchase is missing its accepted receipt, item identity or Gold debit.'
    }
}

function Assert-NativeDepthProgressionKill($kill, [uint32]$levelId, [string]$floorLabel) {
    $matchingCorpses = @($kill.EncounterCorpses | Where-Object {
        $_.EntityId -eq $kill.MonsterEntityId -and $_.LevelId -eq $levelId
    })
    if ($kill.MonsterEntityId -le 0 -or $kill.ExperienceAfter -le $kill.ExperienceBefore -or
        $matchingCorpses.Count -ne 1 -or @($kill.AcceptedShots).Count -lt 1) {
        throw "$floorLabel kill lacks its retained corpse, XP or accepted-shot proof."
    }
    foreach ($shot in @($kill.AcceptedShots)) {
        if ($shot.TargetEntityId -ne $kill.MonsterEntityId -or $shot.Sequence -le 0 -or $shot.AppliedTick -le 0) {
            throw "$floorLabel accepted shot is not bound to its defeated actor and receipt."
        }
    }
}
foreach ($kill in @($l1.Kills)) { Assert-NativeDepthProgressionKill $kill 1 'Native L1' }

foreach ($floor in @($route.Combat)) {
    $combat = $floor.Combat
    if ($null -eq $combat -or @($combat.Kills).Count -lt 1 -or
        $combat.ExperienceAfter -le $combat.ExperienceBefore -or
        $combat.WorkUsed -le 0 -or $combat.WorkUsed -gt 576 -or
        $null -eq $combat.EntryGroundItemIds -or $null -eq $combat.ObservedNewDropEntityIds) {
        throw "Native L$($floor.LevelId) combat has no bounded kill/XP evidence."
    }
    if (@($combat.ObservedNewDropEntityIds | Where-Object { $combat.EntryGroundItemIds -contains $_ }).Count -gt 0) {
        throw "Native L$($floor.LevelId) encounter evidence includes a pre-existing ground item."
    }
    foreach ($kill in @($combat.Kills)) {
        Assert-NativeDepthProgressionKill $kill ([uint32]$floor.LevelId) "Native L$($floor.LevelId)"
    }
}

# Keep Journey untouched and resume from an isolated copy of its exact saved checkpoint.
$resumeDirectory = Join-Path $profileRoot ('sessions/depth-progression-resume-' + [guid]::NewGuid().ToString('N'))
Copy-Item -LiteralPath $sessionDirectory -Destination $resumeDirectory -Recurse -ErrorAction Stop
$arguments.SessionDirectory = $resumeDirectory
Write-Output "Native Town/L1-L4 Resume session: $resumeDirectory"
& $launcher @arguments -AcceptancePhase CathedralNativeDepthProgressionResume
if ($LASTEXITCODE -ne 0) { throw "Native Town/L1-L4 Resume failed with exit $LASTEXITCODE." }
$resume = Get-Content -LiteralPath (Join-Path $resumeDirectory 'cathedralnativedepthprogressionresume.json') -Raw -ErrorAction Stop | ConvertFrom-Json
if ($resume.Passed -ne $true -or $resume.LevelId -ne 0 -or $resume.ExactColdBaseline -ne $true -or
    $resume.AllFloorsRetained -ne $true -or $null -eq $resume.Healer -or $null -eq $resume.HealerStockContinuation -or
    $resume.HealerStockContinuation.RetainedStockMatched -ne $true -or
    $resume.HealerStockContinuation.Stock.Hash -cne $route.HealerStock.Hash -or
    $resume.HealerStockContinuation.PurchaseVerified -isnot [bool] -or
    ($resume.HealerStockContinuation.PurchaseVerified -and @($resume.HealerStockContinuation.Purchases).Count -ne 1) -or
    (-not $resume.HealerStockContinuation.PurchaseVerified -and @($resume.HealerStockContinuation.Purchases).Count -ne 0)) {
    throw 'The fresh-host Resume did not prove the exact cold baseline, four retained floors and honest Pepin stock continuation.'
}

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
$journeyProcess = Read-ModeProcessEvidence 'CathedralNativeDepthProgressionJourney'
$sessionDirectory = $resumeDirectory
$resumeProcess = Read-ModeProcessEvidence 'CathedralNativeDepthProgressionResume'
Assert-FreshProcessEvidence $journeyProcess $resumeProcess 1
$profileHash = (Get-FileHash -LiteralPath $profileMetadataPath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($journeyProcess.ServerBuildConfiguration -cne $ServerBuildConfiguration -or
    $resumeProcess.ServerBuildConfiguration -cne $ServerBuildConfiguration -or
    $journeyProcess.ContentHash -cne $depthProfileMetadata.contentManifestHash -or
    $journeyProcess.ProfileHash -cne $profileHash -or $resumeProcess.ProfileHash -cne $profileHash) {
    throw 'The native Journey and Resume changed build or compiled content identity.'
}
foreach ($field in @('ContentHash', 'RulesetHash', 'ProfileHash', 'ContentTreeHash', 'PopulationTreeHash', 'RuntimeTreeHash')) {
    if ([string]$journeyProcess.$field -cnotmatch '^[0-9A-Fa-f]{64}$' -or
        [string]$resumeProcess.$field -cnotmatch '^[0-9A-Fa-f]{64}$' -or
        $journeyProcess.$field -cne $resumeProcess.$field) {
        throw "The native Journey and Resume changed $field."
    }
}
foreach ($process in @($journeyProcess, $resumeProcess)) {
    if ($process.ProfileKind -cne 'cathedral-native-depth-progression-playtest' -or
        $process.AdapterId -cne 'devilution.diablo.cathedral-native-depth-progression.v1') {
        throw 'The native Journey and Resume did not use the explicit four-floor profile adapter.'
    }
    $owned = @([pscustomobject]@{ ProcessId = $process.HostProcessId; StartedUtc = $process.HostStartedUtc }) + @($process.Clients)
    foreach ($identity in $owned) {
        try { $ownedProcess = [Diagnostics.Process]::GetProcessById([int]$identity.ProcessId) }
        catch [ArgumentException] { continue }
        try {
            if ($ownedProcess.StartTime.ToUniversalTime() -eq ([DateTimeOffset]$identity.StartedUtc).UtcDateTime) {
                throw "An owned native depth-progression process is still running: $($identity.ProcessId)."
            }
        } finally { $ownedProcess.Dispose() }
    }
}
Write-Output 'Native Town/L1-L4 Journey and fresh-host retention passed: L1 gear/earned Gold, L2-L4 combat, Town resume.'
