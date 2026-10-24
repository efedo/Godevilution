[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ProfileDirectory,
    [string]$GodotExecutable,
    [ValidateSet('Debug', 'Release')] [string]$ServerBuildConfiguration = 'Debug',
    [ValidateRange(1, 3600)] [int]$QuitAfterSeconds = 900,
    [switch]$NativeTown,
    # Required for an all-class Town profile; class-bound profiles imply their class.
    [ValidateSet('warrior', 'rogue', 'sorcerer')] [string]$Class
)

$ErrorActionPreference = 'Stop'

function Test-NativeClassIntegerInRange([object]$Value, [decimal]$Minimum, [decimal]$Maximum) {
    if ($null -eq $Value -or $Value.GetType() -notin @(
        [sbyte], [byte], [int16], [uint16], [int], [uint32], [long], [uint64])) {
        return $false
    }
    $number = [decimal]$Value
    return $number -ge $Minimum -and $number -le $Maximum
}

function Assert-NativeClassEvidence($Scene, $ProcessEvidence, [int]$ExpectedClassId, [string]$Phase) {
    if ($ExpectedClassId -notin @(0, 1, 2) -or $Scene.Passed -isnot [bool] -or -not $Scene.Passed -or
        $Scene.Mode -cne 'SinglePlayer' -or
        -not (Test-NativeClassIntegerInRange $Scene.ClassId 0 2) -or $Scene.ClassId -ne $ExpectedClassId -or
        -not (Test-NativeClassIntegerInRange $ProcessEvidence.ClassId 0 2) -or $ProcessEvidence.ClassId -ne $ExpectedClassId) {
        throw "Native class $Phase scene or process evidence changed its bound class identity."
    }
    $checkpoint = $Scene.NativeClass
    if ($null -eq $checkpoint -or
        $checkpoint.PlayerPayload -isnot [string] -or [string]::IsNullOrEmpty($checkpoint.PlayerPayload) -or
        $checkpoint.WorldHash -isnot [string] -or $checkpoint.WorldHash -cnotmatch '^[0-9a-fA-F]{64}$' -or
        -not (Test-NativeClassIntegerInRange $checkpoint.RandomState 0 4294967295) -or
        -not (Test-NativeClassIntegerInRange $checkpoint.Sequence 1 18446744073709551615) -or
        -not (Test-NativeClassIntegerInRange $checkpoint.ClassId 0 2) -or $checkpoint.ClassId -ne $ExpectedClassId) {
        throw "Native class $Phase checkpoint omitted a valid payload, world, random state, sequence, or class identity."
    }
    try { $payloadBytes = [Convert]::FromBase64String($checkpoint.PlayerPayload) }
    catch { throw "Native class $Phase checkpoint PlayerPayload is not canonical base64." }
    if ($payloadBytes.Length -eq 0 -or [Convert]::ToBase64String($payloadBytes) -cne $checkpoint.PlayerPayload) {
        throw "Native class $Phase checkpoint PlayerPayload is not canonical base64."
    }

    if ($Scene.ExactColdBaseline -isnot [bool] -or
        -not (Test-NativeClassIntegerInRange $Scene.Sequence 1 18446744073709551615)) {
        throw "Native class $Phase scene omitted its exact-baseline marker or sequence."
    }
    if ($Scene.ExactColdBaseline) {
        if ($checkpoint.Sequence -ge 18446744073709551615 -or
            $Scene.Sequence -ne ($checkpoint.Sequence + 1) -or
            -not (Test-NativeClassIntegerInRange $Scene.FirstAcceptedSequence 1 18446744073709551615) -or
            $Scene.FirstAcceptedSequence -ne $Scene.Sequence -or
            -not (Test-NativeClassIntegerInRange $Scene.FirstAcceptedAppliedTick 1 18446744073709551615)) {
            throw "Native class $Phase scene sequence does not match the checkpoint and exact next receipt."
        }
    } elseif ($Scene.Sequence -ne $checkpoint.Sequence) {
        throw "Native class $Phase scene sequence does not match its checkpoint."
    }

    foreach ($field in @('ContentHash', 'RulesetHash')) {
        $sceneHash = $Scene.$field
        $processHash = $ProcessEvidence.$field
        if ($sceneHash -isnot [string] -or $sceneHash -cnotmatch '^[0-9a-fA-F]{64}$' -or
            $processHash -isnot [string] -or $processHash -cnotmatch '^[0-9a-fA-F]{64}$' -or
            $sceneHash -cne $processHash) {
            throw "Native class $Phase scene $field does not match its process evidence."
        }
    }
}

