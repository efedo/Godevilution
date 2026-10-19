using System.Diagnostics;
using System.Text;
using Xunit;

namespace Devilution.Server.Tests;

/** Source contracts for the opt-in two-client authored combat/loot journey. */
public sealed class CooperativeLauncherContractTests
{
    [Fact]
    public void CooperativeProfileIsExplicitCohortPreviewAndFreshHostBounded()
    {
        var driver = ReadSource("tools/test-godot-session.ps1");
        var launcher = ReadSource("godot-client/run-local-client.ps1");
        var acceptance = ReadSource("godot-client/Devilution.Client/Acceptance/SessionAcceptance.cs");
        var evidence = ReadSource("godot-client/Devilution.Client/Diagnostics/ClientCommandTimingEvidenceWriter.cs");

        Assert.Contains("[switch]$Cooperative", driver, StringComparison.Ordinal);
        Assert.Contains("$cooperative", driver, StringComparison.Ordinal);
        Assert.Contains("PersistenceMode = 'cohort'", driver, StringComparison.Ordinal);
        Assert.Contains("$parameters.PreviewContent = $true", driver, StringComparison.Ordinal);
        Assert.Contains("Assert-CooperativeEvidence", driver, StringComparison.Ordinal);
        Assert.Contains("Assert-FreshProcessEvidence $created $resumed 2", driver, StringComparison.Ordinal);
        Assert.Contains("process evidence omitted validated host content/ruleset identity", driver, StringComparison.Ordinal);
        Assert.Contains("$processEvidence.ContentHash = $contentHash", launcher, StringComparison.Ordinal);
        Assert.Contains("$processEvidence.RulesetHash = $rulesetHash", launcher, StringComparison.Ordinal);
        Assert.Contains("CooperativeJourney", launcher, StringComparison.Ordinal);
        Assert.Contains("CooperativeResume", launcher, StringComparison.Ordinal);
        Assert.Contains("Cooperative acceptance requires the town-loop-preview content overlay.", launcher, StringComparison.Ordinal);
        Assert.Contains("Cooperative acceptance requires the explicit complete-cohort persistence mode.", launcher, StringComparison.Ordinal);
        Assert.Contains("RunCooperativeAsync", acceptance, StringComparison.Ordinal);
        Assert.Contains("CooperativeJourney", evidence, StringComparison.Ordinal);
        Assert.Contains("CooperativeResume", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void CooperativeAcceptanceUsesAuthoredDropAndChecksOwnershipEvidence()
    {
        var acceptance = ReadSource("godot-client/Devilution.Client/Acceptance/SessionAcceptance.Cooperative.cs");
        var content = ReadSource("server/content/town-loop-preview/monsters.tsv");
        var pack = ReadSource("server/content/town-loop-preview/content-pack.json");

        Assert.Contains("GODEVILUTION_PERSISTENCE_MODE", acceptance, StringComparison.Ordinal);
        Assert.Contains("GameMode.Multiplayer", acceptance, StringComparison.Ordinal);
        Assert.Contains("ClickWorld", acceptance, StringComparison.Ordinal);
        Assert.Contains("MonsterDefeated", acceptance, StringComparison.Ordinal);
        Assert.Contains("OwnerInventoryCount", acceptance, StringComparison.Ordinal);
        Assert.Contains("DropPresent", acceptance, StringComparison.Ordinal);
        Assert.Contains("DropSeed", acceptance, StringComparison.Ordinal);
        Assert.Contains("RecordCooperativeDrawAsync", acceptance, StringComparison.Ordinal);
        Assert.Contains("CurrentAction is null", acceptance, StringComparison.Ordinal);
        Assert.Contains("entry-clear.json", acceptance, StringComparison.Ordinal);
        Assert.Contains("ReceiptStatus", acceptance, StringComparison.Ordinal);
        Assert.Contains("PickupStatus", acceptance, StringComparison.Ordinal);
        Assert.Contains("Assert-CohortRecoveryReceipt", ReadSource("godot-client/run-local-client.ps1"), StringComparison.Ordinal);
        Assert.Contains("1000009", content, StringComparison.Ordinal);
        Assert.Contains("6001", content, StringComparison.Ordinal);
        Assert.Contains("town-loop-preview", pack, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingProfilesDoNotImplicitlySelectCooperativeAcceptance()
    {
        var driver = ReadSource("tools/test-godot-session.ps1");
        var launcher = ReadSource("godot-client/run-local-client.ps1");
        Assert.Contains("if ($cooperative)", driver, StringComparison.Ordinal);
        Assert.Contains("if ($cooperative -and -not $PreviewContent)", launcher, StringComparison.Ordinal);
        Assert.Contains("if ($Multiplayer -and ($PreviewContent -or $PortalPreviewContent) -and -not $cooperative)", launcher, StringComparison.Ordinal);
        Assert.Contains("$cooperative = $AcceptancePhase -in @('CooperativeJourney', 'CooperativeResume')", launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void CooperativeRouteWaitsForProcessDispatchedIntentBeforeReadingItsSequence()
    {
        var acceptance = ReadSource("godot-client/Devilution.Client/Acceptance/SessionAcceptance.Cooperative.cs");
        var start = acceptance.IndexOf("private async Task TravelCooperativeToDungeonAsync", StringComparison.Ordinal);
        var end = acceptance.IndexOf("private async Task CooperativeAttackOnceAsync", start, StringComparison.Ordinal);
        var route = acceptance[start..end];
        var click = route.IndexOf("await Click(travel.GetGlobalRect().GetCenter());", StringComparison.Ordinal);
        var wait = route.IndexOf("await Until(() => main.ObservedLastQueuedSequence > queuedBefore,", StringComparison.Ordinal);
        var read = route.IndexOf("var inputSequence = main.ObservedLastQueuedSequence;", StringComparison.Ordinal);
        Assert.True(click >= 0 && wait > click && read > wait);
        Assert.DoesNotContain("Require(inputSequence > queuedBefore", route, StringComparison.Ordinal);
    }

    [Fact]
    public void CooperativeStairStagingBindsPeerObservationAndKeepsCancellationStable()
    {
        var acceptance = ReadSource("godot-client/Devilution.Client/Acceptance/SessionAcceptance.Cooperative.cs");
        Assert.Contains("stageNearStair: true", acceptance, StringComparison.Ordinal);
        Assert.Contains("staged.EntityId == marker.RootElement.GetProperty(\"EntityId\").GetUInt32()", acceptance, StringComparison.Ordinal);
        Assert.Contains("staged.PositionX == marker.RootElement.GetProperty(\"X\").GetInt32()", acceptance, StringComparison.Ordinal);
        Assert.Contains("staged.PositionY == marker.RootElement.GetProperty(\"Y\").GetInt32()", acceptance, StringComparison.Ordinal);
        Assert.Contains("main.ObservedSnapshot!.Tick >= parkedTick + 4", acceptance, StringComparison.Ordinal);
        Assert.Contains("main.ObservedLastQueuedSequence == parkedSequence", acceptance, StringComparison.Ordinal);
        Assert.Contains("cancel.Disabled && main.ObservedPendingCommandCount == 0", acceptance, StringComparison.Ordinal);
        Assert.Contains("geometry.WarpTriggers.Any(trigger => trigger.Available", acceptance, StringComparison.Ordinal);
    }

    [Fact]
    public void CooperativeAttackWaitsForPostReceiptSnapshotBeforeClassifyingMiss()
    {
        var acceptance = ReadSource("godot-client/Devilution.Client/Acceptance/SessionAcceptance.Cooperative.cs");
        Assert.Contains("var snapshotRevisionAtReceipt = main.ObservedSnapshotRevision;", acceptance, StringComparison.Ordinal);
        Assert.Contains("main.ObservedSnapshotRevision > snapshotRevisionAtReceipt", acceptance, StringComparison.Ordinal);
        Assert.Contains("snapshot.Tick >= receipt.AppliedTick", acceptance, StringComparison.Ordinal);
        Assert.Contains("snapshot.Projectiles.All(projectile => projectile.SourceEntityId != localEntityId", acceptance, StringComparison.Ordinal);
        Assert.Contains("return settled;", acceptance, StringComparison.Ordinal);
    }

    [Fact]
    public void CooperativeResumeRetainsObservedEncounterIdWithoutRequiringDeadActorPresence()
    {
        var acceptance = ReadSource("godot-client/Devilution.Client/Acceptance/SessionAcceptance.Cooperative.cs");
        Assert.Contains("var monsterId = previous is not null ? previous.MonsterId", acceptance, StringComparison.Ordinal);
        Assert.Contains("!snapshot.Monsters.Any(monster => monster.EntityId == previous.MonsterId && monster.Alive)", acceptance, StringComparison.Ordinal);
        Assert.Contains("snapshot.WorldItems.All(item => item.EntityId != previous.DropEntityId)", acceptance, StringComparison.Ordinal);
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "The extracted process reader requires Windows PowerShell.")]
    public void CooperativeProcessReaderRequiresHostHashesWithoutChangingLegacyProfiles()
    {
        const string script = """
            $ErrorActionPreference = 'Stop'
            $tokens = $null; $errors = $null
            $ast = [Management.Automation.Language.Parser]::ParseFile(__SOURCE_PATH__, [ref]$tokens, [ref]$errors)
            if ($errors.Count) { throw 'Driver source did not parse.' }
            $definitions = @($ast.FindAll({ param($node)
                $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Read-ModeProcessEvidence'
            }, $true))
            if ($definitions.Count -ne 1) { throw 'Ambiguous actual process reader.' }
            . ([scriptblock]::Create($definitions[0].Extent.Text))
            $sessionDirectory = Join-Path ([IO.Path]::GetTempPath()) ('godevilution-cooperative-process-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $sessionDirectory | Out-Null
            function New-Record {
                return [pscustomobject]@{
                    PersistenceMode = 'cohort'; HostPersistence = 'complete cohort'
                    HostProcessId = 100; HostStartedUtc = '2026-10-04T00:00:00Z'
                    Clients = @(@{ ProcessId = 200; StartedUtc = '2026-10-04T00:00:01Z' })
                    ContentHash = ('a' * 64); RulesetHash = ('b' * 64)
                }
            }
            function Write-Record([string]$Phase, $Record) {
                [IO.File]::WriteAllText((Join-Path $sessionDirectory ($Phase.ToLowerInvariant() + '-processes.json')),
                    ($Record | ConvertTo-Json -Depth 4))
            }
            $completed = $false
            try {
                $PersistenceMode = 'cohort'
                foreach ($phase in 'CooperativeJourney', 'CooperativeResume') {
                    Write-Record $phase (New-Record)
                    [void](Read-ModeProcessEvidence $phase)
                    foreach ($field in 'ContentHash', 'RulesetHash') {
                        foreach ($value in @($null, '', ('a' * 63), ('A' * 64), ('z' * 64), 123)) {
                            $record = New-Record; $record.$field = $value
                            Write-Record $phase $record
                            $rejected = $false
                            try { [void](Read-ModeProcessEvidence $phase) } catch { $rejected = $true }
                            if (-not $rejected) { throw "Invalid $phase $field accepted." }
                        }
                        $record = New-Record; $record.PSObject.Properties.Remove($field)
                        Write-Record $phase $record
                        $rejected = $false
                        try { [void](Read-ModeProcessEvidence $phase) } catch { $rejected = $true }
                        if (-not $rejected) { throw "Missing $phase $field accepted." }
                    }
                }
                foreach ($mode in 'cohort', 'legacy') {
                    $PersistenceMode = $mode
                    $record = New-Record
                    $record.PersistenceMode = $mode
                    $record.HostPersistence = if ($mode -eq 'cohort') { 'complete cohort' } else { 'legacy per-session' }
                    $record.PSObject.Properties.Remove('ContentHash'); $record.PSObject.Properties.Remove('RulesetHash')
                    Write-Record 'MultiplayerCreate' $record
                    [void](Read-ModeProcessEvidence 'MultiplayerCreate')
                }
                'COOPERATIVE-PROCESS-HASHES-PASSED'
                $completed = $true
            } finally {
                if ($completed) {
                    $absoluteRoot = [IO.Path]::GetFullPath($sessionDirectory)
                    $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
                    if ([IO.Path]::GetDirectoryName($absoluteRoot) -cne $tempParent -or
                        [IO.Path]::GetFileName($absoluteRoot) -cnotmatch '^godevilution-cooperative-process-[0-9a-f]{32}$' -or
                        ((Get-Item -LiteralPath $absoluteRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                        throw 'Refusing cleanup outside the owned regular process fixture.'
                    }
                    $files = @(Get-ChildItem -LiteralPath $absoluteRoot -Force)
                    foreach ($file in $files) {
                        if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
                            $file.Name -cnotin @('cooperativejourney-processes.json', 'cooperativeresume-processes.json', 'multiplayercreate-processes.json')) {
                            throw 'Unexpected process fixture file.'
                        }
                    }
                    foreach ($file in $files) { Remove-Item -LiteralPath $file.FullName -Force }
                    Remove-Item -LiteralPath $absoluteRoot -Force
                }
            }
            """;
        var source = Path.Combine(FindRepositoryRoot(), "tools", "test-godot-session.ps1");
        var output = RunPowerShell(script.Replace("__SOURCE_PATH__", QuotePowerShell(source), StringComparison.Ordinal));
        Assert.Contains("COOPERATIVE-PROCESS-HASHES-PASSED", output, StringComparison.Ordinal);
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "The extracted evidence helper requires Windows PowerShell.")]
    public void CooperativeEvidenceHelperAcceptsTwoClientContinuationEvidence()
    {
        var output = RunPowerShell(BuildCooperativeEvidenceHarness(string.Empty));
        Assert.Contains("COOPERATIVE-EVIDENCE-PASSED", output, StringComparison.Ordinal);
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "The extracted evidence helper requires Windows PowerShell.")]
    public void CooperativeEvidenceHelperRejectsMismatchedIdsDropOwnershipRecoveryTickAndReceipts()
    {
        var mutations = @"
foreach ($case in @(
    @{ Name = 'mismatched-id'; Action = { param($j1, $j2, $r1, $r2); $j2.PeerEntityId = 999 } },
    @{ Name = 'wrong-encounter'; Action = { param($j1, $j2, $r1, $r2); $j1.MonsterId = 8; $r1.MonsterId = 8 } },
    @{ Name = 'divergent-peer-position'; Action = { param($j1, $j2, $r1, $r2); $j1.PeerPositionX++; $r1.RestoredPeerX++; $r1.InitialPeerX++; $r1.PeerPositionX++ } },
    @{ Name = 'wrong-drop-seed'; Action = { param($j1, $j2, $r1, $r2); $j1.DropSeed = 6002 } },
    @{ Name = 'wrong-owner'; Action = { param($j1, $j2, $r1, $r2); $j1.OwnerEntityId = $j2.EntityId; $r1.OwnerEntityId = $j2.EntityId } },
    @{ Name = 'wrong-recovery-tick'; Action = { param($j1, $j2, $r1, $r2); $r1.PickupAppliedTick = $j1.PickupAppliedTick + 1 } },
    @{ Name = 'missing-receipt'; Action = { param($j1, $j2, $r1, $r2); $j1.PickupReceiptSequence = 0; $r1.PickupReceiptSequence = 0 } },
    @{ Name = 'missing-pickup-sequence'; Action = { param($j1, $j2, $r1, $r2); $j1.PickupSequence = 0; $r1.PickupSequence = 0 } },
    @{ Name = 'stale-resume-sequence'; Action = { param($j1, $j2, $r1, $r2); $r1.Sequence = $j1.Sequence } },
    @{ Name = 'stale-resume-receipt'; Action = { param($j1, $j2, $r1, $r2); $r1.ReceiptSequence = $j1.ReceiptSequence } },
    @{ Name = 'backward-resume-tick'; Action = { param($j1, $j2, $r1, $r2); $r1.AppliedTick = $j1.AppliedTick - 1 } },
    @{ Name = 'unchanged-resume-position'; Action = { param($j1, $j2, $r1, $r2); $r1.PositionX = $r1.RestoredX; $r1.PostMoveX = $r1.RestoredX } },
    @{ Name = 'wrong-move-position'; Action = { param($j1, $j2, $r1, $r2); $r1.PostMoveX++ } },
    @{ Name = 'wrong-visible-mode'; Action = { param($j1, $j2, $r1, $r2); $r1.Visible = $true; $r1.DrawObserved = $true } },
    @{ Name = 'non-boolean-pass'; Action = { param($j1, $j2, $r1, $r2); $j1.Passed = 'true' } }
)) {
    Expect-Rejected $case.Name $case.Action
}
";
        var output = RunPowerShell(BuildCooperativeEvidenceHarness(mutations));
        Assert.Contains("COOPERATIVE-EVIDENCE-CORRUPTION-CAUGHT", output, StringComparison.Ordinal);
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "The extracted canonical receipt helper requires Windows PowerShell.")]
    public void CooperativePickupRequiresTheExactAcceptedCanonicalOwnerReceipt()
    {
        const string script = """
            $ErrorActionPreference = 'Stop'
            $tokens = $null; $errors = $null
            $ast = [Management.Automation.Language.Parser]::ParseFile(__SOURCE_PATH__, [ref]$tokens, [ref]$errors)
            if ($errors.Count) { throw 'Launcher source did not parse.' }
            foreach ($name in @('Test-CohortJsonInteger', 'Assert-CohortRecoveryReceipt', 'Assert-CohortCooperativePickupReceipt')) {
                $definitions = @($ast.FindAll({ param($node)
                    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name
                }, $true))
                if ($definitions.Count -ne 1) { throw "Ambiguous actual helper: $name" }
                . ([scriptblock]::Create($definitions[0].Extent.Text))
            }
            function New-Participants {
                return ('[{"EntityId":101,"CommandReceipts":[{"ClientSequence":10,"ServerReceiptSequence":19,"AppliedTick":30,"Status":1,"RejectReason":0}]},' +
                    '{"EntityId":202,"CommandReceipts":[{"ClientSequence":10,"ServerReceiptSequence":20,"AppliedTick":30,"Status":1,"RejectReason":0}]}]') | ConvertFrom-Json
            }
            function New-Evidence {
                return '{"Passed":true,"OwnerEntityId":101,"PickupSequence":10,"PickupReceiptSequence":19,"PickupAppliedTick":30}' | ConvertFrom-Json
            }
            Assert-CohortCooperativePickupReceipt (New-Participants) (New-Evidence)
            foreach ($case in @(
                @{ Name = 'unknown owner'; Change = { param($p, $e) $e.OwnerEntityId = 999 } },
                @{ Name = 'wrong owner'; Change = { param($p, $e) $e.OwnerEntityId = 202 } },
                @{ Name = 'wrong command'; Change = { param($p, $e) $e.PickupSequence++ } },
                @{ Name = 'wrong receipt'; Change = { param($p, $e) $e.PickupReceiptSequence++ } },
                @{ Name = 'wrong tick'; Change = { param($p, $e) $e.PickupAppliedTick++ } },
                @{ Name = 'rejected'; Change = { param($p, $e) $p[0].CommandReceipts[0].Status = 2 } },
                @{ Name = 'reason'; Change = { param($p, $e) $p[0].CommandReceipts[0].RejectReason = 1 } },
                @{ Name = 'duplicate row'; Change = { param($p, $e) $p[0].CommandReceipts += $p[0].CommandReceipts[0] } },
                @{ Name = 'missing sequence'; Change = { param($p, $e) $e.PickupSequence = $null } }
            )) {
                $participants = New-Participants; $evidence = New-Evidence
                & $case.Change $participants $evidence
                $rejected = $false
                try { Assert-CohortCooperativePickupReceipt $participants $evidence } catch { $rejected = $true }
                if (-not $rejected) { throw "False canonical receipt acceptance: $($case.Name)" }
            }
            'EXACT-PICKUP-RECEIPT-PASSED'
            """;
        var source = Path.Combine(FindRepositoryRoot(), "godot-client", "run-local-client.ps1");
        var output = RunPowerShell(script.Replace("__SOURCE_PATH__", QuotePowerShell(source), StringComparison.Ordinal));
        Assert.Contains("EXACT-PICKUP-RECEIPT-PASSED", output, StringComparison.Ordinal);
    }

    public static bool IsWindows => OperatingSystem.IsWindows();

    private static string BuildCooperativeEvidenceHarness(string mutations)
    {
        var sourcePath = Path.Combine(FindRepositoryRoot(), "tools", "test-godot-session.ps1");
        const string template = """
$ErrorActionPreference = 'Stop'
$sourcePath = __SOURCE_PATH__
$sourceText = [IO.File]::ReadAllText($sourcePath)
$tokens = $null
$parseErrors = $null
$sourceAst = [System.Management.Automation.Language.Parser]::ParseInput($sourceText, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }
$functionAst = $sourceAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-CooperativeEvidence' }, $true) | Select-Object -First 1
if ($null -eq $functionAst) { throw 'Could not extract Assert-CooperativeEvidence.' }
. ([scriptblock]::Create($functionAst.Extent.Text))

$root = Join-Path ([IO.Path]::GetTempPath()) ('godevilution-cooperative-evidence-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $root 'player-1') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $root 'player-2') -Force | Out-Null

function Copy-Document($Document) {
    return ($Document | ConvertTo-Json -Depth 8 | ConvertFrom-Json)
}

function New-Document([int]$Slot) {
    $entityId = if ($Slot -eq 1) { 101 } else { 202 }
    $peerEntityId = if ($Slot -eq 1) { 202 } else { 101 }
    $x = if ($Slot -eq 1) { 10 } else { 13 }
    $peerX = if ($Slot -eq 1) { 13 } else { 10 }
    return [pscustomobject]@{
        Passed = $true; Mode = 'Multiplayer'; ContentHash = 'CONTENT'; RulesetHash = 'RULES'; Slot = $Slot
        EntityId = $entityId; PeerEntityId = $peerEntityId; LevelId = 1; PositionX = $x; PositionY = 10
        PeerPositionX = $peerX; PeerPositionY = 10; MonsterId = 9; MonsterDefeated = $true
        DropEntityId = 1000009; DropSeed = 6001; OwnerEntityId = 101; OwnerInventoryCount = 1
        PeerInventoryCount = 0; DropPresent = $false; Sequence = 15; ReceiptSequence = 20 + $Slot; AppliedTick = 33
        ReceiptStatus = 'Accepted'; PostMoveX = $x; PostMoveY = 10
        PickupSequence = 10; PickupReceiptSequence = 19; PickupAppliedTick = 30; PickupStatus = 'Accepted'; Visible = $false
        DrawObserved = $false; InitialX = $x - 1; InitialY = 10; InitialPeerX = $peerX - 1; InitialPeerY = 10
        RestoredEntityId = 0; RestoredPeerEntityId = 0; RestoredX = 0; RestoredY = 0
        RestoredPeerX = 0; RestoredPeerY = 0
    }
}

function New-Resume($Journey) {
    $resume = Copy-Document $Journey
    $resume.RestoredEntityId = $Journey.EntityId
    $resume.RestoredPeerEntityId = $Journey.PeerEntityId
    $resume.RestoredX = $Journey.PositionX; $resume.RestoredY = $Journey.PositionY
    $resume.RestoredPeerX = $Journey.PeerPositionX; $resume.RestoredPeerY = $Journey.PeerPositionY
    $resume.InitialX = $Journey.PositionX; $resume.InitialY = $Journey.PositionY
    $resume.InitialPeerX = $Journey.PeerPositionX; $resume.InitialPeerY = $Journey.PeerPositionY
    $resume.PositionX++; $resume.PeerPositionX++; $resume.PostMoveX = $resume.PositionX
    $resume.Sequence++; $resume.ReceiptSequence += 10; $resume.AppliedTick += 10
    return $resume
}

function Write-Documents($Journey1, $Journey2, $Resume1, $Resume2) {
    [IO.File]::WriteAllText((Join-Path $root 'player-1/cooperativejourney.json'), ($Journey1 | ConvertTo-Json -Depth 8))
    [IO.File]::WriteAllText((Join-Path $root 'player-2/cooperativejourney.json'), ($Journey2 | ConvertTo-Json -Depth 8))
    [IO.File]::WriteAllText((Join-Path $root 'player-1/cooperativeresume.json'), ($Resume1 | ConvertTo-Json -Depth 8))
    [IO.File]::WriteAllText((Join-Path $root 'player-2/cooperativeresume.json'), ($Resume2 | ConvertTo-Json -Depth 8))
}

function Expect-Rejected([string]$Name, [scriptblock]$Mutation) {
    $journey1 = New-Document 1
    $journey2 = New-Document 2
    $resume1 = New-Resume $journey1
    $resume2 = New-Resume $journey2
    & $Mutation $journey1 $journey2 $resume1 $resume2
    Write-Documents $journey1 $journey2 $resume1 $resume2
    $rejected = $false
    try { Assert-CooperativeEvidence $root 'CONTENT' 'RULES' } catch { $rejected = $true }
    if (-not $rejected) { throw "Cooperative evidence helper accepted corruption: $Name" }
}

$completed = $false
try {
    $journey1 = New-Document 1
    $journey2 = New-Document 2
    $resume1 = New-Resume $journey1
    $resume2 = New-Resume $journey2
    Write-Documents $journey1 $journey2 $resume1 $resume2
    Assert-CooperativeEvidence $root 'CONTENT' 'RULES'
    Write-Output 'COOPERATIVE-EVIDENCE-PASSED'
    __MUTATIONS__
    Write-Output 'COOPERATIVE-EVIDENCE-CORRUPTION-CAUGHT'
    $completed = $true
} finally {
    if ($completed -and (Test-Path -LiteralPath $root)) {
        $absoluteRoot = [IO.Path]::GetFullPath($root)
        $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        if ([IO.Path]::GetDirectoryName($absoluteRoot) -cne $tempParent -or
            [IO.Path]::GetFileName($absoluteRoot) -cnotmatch '^godevilution-cooperative-evidence-[0-9a-f]{32}$' -or
            ((Get-Item -LiteralPath $absoluteRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Refusing cleanup outside the owned regular fixture directory.'
        }
        foreach ($player in Get-ChildItem -LiteralPath $absoluteRoot -Force) {
            if (-not $player.PSIsContainer -or $player.Name -cnotin @('player-1', 'player-2') -or
                ($player.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unexpected fixture child.' }
            foreach ($file in Get-ChildItem -LiteralPath $player.FullName -Force) {
                if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
                    $file.Name -cnotin @('cooperativejourney.json', 'cooperativeresume.json')) { throw 'Unexpected fixture file.' }
            }
        }
        Remove-Item -LiteralPath $absoluteRoot -Recurse -Force
    }
}
""";
        return template.Replace("__SOURCE_PATH__", QuotePowerShell(sourcePath), StringComparison.Ordinal)
            .Replace("__MUTATIONS__", mutations.Trim(), StringComparison.Ordinal);
    }

    private static string RunPowerShell(string script)
    {
        var path = Path.Combine(Path.GetTempPath(), $"godevilution-cooperative-helper-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(path, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var completed = false;
        try {
            using var process = Process.Start(new ProcessStartInfo {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{path}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException("Could not start Windows PowerShell.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000)) {
                process.Kill(entireProcessTree: true);
                Assert.True(process.WaitForExit(5_000), $"Owned helper {process.Id} did not terminate; retain {path}.");
                Assert.Fail("The cooperative evidence PowerShell helper exceeded its bounded deadline.");
            }
            Assert.True(Task.WhenAll(outputTask, errorTask).Wait(TimeSpan.FromSeconds(5)),
                $"Owned helper output did not complete; retain {path}.");
            var output = outputTask.GetAwaiter().GetResult() + errorTask.GetAwaiter().GetResult();
            Assert.True(process.ExitCode == 0,
                $"The cooperative evidence PowerShell helper failed ({process.ExitCode}):{Environment.NewLine}{output}");
            completed = true;
            return output;
        } finally {
            if (completed && File.Exists(path))
                File.Delete(path);
        }
    }

    private static string QuotePowerShell(string value)
        => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string FindRepositoryRoot()
    {
        var configured = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        foreach (var start in new[] { configured, Directory.GetCurrentDirectory(), AppContext.BaseDirectory }) {
            if (string.IsNullOrWhiteSpace(start))
                continue;
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }

    private static string ReadSource(string relativePath)
    {
        var configured = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        foreach (var start in new[] { configured, Directory.GetCurrentDirectory(), AppContext.BaseDirectory }) {
            if (string.IsNullOrWhiteSpace(start))
                continue;
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }
}
