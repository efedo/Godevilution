[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ProfileDirectory,
    [string]$GodotExecutable,
    [ValidateSet('Debug', 'Release')] [string]$ServerBuildConfiguration = 'Debug',
    [ValidateRange(1, 3600)] [int]$QuitAfterSeconds = 900
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
    if ($ExpectedClassId -notin @(0, 2) -or $Scene.Passed -isnot [bool] -or -not $Scene.Passed -or
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

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$profileRoot = (Resolve-Path -LiteralPath $ProfileDirectory).ProviderPath
$metadataPath = Join-Path $profileRoot 'cathedral-native-class-progression-playtest.json'
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw 'The compiled class-bound native profile metadata is missing.'
}
$metadata = Get-Content -LiteralPath $metadataPath -Raw -ErrorAction Stop | ConvertFrom-Json
if (($metadata.formatVersion -isnot [int] -and $metadata.formatVersion -isnot [long]) -or
    $metadata.formatVersion -ne 4 -or
    $metadata.adapterId -cne 'devilution.diablo.cathedral-native-class-progression.v1') {
    throw 'The supplied profile is not the format-4 class-bound native progression profile.'
}
$classFacts = @{
    warrior = @{ Id = 0; Life = 4480; Mana = 640; Light = 10; Idle = 10 }
    sorcerer = @{ Id = 2; Life = 1920; Mana = 4480; Light = 10; Idle = 8 }
}
$classSymbol = [string]$metadata.heroClassSymbol
if (-not $classFacts.ContainsKey($classSymbol)) {
    throw 'The class-bound native profile must select Warrior or Sorcerer.'
}
$facts = $classFacts[$classSymbol]
if ($metadata.heroBaseLifeMaximum -ne $facts.Life -or $metadata.heroBaseManaMaximum -ne $facts.Mana -or
    $metadata.heroLightRadius -ne $facts.Light -or $metadata.heroIdleFrames -ne $facts.Idle) {
    throw 'The class-bound native profile does not contain the canonical selected-class hero facts.'
}
$classId = $facts.Id
$journeyPhase = if ($classId -eq 0) { 'NativeWarriorJourney' } else { 'NativeSorcererJourney' }
$resumePhase = if ($classId -eq 0) { 'NativeWarriorResume' } else { 'NativeSorcererResume' }
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
if (-not [string]::IsNullOrWhiteSpace($GodotExecutable)) { $arguments.GodotExecutable = $GodotExecutable }

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
    $journey.ContentHash -cne $metadata.contentManifestHash -or
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
} elseif ($nativeClass.ChargesBefore -le 0 -or
    $nativeClass.ChargesAfterCast -ne $nativeClass.ChargesBefore - 1 -or
    $nativeClass.ManaAfter -ge $nativeClass.ManaBefore -or
    $nativeClass.ChargesAfterRecharge -ne $nativeClass.MaxChargesAfterRecharge -or
    $nativeClass.MaxChargesAfterRecharge -ge $nativeClass.MaxChargesBeforeRecharge) {
    throw 'The Sorcerer Journey did not prove a charged-staff cast, Mana use and authoritative Recharge mutation.'
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
    $resume.ContentHash -cne $metadata.contentManifestHash -or
    [string]$resume.RulesetHash -cnotmatch '^[0-9a-fA-F]{64}$' -or
    -not $resume.ExactColdBaseline -or $null -eq $resumedClass -or $resumedClass.ClassId -ne $classId -or
    $resumedClass.PlayerPayload -cne $nativeClass.PlayerPayload -or
    $resumedClass.RandomState -ne $nativeClass.RandomState -or $resumedClass.WorldHash -cne $nativeClass.WorldHash -or
    $resumedClass.Sequence -ne $nativeClass.Sequence -or
    $resume.FirstAcceptedSequence -ne ($nativeClass.Sequence + 1) -or
    $resume.FirstAcceptedAppliedTick -le 0 -or $resume.Sequence -ne $resume.FirstAcceptedSequence) {
    throw 'The class Resume did not prove an exact cold baseline and the exact next accepted receipt.'
}

$markerPath = Join-Path $sessionDirectory 'cathedral-native-class-progression-session.json'
$marker = Get-Content -LiteralPath $markerPath -Raw -ErrorAction Stop | ConvertFrom-Json
$metadataHash = (Get-FileHash -LiteralPath $metadataPath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($marker.FormatVersion -ne 1 -or $marker.ProfileKind -cne 'cathedral-native-class-progression-playtest' -or
    $marker.AdapterId -cne 'devilution.diablo.cathedral-native-class-progression.v1' -or
    $marker.MetadataHash -cne $metadataHash -or $marker.ContentTreeHash -cne $metadata.packFileHash -or
    $marker.PopulationTreeHash -cne $metadata.populationPackFileHash -or
    $marker.RuntimeTreeHash -cne $metadata.runtimePackFileHash -or
    $marker.ClassId -ne $classId -or $marker.ClassSymbol -cne $classSymbol) {
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
    ContentHash = [string]$metadata.contentManifestHash
    ProfileHash = $metadataHash
    ContentTreeHash = [string]$metadata.packFileHash
    PopulationTreeHash = [string]$metadata.populationPackFileHash
    RuntimeTreeHash = [string]$metadata.runtimePackFileHash
}
foreach ($evidence in @($journeyProcess, $resumeProcess)) {
    if ($evidence.ServerBuildConfiguration -cne $ServerBuildConfiguration -or
        $evidence.ProfileKind -cne 'cathedral-native-class-progression-playtest' -or
        $evidence.AdapterId -cne 'devilution.diablo.cathedral-native-class-progression.v1' -or
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
Write-Output "Native $classSymbol generated combat, class ability, exact cold resume and fresh-host retention passed. Evidence: $sessionDirectory"