function Assert-NativeObjectArtEvidence($Scene, [bool]$Resumed) {
    if ([string]::IsNullOrWhiteSpace($env:GODEVILUTION_NATIVE_OBJECT_PACK)) { return }
    $objects = $Scene.NativeObjects
    if ($objects.Passed -isnot [bool] -or -not $objects.Passed -or
        $objects.CurrentSnapshotFramesOnly -isnot [bool] -or -not $objects.CurrentSnapshotFramesOnly -or
        $objects.NativeLifecycleTimingParity -isnot [bool] -or $objects.NativeLifecycleTimingParity -or
        $objects.NativeLightingAndScenePhaseParity -isnot [bool] -or $objects.NativeLightingAndScenePhaseParity -or
        $objects.RenderedParity -isnot [bool] -or $objects.RenderedParity -or
        $objects.ColdL1RedrawVerified -isnot [bool] -or $objects.ColdL1RedrawVerified -or
        $objects.Pack.IsLoaded -isnot [bool] -or -not $objects.Pack.IsLoaded -or
        $objects.Pack.PackId -cnotmatch '^native-object-[0-9a-f]{24}$' -or
        $objects.Pack.ContentManifestHash -cne $Scene.ContentHash -or
        $objects.Checkpoint.ContentHash -cne $Scene.ContentHash -or
        @($objects.Checkpoint.Objects).Count -eq 0 -or
        $objects.Checkpoint.GeometryHash -cnotmatch '^[0-9a-fA-F]{64}$' -or
        $objects.ColdObjectFrameStateCollisionRetained -isnot [bool] -or
        $objects.ColdObjectFrameStateCollisionRetained -ne $Resumed) {
        throw 'Native object art evidence omitted its scoped current-frame or exact cold state contract.'
    }
    $interaction = $objects.Checkpoint.Interaction
    if ($interaction.NativeObjectId -notin @(5, 6, 7) -or $interaction.EntityId -le 0 -or
        $interaction.BeforeFrame -notin @(1, 4) -or $interaction.AfterFrame -ne ($interaction.BeforeFrame + 2) -or
        $interaction.Sequence -le 0 -or $interaction.AppliedTick -le 0) {
        throw 'Native object art evidence omitted its ordinary chest frame transition.'
    }
    foreach ($draw in @($interaction.BeforeDraw, $interaction.AfterDraw)) {
        if ($null -eq $draw -or $draw.NativeArt -isnot [bool] -or -not $draw.NativeArt -or
            $draw.GenericFallback -isnot [bool] -or $draw.GenericFallback -or
            $draw.AnchorInsideViewport -isnot [bool] -or -not $draw.AnchorInsideViewport -or
            $draw.EntityId -ne $interaction.EntityId -or $draw.NativeObjectId -ne $interaction.NativeObjectId -or
            $draw.LevelId -ne 1 -or $draw.SnapshotTick -le 0 -or $draw.NativeWidth -ne 96 -or
            $draw.PackId -cne $objects.Pack.PackId -or
            $draw.Destroyed -isnot [bool] -or $draw.Destroyed -or $draw.TrapArmed -isnot [bool] -or $draw.TrapArmed -or
            [string]::IsNullOrWhiteSpace($draw.AtlasPath) -or $draw.Width -le 0 -or $draw.Height -le 0 -or
            $draw.DrawX -ne ($draw.AnchorX - 16) -or $draw.DrawY -ne ($draw.AnchorY - $draw.Height + 1)) {
            throw 'Native object art evidence omitted an actual correctly placed chest draw.'
        }
    }
    if ($interaction.BeforeDraw.NativeFrame -ne $interaction.BeforeFrame -or
        $interaction.AfterDraw.NativeFrame -ne $interaction.AfterFrame -or
        $interaction.BeforeDraw.PositionX -ne $interaction.AfterDraw.PositionX -or
        $interaction.BeforeDraw.PositionY -ne $interaction.AfterDraw.PositionY -or
        $interaction.BeforeDraw.SnapshotTick -gt $interaction.AppliedTick -or
        $interaction.AfterDraw.SnapshotTick -lt $interaction.AppliedTick -or
        $interaction.BeforeDraw.Activated -isnot [bool] -or $interaction.BeforeDraw.Activated -or
        $interaction.AfterDraw.Activated -isnot [bool] -or -not $interaction.AfterDraw.Activated) {
        throw 'Native object art evidence drew a frame other than the ordinary authoritative chest state.'
    }
    if (-not $Resumed -and ($objects.Pack.AtlasesReady -isnot [bool] -or -not $objects.Pack.AtlasesReady -or
        @($objects.Pack.LoadedAtlasPaths).Count -ne 8 -or @($objects.Pack.MissingAtlasPaths).Count -ne 0 -or
        @($objects.Samples).Count -eq 0 -or @($objects.Samples | Where-Object {
            $_.NativeArt -isnot [bool] -or -not $_.NativeArt -or
            $_.GenericFallback -isnot [bool] -or $_.GenericFallback -or
            $_.AnchorInsideViewport -isnot [bool] -or -not $_.AnchorInsideViewport
        }).Count -ne 0)) {
        throw 'Native object art evidence omitted its complete texture family or actual object samples.'
    }
}

function Assert-NativeTownSceneEvidence($Scene) {
    if ($Scene.NativeTown.Passed -isnot [bool] -or -not $Scene.NativeTown.Passed -or
        $Scene.NativeTown.FineCells -ne 12544 -or $Scene.NativeTown.SpecialCells -ne 12544 -or
        $Scene.NativeTown.OccupiedCells -ne 20 -or $Scene.NativeTown.GeometryHash -cnotmatch '^[0-9a-f]{64}$' -or
        $Scene.NativeSceneArt.Passed -isnot [bool] -or -not $Scene.NativeSceneArt.Passed -or
        $Scene.NativeSceneArt.TownProfileIntegration -isnot [bool] -or -not $Scene.NativeSceneArt.TownProfileIntegration) {
        throw 'Native Town scene evidence omitted its complete geometry, resident collision, or native drawing.'
    }
    $townDraws = @($Scene.NativeSceneArt.Samples | Where-Object {
        $_.Drawing.NativeSceneActive -and $_.Drawing.Location -ceq 'town' -and $_.Drawing.LevelId -eq 0 -and
        $_.Drawing.FloorRasterDrawCount -gt 0 -and $_.Drawing.ContentRasterDrawCount -gt 0 -and
        $_.Drawing.ActorDrawCount -gt 0 -and $_.Drawing.TextureDrawCount -gt 0 -and $_.PointerRoundTrip
    })
    if ($townDraws.Count -eq 0) { throw 'Native Town did not emit actual floor, content and actor draws with pointer mapping.' }
}

function Assert-NativeTownerEvidence($Scene, [bool]$Resumed) {
    if ([string]::IsNullOrWhiteSpace($env:GODEVILUTION_NATIVE_TOWNER_PACK)) { return }
    $resident = $Scene.NativeTowners
    if ($resident.Passed -isnot [bool] -or -not $resident.Passed -or
        $resident.CosmeticAnimation -isnot [bool] -or -not $resident.CosmeticAnimation -or
        $resident.NativeInitializationAndColdPhaseParity -isnot [bool] -or $resident.NativeInitializationAndColdPhaseParity -or
        $resident.RenderedParity -isnot [bool] -or $resident.RenderedParity -or
        $resident.Pack.IsLoaded -isnot [bool] -or -not $resident.Pack.IsLoaded -or
        $resident.Pack.IdentityMatched -isnot [bool] -or -not $resident.Pack.IdentityMatched -or
        $resident.Pack.AtlasesReady -isnot [bool] -or -not $resident.Pack.AtlasesReady -or
        $resident.Pack.ContentManifestHash -cne $Scene.ContentHash -or
        $resident.Pack.TownCatalogFingerprint -cne $Scene.NativeTown.CatalogFingerprint -or
        @($resident.Pack.MissingAtlasPaths).Count -ne 0 -or @($resident.Pack.LoadedAtlasPaths).Count -eq 0 -or
        @($resident.Samples).Count -eq 0 -or
        @($resident.Samples | Where-Object { -not $_.NativeArt -or $_.GenericFallback -or -not $_.AnchorInsideViewport -or [string]::IsNullOrWhiteSpace($_.AtlasPath) }).Count -gt 0) {
        throw 'Native resident evidence omitted complete content/roster/atlas binding or actual draws.'
    }
    if ($Resumed) {
        $ready = $resident.ColdReadiness
        $probe = $ready.Probe; $reply = $ready.Reply; $snapshots = @($ready.Snapshots)
        if ($ready.Passed -isnot [bool] -or -not $ready.Passed -or $ready.StartTimestamp -le 0 -or
            $ready.StopwatchFrequency -le 0 -or $ready.EndRevision -lt ($ready.StartRevision + 2) -or
            $ready.EndTick -le $ready.StartTick -or $probe.TimingRequestId -le 0 -or
            $probe.SentStopwatchTimestamp -le $ready.StartTimestamp -or
            $probe.WriteStartStopwatchTimestamp -le $ready.StartTimestamp -or
            $probe.WriteEndStopwatchTimestamp -lt $probe.WriteStartStopwatchTimestamp -or $probe.WriteOutcome -ne 1 -or
            $reply.TimingRequestId -ne $probe.TimingRequestId -or $reply.TransportGeneration -ne $probe.TransportGeneration -or
            $reply.Outcome -ne 0 -or $reply.TimingCalibrationReady -isnot [bool] -or -not $reply.TimingCalibrationReady -or
            $reply.OutcomeStopwatchTimestamp -le $probe.WriteEndStopwatchTimestamp -or $snapshots.Count -ne 2 -or
            @($snapshots | Where-Object { $_.TransportGeneration -ne $probe.TransportGeneration -or
                $_.ReceivedStopwatchTimestamp -le $reply.OutcomeStopwatchTimestamp }).Count -ne 0 -or
            $snapshots[0].SnapshotTick -le $ready.StartTick -or $snapshots[1].SnapshotTick -le $snapshots[0].SnapshotTick -or
            $ready.EndTick -lt $snapshots[1].SnapshotTick) {
            throw 'Native resident cold evidence omitted a post-draw probe reply and two fresh consumed snapshots.'
        }
    } else {
        if ($resident.CompleteRosterRoaming -isnot [bool] -or -not $resident.CompleteRosterRoaming) {
            throw 'Native resident journey did not complete actual roster roaming.'
        }
        foreach ($slot in 0..10) {
            $frames = @($resident.Samples | Where-Object { $_.ResidentSlot -eq $slot } | Select-Object -ExpandProperty FrameIndex -Unique)
            if ($frames.Count -lt 2) { throw "Native resident slot $slot did not draw two distinct native frames." }
        }
    }
}
function Read-NativeTownCheckpointHash([string]$Session, [string]$Fingerprint) {
    $document = Get-Content -LiteralPath (Join-Path $Session 'save/authoritative-cohort.json') -Raw | ConvertFrom-Json
    $state = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($document.SerializedModuleState)) | ConvertFrom-Json
    $town = $state.NativeTown
    if ($state.FormatVersion -ne 11 -or $null -eq $town -or $town.CatalogFingerprint -cne $Fingerprint -or
        @($town.NativePieceLayer).Count -ne 12544 -or @($town.NativeSpecialLayer).Count -ne 12544 -or
        @($town.NativeMegaTileLayer).Count -ne 1600 -or @($town.NativeSolPropertyLayer).Count -ne 12544 -or
        @($town.NativeTransparencyLayer).Count -ne 12544 -or @($town.ClosedHellTileChoices).Count -ne 10) {
        throw 'Native Town persistence omitted the complete version-11 checkpoint.'
    }
    $text = $town | ConvertTo-Json -Depth 64 -Compress
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$profileRoot = (Resolve-Path -LiteralPath $ProfileDirectory).ProviderPath
$metadataPath = Join-Path $profileRoot 'cathedral-native-class-progression-playtest.json'
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw 'The compiled class-bound native profile metadata is missing.'
}
$metadata = Get-Content -LiteralPath $metadataPath -Raw -ErrorAction Stop | ConvertFrom-Json
$townMetadata = $null
$evidenceMetadataPath = $metadataPath
$contentManifestHash = [string]$metadata.contentManifestHash
$profileKind = 'cathedral-native-class-progression-playtest'
$adapterId = 'devilution.diablo.cathedral-native-class-progression.v1'
$allClass = $NativeTown -and (Test-Path -LiteralPath (Join-Path $profileRoot 'cathedral-native-all-class-town-playtest.json') -PathType Leaf)
if ($allClass -ne [bool]$Class) { throw '-Class is required for, and only for, an all-class native Town profile.' }
if ($NativeTown) {
    $townFile = if ($allClass) { 'cathedral-native-all-class-town-playtest.json' } else { 'cathedral-native-town-progression-playtest.json' }
    $evidenceMetadataPath = Join-Path $profileRoot $townFile
    $townMetadata = Get-Content -LiteralPath $evidenceMetadataPath -Raw -ErrorAction Stop | ConvertFrom-Json
    $expectedTownAdapter = if ($allClass) { 'devilution.diablo.cathedral-native-all-class-town.v1' } else { 'devilution.diablo.cathedral-native-town-progression.v1' }
    if ($townMetadata.formatVersion -ne $(if ($allClass) { 6 } else { 5 }) -or
        $townMetadata.adapterId -cne $expectedTownAdapter -or
        ($townMetadata.classProfile | ConvertTo-Json -Depth 64 -Compress) -cne
            ($metadata | ConvertTo-Json -Depth 64 -Compress)) {
        throw 'The supplied Town profile does not retain the exact format-4 class identity.'
    }
    $contentManifestHash = [string]$townMetadata.contentManifestHash
    $profileKind = if ($allClass) { 'cathedral-native-all-class-town-playtest' } else { 'cathedral-native-town-progression-playtest' }
    $adapterId = [string]$townMetadata.adapterId
    if ([string]::IsNullOrWhiteSpace($env:GODEVILUTION_NATIVE_SCENE_PACK) -or
        [string]::IsNullOrWhiteSpace($env:GODEVILUTION_NATIVE_PLAYER_PACK)) {
        throw 'Native Town graphical acceptance requires explicit native scene and player packs.'
    }
}
if (($metadata.formatVersion -isnot [int] -and $metadata.formatVersion -isnot [long]) -or
    $metadata.formatVersion -ne 4 -or
    $metadata.adapterId -cne 'devilution.diablo.cathedral-native-class-progression.v1') {
    throw 'The supplied profile is not the format-4 class-bound native progression profile.'
}
$classFacts = @{
    warrior = @{ Id = 0; Life = 4480; Mana = 640; Light = 10; Idle = 10; Phase = 'NativeWarrior' }
    rogue = @{ Id = 1; Life = 2880; Mana = 1440; Light = 10; Idle = 8; Phase = 'NativeRogue' }
    sorcerer = @{ Id = 2; Life = 1920; Mana = 4480; Light = 10; Idle = 8; Phase = 'NativeSorcerer' }
}
# An all-class profile's hero fields describe its default Warrior; the chosen class comes from -Class.
$classSymbol = if ($allClass) { $Class } else { [string]$metadata.heroClassSymbol }
if (-not $classFacts.ContainsKey($classSymbol) -or (-not $allClass -and $classSymbol -ceq 'rogue')) {
    throw 'The class-bound native profile must select Warrior or Sorcerer.'
}
$facts = $classFacts[$classSymbol]
$profileFacts = if ($allClass) { @($townMetadata.classes | Where-Object classId -eq $facts.Id)[0] } else { $null }
if ($allClass -and ($null -eq $profileFacts -or $profileFacts.heroBaseLifeMaximum -ne $facts.Life -or
        $profileFacts.heroBaseManaMaximum -ne $facts.Mana -or $profileFacts.heroLightRadius -ne $facts.Light -or
        $profileFacts.heroIdleFrames -ne $facts.Idle)) {
    throw 'The all-class native profile does not contain the canonical selected-class hero facts.'
}
if (-not $allClass -and ($metadata.heroBaseLifeMaximum -ne $facts.Life -or $metadata.heroBaseManaMaximum -ne $facts.Mana -or
    $metadata.heroLightRadius -ne $facts.Light -or $metadata.heroIdleFrames -ne $facts.Idle)) {
    throw 'The class-bound native profile does not contain the canonical selected-class hero facts.'
}
$classId = $facts.Id
$journeyPhase = $facts.Phase + 'Journey'
$resumePhase = $facts.Phase + 'Resume'
$sessionDirectory = Join-Path $profileRoot ('sessions/native-class-' + $classSymbol + '-' + [guid]::NewGuid().ToString('N'))
$sessionsRoot = Join-Path $profileRoot 'sessions'
New-Item -ItemType Directory -Path $sessionsRoot -Force | Out-Null
$launcher = Join-Path $repositoryRoot 'godot-client/run-local-client.ps1'
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) { throw 'The authoritative Godot launcher is missing.' }
$arguments = @{
    CathedralNativeClassProfileDirectory = $profileRoot
    PersistenceMode = 'cohort'
    Headless = $true
    Automated = $true
    SessionDirectory = $sessionDirectory
    QuitAfterSeconds = $QuitAfterSeconds
    ServerBuildConfiguration = $ServerBuildConfiguration
}
if ($NativeTown) {
    $arguments.Remove('CathedralNativeClassProfileDirectory')
    $arguments.CathedralNativeTownProfileDirectory = $profileRoot
}
if (-not [string]::IsNullOrWhiteSpace($GodotExecutable)) { $arguments.GodotExecutable = $GodotExecutable }
if (-not [string]::IsNullOrWhiteSpace($env:GODEVILUTION_NATIVE_TOWNER_PACK)) { $arguments.CommandTimingTrace = $true }

Write-Output "Native $classSymbol Journey session: $sessionDirectory"
& $launcher @arguments -AcceptancePhase $journeyPhase
$journeyExitCode = $LASTEXITCODE
if ($journeyExitCode -ne 0) { throw "Native $classSymbol Journey failed with exit $journeyExitCode." }
$journeyPath = Join-Path $sessionDirectory ($journeyPhase.ToLowerInvariant() + '.json')
$journey = Get-Content -LiteralPath $journeyPath -Raw -ErrorAction Stop | ConvertFrom-Json
$attacksPath = Join-Path $sessionDirectory 'native-class-attacks.json'
$attacks = Get-Content -LiteralPath $attacksPath -Raw -ErrorAction Stop | ConvertFrom-Json
$nativeClass = $journey.NativeClass
$defeatedAttacks = @($attacks.Attacks | Where-Object {
    $_.Target -eq $nativeClass.DefeatedMonster -and $_.Sequence -gt 0 -and $_.AppliedTick -gt 0
})
if ($journey.Passed -isnot [bool] -or -not $journey.Passed -or $journey.Mode -cne 'SinglePlayer' -or
    $journey.ClassId -ne $classId -or $journey.LevelId -ne 0 -or $journey.ExactColdBaseline -ne $false -or
    $journey.ContentHash -cne $contentManifestHash -or
    [string]$journey.RulesetHash -cnotmatch '^[0-9a-fA-F]{64}$' -or
    $null -eq $nativeClass -or $nativeClass.ClassId -ne $classId -or $nativeClass.DefeatedMonster -le 0 -or
    $nativeClass.ExperienceAfter -le $nativeClass.ExperienceBefore -or
    $attacks.Passed -isnot [bool] -or -not $attacks.Passed -or @($attacks.Attacks).Count -lt 1 -or
    $defeatedAttacks.Count -lt 1) {
    throw 'The native class Journey did not prove a generated kill, accepted combat and earned XP.'
}
if ($classId -eq 0) {
    if (-not (Test-NativeClassIntegerInRange $nativeClass.RepairItemSlot 4 5) -or
        -not (Test-NativeClassIntegerInRange $nativeClass.RepairItemStableId 1 4294967295) -or
        $nativeClass.InitialDurability -le $nativeClass.DurabilityBeforeRepair -or
        $nativeClass.DurabilityAfterRepair -ne $nativeClass.MaxDurabilityAfterRepair -or
        $nativeClass.MaxDurabilityAfterRepair -ge $nativeClass.MaxDurabilityBeforeRepair) {
        throw 'The Warrior Journey did not prove natural starting-equipment wear followed by the authoritative Repair mutation.'
    }
} elseif ($classId -eq 2 -and ($nativeClass.ChargesBefore -le 0 -or
    $nativeClass.ChargesAfterCast -ne $nativeClass.ChargesBefore - 1 -or
    $nativeClass.ManaAfter -ge $nativeClass.ManaBefore -or
    $nativeClass.ChargesAfterRecharge -ne $nativeClass.MaxChargesAfterRecharge -or
    $nativeClass.MaxChargesAfterRecharge -ge $nativeClass.MaxChargesBeforeRecharge)) {
    throw 'The Sorcerer Journey did not prove a charged-staff cast, Mana use and authoritative Recharge mutation.'
}

Assert-NativeObjectArtEvidence $journey $false
$townCheckpointHash = $null
if ($NativeTown) {
    $services = Get-Content -LiteralPath (Join-Path $sessionDirectory 'native-town-services.json') -Raw | ConvertFrom-Json
    if ($services.Passed -isnot [bool] -or -not $services.Passed -or $services.SmithSequence -le 0 -or
        $services.FinalSequence -le $services.SmithSequence) { throw 'Native Town Smith and Pepin service controls were not verified.' }
    $townCheckpointHash = Read-NativeTownCheckpointHash $sessionDirectory $townMetadata.townCatalogFingerprint
    Assert-NativeTownSceneEvidence $journey
    Assert-NativeTownerEvidence $journey $false
}
Write-Output "Native $classSymbol Resume session: $sessionDirectory"
& $launcher @arguments -AcceptancePhase $resumePhase
$resumeExitCode = $LASTEXITCODE
if ($resumeExitCode -ne 0) { throw "Native $classSymbol Resume failed with exit $resumeExitCode." }
$resumePath = Join-Path $sessionDirectory ($resumePhase.ToLowerInvariant() + '.json')
$resume = Get-Content -LiteralPath $resumePath -Raw -ErrorAction Stop | ConvertFrom-Json
$resumedClass = $resume.NativeClass
if ($resume.Passed -isnot [bool] -or -not $resume.Passed -or $resume.Mode -cne 'SinglePlayer' -or
    $resume.ClassId -ne $classId -or $resume.LevelId -ne 0 -or $resume.ExactColdBaseline -isnot [bool] -or
    $resume.ContentHash -cne $contentManifestHash -or
    [string]$resume.RulesetHash -cnotmatch '^[0-9a-fA-F]{64}$' -or
    -not $resume.ExactColdBaseline -or $null -eq $resumedClass -or $resumedClass.ClassId -ne $classId -or
    $resumedClass.PlayerPayload -cne $nativeClass.PlayerPayload -or
    $resumedClass.RandomState -ne $nativeClass.RandomState -or $resumedClass.WorldHash -cne $nativeClass.WorldHash -or
    $resumedClass.Sequence -ne $nativeClass.Sequence -or
    $resume.FirstAcceptedSequence -ne ($nativeClass.Sequence + 1) -or
    $resume.FirstAcceptedAppliedTick -le 0 -or $resume.Sequence -ne $resume.FirstAcceptedSequence) {
    throw 'The class Resume did not prove an exact cold baseline and the exact next accepted receipt.'
}

Assert-NativeObjectArtEvidence $resume $true
if (-not [string]::IsNullOrWhiteSpace($env:GODEVILUTION_NATIVE_OBJECT_PACK) -and
    ([string]::Join('|', @($journey.NativeObjects.Checkpoint.Objects)) -cne [string]::Join('|', @($resume.NativeObjects.Checkpoint.Objects)) -or
    $journey.NativeObjects.Checkpoint.GeometryHash -cne $resume.NativeObjects.Checkpoint.GeometryHash)) {
    throw 'Native object art cold evidence changed its saved object payloads or collision geometry.'
}
if ($NativeTown) {
    Assert-NativeTownSceneEvidence $resume
    Assert-NativeTownerEvidence $resume $true
    if ($resume.NativeTown.GeometryHash -cne $journey.NativeTown.GeometryHash -or
        (Read-NativeTownCheckpointHash $sessionDirectory $townMetadata.townCatalogFingerprint) -cne $townCheckpointHash) {
        throw 'Native Town wire geometry or full fine/coarse/special/protected/RNG checkpoint changed across cold restore.'
    }
}
$markerFile = if ($allClass) { 'cathedral-native-all-class-town-session.json' } elseif ($NativeTown) { 'cathedral-native-town-progression-session.json' } else { 'cathedral-native-class-progression-session.json' }
$markerClassId = if ($allClass) { -1 } else { $classId }
$markerClassSymbol = if ($allClass) { 'all' } else { $classSymbol }
$markerPath = Join-Path $sessionDirectory $markerFile
$marker = Get-Content -LiteralPath $markerPath -Raw -ErrorAction Stop | ConvertFrom-Json
$metadataHash = (Get-FileHash -LiteralPath $evidenceMetadataPath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($marker.FormatVersion -ne 1 -or $marker.ProfileKind -cne $profileKind -or
    $marker.AdapterId -cne $adapterId -or
    $marker.MetadataHash -cne $metadataHash -or $marker.ContentTreeHash -cne $metadata.packFileHash -or
    $marker.PopulationTreeHash -cne $metadata.populationPackFileHash -or
    $marker.RuntimeTreeHash -cne $metadata.runtimePackFileHash -or
    $marker.ClassId -ne $markerClassId -or $marker.ClassSymbol -cne $markerClassSymbol) {
    throw 'The save marker does not bind this session to the selected class-bound profile.'
}

# Reuse the shared fresh-host evidence validators without starting another app or rebuilding it.
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
$journeyProcess = Read-ModeProcessEvidence $journeyPhase
$resumeProcess = Read-ModeProcessEvidence $resumePhase
Assert-FreshProcessEvidence $journeyProcess $resumeProcess 1
Assert-NativeClassEvidence $journey $journeyProcess $classId 'Journey'
Assert-NativeClassEvidence $resume $resumeProcess $classId 'Resume'
$expectedHashes = @{
    ContentHash = $contentManifestHash
    ProfileHash = $metadataHash
    ContentTreeHash = [string]$metadata.packFileHash
    PopulationTreeHash = [string]$metadata.populationPackFileHash
    RuntimeTreeHash = [string]$metadata.runtimePackFileHash
}
if ($NativeTown) {
    $expectedHashes.TownTreeHash = [string]$townMetadata.townPackTreeHash
    $expectedHashes.TownCatalogFingerprint = [string]$townMetadata.townCatalogFingerprint
    if ($marker.TownTreeHash -cne $townMetadata.townPackTreeHash -or
        $marker.TownCatalogFingerprint -cne $townMetadata.townCatalogFingerprint -or
        $marker.ContentManifestHash -cne $contentManifestHash -or $marker.TownInitialSeed -ne $townMetadata.townInitialSeed) {
        throw 'The Town session marker does not bind its final content, semantic pack, catalog, and seed.'
    }
}
foreach ($evidence in @($journeyProcess, $resumeProcess)) {
    if ($evidence.ServerBuildConfiguration -cne $ServerBuildConfiguration -or
        $evidence.ProfileKind -cne $profileKind -or
        $evidence.AdapterId -cne $adapterId -or
        $evidence.ClassId -ne $classId -or
        [string]$evidence.RulesetHash -cnotmatch '^[0-9a-fA-F]{64}$') {
        throw 'Native class process evidence changed its build, adapter, class, or ruleset identity.'
    }
    foreach ($field in $expectedHashes.Keys) {
        if ([string]$evidence.$field -cnotmatch '^[0-9a-fA-F]{64}$' -or
            $evidence.$field -cne $expectedHashes[$field]) {
            throw "Native class process evidence does not match the compiled profile $field."
        }
    }
}
foreach ($field in @('ContentHash', 'RulesetHash', 'ProfileHash', 'ContentTreeHash', 'PopulationTreeHash', 'RuntimeTreeHash')) {
    if ($journeyProcess.$field -cne $resumeProcess.$field) {
        throw "Native class Journey and Resume changed $field."
    }
}
foreach ($processEvidence in @($journeyProcess, $resumeProcess)) {
    $owned = @([pscustomobject]@{ ProcessId = $processEvidence.HostProcessId; StartedUtc = $processEvidence.HostStartedUtc }) + @($processEvidence.Clients)
    foreach ($identity in $owned) {
        try { $ownedProcess = [Diagnostics.Process]::GetProcessById([int]$identity.ProcessId) }
        catch [ArgumentException] { continue }
        try {
            if ($ownedProcess.StartTime.ToUniversalTime() -eq ([DateTimeOffset]$identity.StartedUtc).UtcDateTime) {
                throw "An owned native class process is still running: $($identity.ProcessId)."
            }
        } finally { $ownedProcess.Dispose() }
    }
}
$checks = if ($classId -eq 1) { 'generated combat' } else { 'generated combat, class ability' }
Write-Output "Native $classSymbol $checks, exact cold resume and fresh-host retention passed. Evidence: $sessionDirectory"
