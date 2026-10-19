using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;
using Xunit.Sdk;

namespace Devilution.Server.Tests;

/** Fixture-only coverage for the explicit cohort launcher boundary. */
public sealed class CohortLauncherBehaviorTests
{
    private static readonly ConcurrentDictionary<string, byte> RetainedFixtureRoots = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] CohortCheckpointFunctions = [
        "Get-Sha256Hex",
        "Assert-CohortJsonProperties",
        "Test-CohortJsonInteger",
        "Assert-CohortCheckpoint",
    ];

    private static readonly string[] DriverProcessEvidenceFunctions = [
        "Read-ModeProcessEvidence",
        "Assert-FreshProcessEvidence",
        "Assert-RecoveryProcessEvidence",
    ];

    private const string ContentHash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string RulesetHash =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void DriverCarriesPersistenceModeAndFreshProcessEvidenceContract()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var driver = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "test-godot-session.ps1"));
        Assert.Contains("[ValidateSet('legacy', 'cohort')] [string]$PersistenceMode = 'legacy'", driver, StringComparison.Ordinal);
        Assert.Contains("PersistenceMode = $PersistenceMode", driver, StringComparison.Ordinal);
        Assert.Contains("Godevilution-Scene-Acceptance-' + [guid]::NewGuid().ToString('N')", driver, StringComparison.Ordinal);
        Assert.Contains("Read-ModeProcessEvidence 'Create'", driver, StringComparison.Ordinal);
        Assert.Contains("Read-ModeProcessEvidence 'Resume'", driver, StringComparison.Ordinal);
        Assert.Contains("Read-ModeProcessEvidence 'Recovery'", driver, StringComparison.Ordinal);
        Assert.Contains("Assert-RecoveryProcessEvidence", driver, StringComparison.Ordinal);

        var launcher = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "godot-client", "run-local-client.ps1"));
        Assert.Contains("GODEVILUTION_PERSISTENCE_MODE", launcher, StringComparison.Ordinal);
        Assert.Contains("Assert-CohortCheckpoint", launcher, StringComparison.Ordinal);
        Assert.Contains("HostProcessId", launcher, StringComparison.Ordinal);
        Assert.Contains("HostStartedUtc", launcher, StringComparison.Ordinal);
        Assert.Contains("ProcessId", launcher, StringComparison.Ordinal);
        Assert.Contains("StartedUtc", launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void DriverProcessEvidenceHelpersRejectWrongPhaseIdentity()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = NewRoot("Godevilution-Cohort-Driver-Evidence-");
        Directory.CreateDirectory(root);
        try {
            var body = new StringBuilder()
                .AppendLine("function Expect-Rejected([scriptblock]$Action) {")
                .AppendLine("    $rejected = $false")
                .AppendLine("    try { & $Action | Out-Null } catch { $rejected = $true }")
                .AppendLine("    if (-not $rejected) { throw 'An invalid process identity was accepted.' }")
                .AppendLine("}")
                .AppendLine("$created = [pscustomobject]@{ HostProcessId = 100; HostStartedUtc = 'create-host'; Clients = @([pscustomobject]@{ ProcessId = 200; StartedUtc = 'create-client' }) }")
                .AppendLine("$resumed = [pscustomobject]@{ HostProcessId = 101; HostStartedUtc = 'resume-host'; Clients = @([pscustomobject]@{ ProcessId = 201; StartedUtc = 'resume-client' }) }")
                .AppendLine("Assert-FreshProcessEvidence $created $resumed 1")
                .AppendLine("$bad = $resumed | ConvertTo-Json -Depth 8 | ConvertFrom-Json")
                .AppendLine("$bad.HostProcessId = 100; $bad.HostStartedUtc = 'create-host'")
                .AppendLine("Expect-Rejected { Assert-FreshProcessEvidence $created $bad 1 }")
                .AppendLine("$bad = $resumed | ConvertTo-Json -Depth 8 | ConvertFrom-Json")
                .AppendLine("$bad.Clients[0].ProcessId = 200; $bad.Clients[0].StartedUtc = 'create-client'")
                .AppendLine("Expect-Rejected { Assert-FreshProcessEvidence $created $bad 1 }")
                .AppendLine("$recovery = [pscustomobject]@{")
                .AppendLine("    PersistenceMode = 'cohort'; HostPersistence = 'complete cohort'")
                .AppendLine("    HostProcessId = 300; HostStartedUtc = '2026-10-04T12:00:00Z'; Port = 6112")
                .AppendLine("    Clients = @([pscustomobject]@{ ProcessId = 400; StartedUtc = 'recovery-client' })")
                .AppendLine("    StoppedProcessId = 300; StoppedHostStartedUtc = '2026-10-04T12:00:00Z'")
                .AppendLine("    StoppedHostEndedUtc = '2026-10-04T12:00:01Z'; StoppedHostExitCode = -1")
                .AppendLine("    RestartedProcessId = 301; RestartedHostStartedUtc = '2026-10-04T12:00:02Z'")
                .AppendLine("    GodotProcessId = 400; GodotStartedUtc = 'recovery-client'")
                .AppendLine("    ExistingClients = @([pscustomobject]@{ ProcessId = 400; StartedUtc = 'recovery-client' })").AppendLine("}")
                .AppendLine("$marker = [pscustomobject]@{ Stopped = $true; ProcessId = 300; StartedUtc = $recovery.StoppedHostStartedUtc; EndedUtc = $recovery.StoppedHostEndedUtc; ExitCode = -1; Port = 6112 }")
                .AppendLine("Assert-RecoveryProcessEvidence $recovery $marker")
                .AppendLine("$bad = $recovery | ConvertTo-Json -Depth 8 | ConvertFrom-Json")
                .AppendLine("$bad.RestartedProcessId = 300; $bad.RestartedHostStartedUtc = $recovery.StoppedHostStartedUtc")
                .AppendLine("Expect-Rejected { Assert-RecoveryProcessEvidence $bad $marker }")
                .AppendLine("$bad = $recovery | ConvertTo-Json -Depth 8 | ConvertFrom-Json")
                .AppendLine("$bad.GodotProcessId = 401")
                .AppendLine("Expect-Rejected { Assert-RecoveryProcessEvidence $bad $marker }")
                .AppendLine("$bad = $recovery | ConvertTo-Json -Depth 8 | ConvertFrom-Json")
                .AppendLine("$bad.GodotStartedUtc = 'other-client'")
                .AppendLine("Expect-Rejected { Assert-RecoveryProcessEvidence $bad $marker }")
                .AppendLine("$bad = $recovery | ConvertTo-Json -Depth 8 | ConvertFrom-Json")
                .AppendLine("$bad.ExistingClients[0].StartedUtc = 'other-client'")
                .AppendLine("Expect-Rejected { Assert-RecoveryProcessEvidence $bad $marker }")
                .AppendLine("$sessionDirectory = " + QuotePowerShell(root))
                .AppendLine("$PersistenceMode = 'cohort'")
                .AppendLine("$recovery | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $sessionDirectory 'recovery-processes.json') -Encoding UTF8")
                .AppendLine("Read-ModeProcessEvidence 'Recovery' | Out-Null")
                .AppendLine("$wrongMode = $recovery | ConvertTo-Json -Depth 8 | ConvertFrom-Json")
                .AppendLine("$wrongMode.PersistenceMode = 'legacy'")
                .AppendLine("$wrongMode | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $sessionDirectory 'recovery-processes.json') -Encoding UTF8")
                .AppendLine("Expect-Rejected { Read-ModeProcessEvidence 'Recovery' | Out-Null }")
                .AppendLine("'PASS'")
                .ToString();
            Assert.Contains(
                "PASS",
                RunPowerShell(BuildDriverFunctionHarness(DriverProcessEvidenceFunctions, body), root),
                StringComparison.Ordinal);
        } finally {
            DeleteOwnedTempDirectory(root, "Godevilution-Cohort-Driver-Evidence-");
        }
    }

    [Fact]
    public void RecoveryProcessEvidenceRejectsUnconfirmedOrMismatchedTerminalExit()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var body = """
            $evidenceJson = '{"HostProcessId":300,"HostStartedUtc":"2026-10-04T12:00:00Z",' +
                '"StoppedProcessId":300,"StoppedHostStartedUtc":"2026-10-04T12:00:00Z",' +
                '"StoppedHostEndedUtc":"2026-10-04T12:00:01Z","StoppedHostExitCode":-1,' +
                '"RestartedProcessId":301,"RestartedHostStartedUtc":"2026-10-04T12:00:02Z",' +
                '"GodotProcessId":400,"GodotStartedUtc":"client-start", "Port":6112,' +
                '"Clients":[{"ProcessId":400,"StartedUtc":"client-start"}],' +
                '"ExistingClients":[{"ProcessId":400,"StartedUtc":"client-start"}]}'
            $markerJson = '{"Stopped":true,"ProcessId":300,"StartedUtc":"2026-10-04T12:00:00Z",' +
                '"EndedUtc":"2026-10-04T12:00:01Z","ExitCode":-1,"Port":6112}'
            foreach ($code in @(-1, 0, 23, [int]::MinValue, [int]::MaxValue)) {
                $e = $evidenceJson | ConvertFrom-Json; $m = $markerJson | ConvertFrom-Json
                $e.StoppedHostExitCode = $code; $m.ExitCode = $code
                Assert-RecoveryProcessEvidence $e $m
            }
            $changes = @(
                { param($e, $m) $e.StoppedHostExitCode = $null },
                { param($e, $m) $e.PSObject.Properties.Remove('StoppedHostExitCode') },
                { param($e, $m) $e.StoppedHostExitCode = '-1' },
                { param($e, $m) $e.StoppedHostExitCode = -1.0 },
                { param($e, $m) $e.StoppedHostExitCode = $true },
                { param($e, $m) $e.StoppedHostExitCode = [long]::MaxValue },
                { param($e, $m) $e.StoppedHostEndedUtc = $null },
                { param($e, $m) $e.StoppedHostEndedUtc = 'bad-date' },
                { param($e, $m) $e.StoppedHostEndedUtc = '2026-10-04T11:59:59Z'; $m.EndedUtc = $e.StoppedHostEndedUtc },
                { param($e, $m) $e.RestartedHostStartedUtc = '2026-10-04T12:00:00Z' },
                { param($e, $m) $e.StoppedProcessId = '300' },
                { param($e, $m) $e.RestartedProcessId = 0 },
                { param($e, $m) $e.HostProcessId = 302 },
                { param($e, $m) $e.HostStartedUtc = 'other-host' },
                { param($e, $m) $e.Port = 0 },
                { param($e, $m) $m.Stopped = $false },
                { param($e, $m) $m.Stopped = 'true' },
                { param($e, $m) $m.ProcessId = 302 },
                { param($e, $m) $m.ProcessId = '300' },
                { param($e, $m) $m.StartedUtc = 'other-host' },
                { param($e, $m) $m.EndedUtc = '2026-10-04T12:00:02Z' },
                { param($e, $m) $m.ExitCode = 0 },
                { param($e, $m) $m.ExitCode = $null },
                { param($e, $m) $m.ExitCode = '-1' },
                { param($e, $m) $m.Port = 6113 },
                { param($e, $m) $m.Port = '6112' }
            )
            $case = 0
            foreach ($change in $changes) {
                $e = $evidenceJson | ConvertFrom-Json; $m = $markerJson | ConvertFrom-Json
                & $change $e $m
                $rejected = $false
                try { Assert-RecoveryProcessEvidence $e $m } catch { $rejected = $true }
                if (-not $rejected) { throw "Invalid terminal evidence case $case was accepted." }
                $case++
            }
            $rejected = $false
            try { Assert-RecoveryProcessEvidence ($evidenceJson | ConvertFrom-Json) $null } catch { $rejected = $true }
            if (-not $rejected) { throw 'Missing stopped-host marker was accepted.' }
            'PASS'
            """;
        Assert.Contains("PASS", RunPowerShell(BuildDriverFunctionHarness(
            ["Assert-RecoveryProcessEvidence"], body)), StringComparison.Ordinal);
    }

    [Fact]
    public void SessionMenuEvidenceRequiresTypedRefusalCleanupIdentityAndExactSequenceProof()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = NewRoot("Godevilution-Session-Menu-Evidence-");
        Directory.CreateDirectory(root);
        try {
            var body = """
                function Save-Evidence($menu, $terminal, $events, $exit) {
                    # The expected phase is independent of a tampered menu label.
                    $prefix = $phase.ToLowerInvariant()
                    [IO.File]::WriteAllText((Join-Path $work ($prefix + '-menu-return.json')), ($menu | ConvertTo-Json -Depth 5))
                    [IO.File]::WriteAllText((Join-Path $work ($prefix + '.json')), ($terminal | ConvertTo-Json -Depth 5))
                    [IO.File]::WriteAllText((Join-Path $work ($prefix + '-menu-events.json')), ($events | ConvertTo-Json -Depth 5))
                    [IO.File]::WriteAllText((Join-Path $work ($prefix + '-menu-exit.json')), ($exit | ConvertTo-Json -Depth 5))
                }
                function Expect-Rejected($menu, $terminal, $events, $exit, [string]$case) {
                    Save-Evidence $menu $terminal $events $exit
                    $rejected = $false
                    try { Assert-SessionMenuEvidence $work $phase | Out-Null } catch { $rejected = $true }
                    if (-not $rejected) {
                        if ([string]::IsNullOrWhiteSpace($case)) {
                            $case = 'unlabeled fixture case'
                        }
                        throw "Invalid session-menu evidence was accepted for phase '$phase' ($case)."
                    }
                }
                foreach ($phase in @('Create', 'Resume')) {
                    $creating = $phase -eq 'Create'
                    $menu = [pscustomobject]@{
                        Passed = $true; Phase = $phase; Mode = 'SinglePlayer'
                        ContentHash = ('a' * 64); RulesetHash = ('b' * 64)
                        EntityId = 2; ClassId = 1; LevelId = 0
                        ReturnedToSetup = $true; BookmarkPreserved = $true; OldWorldCleared = $true
                        Rejoined = $true; ResumeDefaultsPreserved = $true; SettledReturnConfirmed = $true
                        ConnectCancellationConfirmed = $true; RemoteDefaultsPreserved = $true
                        LocalConfirmedTokenPreserved = $true; LocalTokenDurableWhenWritable = $true
                        InMemoryTokenWarningWhenBlocked = $true
                        PendingReturnRejected = $creating; BookmarkReturnRejected = $creating
                        PendingCountAtRefusal = $(if ($creating) { 1 } else { 0 })
                        InitialSequence = 3; FinalSequence = 4
                        InitialX = 0; InitialY = 1; RejoinedX = 0; RejoinedY = 1; FinalX = 0; FinalY = 2
                        InitialGeneration = 1; ReturnedGeneration = 2; RejoinedGeneration = 3
                        InitialModelGeneration = 0; RejoinedModelGeneration = 1
                    }
                    $terminal = [pscustomobject]@{
                        Passed = $true; Mode = 'SinglePlayer'; ContentHash = ('a' * 64); RulesetHash = ('b' * 64)
                        EntityId = 2; ClassId = 1; LevelId = 0; Sequence = 8; X = 0; Y = 3
                    }
                    $events = [pscustomobject]@{
                        Passed = $true; Phase = $phase; ContentHash = ('a' * 64); RulesetHash = ('b' * 64)
                        EntityId = 2; InitialSequence = 3; InitialX = 0; InitialY = 1
                        PriorMenuReturnSequence = 4; PriorMenuReturnX = 0; PriorMenuReturnY = 2
                        InitialGeneration = [long]1; InitialModelGeneration = 0
                        FirstApproachInitialSequence = 4; FirstApproachFinalSequence = 4; FirstApproachAccepted = $true
                        FirstClearGeneration = [long]3; FirstClearModelGeneration = 1
                        FirstClearDialogueSequence = 5; FirstPostClearDialogueSequence = 6
                        SecondReturnedGeneration = [long]4; SecondReturnedModelGeneration = 2
                        SecondRejoinedGeneration = [long]5; SecondRejoinedModelGeneration = 2
                        SecondModelDialogueSequence = 7; SecondClearGeneration = [long]5; SecondClearModelGeneration = 2
                        FinalMovementSequence = 8
                        FirstClearEventBatchCount = 1; FirstClearEventCount = 2; FirstClearEventKind = 'Dialogue'
                        FirstClearButtonEnabled = $true; FirstClearButtonClicked = $true; FirstClearRemainingCount = 0
                        FirstClearStartSequence = 10; FirstClearEndSequence = 12
                        FirstClearRemainingStartSequence = 12; FirstClearRemainingEndSequence = 12
                        FirstPostClearEventBatchCount = 2; FirstPostClearEventBatchDelta = 1
                        FirstPostClearEventCount = 1; FirstPostClearEventKind = 'Dialogue'
                        SecondClearEventBatchCount = 1; SecondClearEventBatchDelta = 1
                        SecondClearEventCount = 1; SecondClearEventKind = 'Dialogue'
                        SecondClearButtonEnabled = $true; SecondClearButtonClicked = $true; SecondClearRemainingCount = 0
                        SecondClearStartSequence = 20; SecondClearEndSequence = 21
                        SecondClearRemainingStartSequence = 21; SecondClearRemainingEndSequence = 21
                        FinalCommandSequence = 8; FinalX = 0; FinalY = 3
                        BookmarkBytesPreserved = $true; IdentityPreserved = $true
                        FinalMovementAccepted = $true
                    }
                    $exit = [pscustomobject]@{
                        Passed = $true; Phase = $phase; ExitDuringHandshakeVerified = $true
                        HandshakeExitPendingObserved = $true; HandshakeExitMainFreed = $true
                        HandshakeExitPeerClosed = $true; HandshakeExitCleanupCompleted = $true
                        HandshakeExitCleanupFailureCount = 0; HandshakeExitReturnCleanupJoined = $true
                        HandshakeExitPostExitMenuPublicationAttemptCount = [long]0
                        HandshakeExitNoPostExitMenuPublication = $true
                        ExitDuringReturnVerified = $true; ReturnExitPendingObserved = $true
                        ReturnExitCleanupGateObserved = $true; ReturnExitStillPendingWhileGated = $true
                        ReturnExitMainFreed = $true; ReturnExitPeerClosed = $true
                        ReturnExitCleanupCompleted = $true; ReturnExitCleanupFailureCount = 0
                        ReturnExitReturnCleanupJoined = $true
                        ReturnExitPostExitMenuPublicationAttemptCount = [long]0
                        ReturnExitNoPostExitMenuPublication = $true
                    }
                    Save-Evidence $menu $terminal $events $exit
                    $accepted = Assert-SessionMenuEvidence $work $phase
                    $menuJson = $menu | ConvertTo-Json -Depth 5
                    $terminalJson = $terminal | ConvertTo-Json -Depth 5
                    $eventsJson = $events | ConvertTo-Json -Depth 5
                    $exitJson = $exit | ConvertTo-Json -Depth 5
                    if ($accepted.Events.FinalCommandSequence -ne $terminal.Sequence -or
                        $accepted.Events.PriorMenuReturnSequence -ne $menu.FinalSequence -or
                        $accepted.Events.FinalX -ne $terminal.X -or $accepted.Events.FinalY -ne $terminal.Y) {
                        throw 'Valid supplementary evidence did not bind the menu and terminal sequence endpoints.'
                    }
                    $zeroBasedEvents = $eventsJson | ConvertFrom-Json
                    $zeroBasedEvents.FirstClearStartSequence = 0
                    $zeroBasedEvents.FirstClearEndSequence = 2
                    $zeroBasedEvents.FirstClearRemainingStartSequence = 2
                    $zeroBasedEvents.FirstClearRemainingEndSequence = 2
                    $zeroBasedEvents.SecondClearStartSequence = 0
                    $zeroBasedEvents.SecondClearEndSequence = 1
                    $zeroBasedEvents.SecondClearRemainingStartSequence = 1
                    $zeroBasedEvents.SecondClearRemainingEndSequence = 1
                    Save-Evidence $menu $terminal $zeroBasedEvents $exit
                    $zeroBasedAccepted = Assert-SessionMenuEvidence $work $phase
                    if ($zeroBasedAccepted.Events.FirstClearStartSequence -ne 0 -or
                        $zeroBasedAccepted.Events.FirstClearEndSequence -ne 2 -or
                        $zeroBasedAccepted.Events.FirstClearEndSequence - $zeroBasedAccepted.Events.FirstClearStartSequence -ne $zeroBasedAccepted.Events.FirstClearEventCount -or
                        $zeroBasedAccepted.Events.FirstClearRemainingStartSequence -ne 2 -or
                        $zeroBasedAccepted.Events.FirstClearRemainingEndSequence -ne 2 -or
                        $zeroBasedAccepted.Events.SecondClearStartSequence -ne 0 -or
                        $zeroBasedAccepted.Events.SecondClearEndSequence -ne 1 -or
                        $zeroBasedAccepted.Events.SecondClearEndSequence - $zeroBasedAccepted.Events.SecondClearStartSequence -ne $zeroBasedAccepted.Events.SecondClearEventCount -or
                        $zeroBasedAccepted.Events.SecondClearRemainingStartSequence -ne 1 -or
                        $zeroBasedAccepted.Events.SecondClearRemainingEndSequence -ne 1) {
                        throw "Valid zero-based event-history boundaries were not preserved for phase '$phase'."
                    }
                    Save-Evidence $menu $terminal $events $exit
                    foreach ($flag in @('Passed', 'FirstClearButtonEnabled', 'FirstClearButtonClicked',
                        'SecondClearButtonEnabled', 'SecondClearButtonClicked', 'BookmarkBytesPreserved',
                        'IdentityPreserved', 'FirstApproachAccepted', 'FinalMovementAccepted')) {
                        foreach ($invalid in @($false, 'true', $null)) {
                            $bad = $eventsJson | ConvertFrom-Json
                            $bad.$flag = $invalid
                            Expect-Rejected $menu $terminal $bad $exit "event flag $flag with coerced/false value"
                        }
                        $bad = $eventsJson | ConvertFrom-Json
                        $bad.PSObject.Properties.Remove($flag)
                        Expect-Rejected $menu $terminal $bad $exit "missing event flag $flag"
                    }
                    foreach ($flag in @('Passed', 'ExitDuringHandshakeVerified', 'HandshakeExitPendingObserved',
                        'HandshakeExitMainFreed', 'HandshakeExitPeerClosed', 'HandshakeExitCleanupCompleted',
                        'HandshakeExitReturnCleanupJoined', 'HandshakeExitNoPostExitMenuPublication',
                        'ExitDuringReturnVerified', 'ReturnExitPendingObserved', 'ReturnExitCleanupGateObserved',
                        'ReturnExitStillPendingWhileGated', 'ReturnExitMainFreed', 'ReturnExitPeerClosed',
                        'ReturnExitCleanupCompleted', 'ReturnExitReturnCleanupJoined',
                        'ReturnExitNoPostExitMenuPublication')) {
                        foreach ($invalid in @($false, 'true', $null)) {
                            $bad = $exitJson | ConvertFrom-Json
                            $bad.$flag = $invalid
                            Expect-Rejected $menu $terminal $events $bad "exit flag $flag with coerced/false value"
                        }
                        $bad = $exitJson | ConvertFrom-Json
                        $bad.PSObject.Properties.Remove($flag)
                        Expect-Rejected $menu $terminal $events $bad "missing exit flag $flag"
                    }
                    foreach ($field in @('InitialSequence', 'InitialX', 'InitialY', 'PriorMenuReturnSequence',
                        'PriorMenuReturnX', 'PriorMenuReturnY', 'EntityId', 'InitialGeneration',
                        'InitialModelGeneration', 'FirstApproachInitialSequence', 'FirstApproachFinalSequence',
                        'FirstClearGeneration', 'FirstClearModelGeneration', 'FirstClearDialogueSequence',
                        'FirstPostClearDialogueSequence', 'SecondReturnedGeneration', 'SecondReturnedModelGeneration',
                        'SecondRejoinedGeneration', 'SecondRejoinedModelGeneration',
                        'SecondModelDialogueSequence', 'FinalMovementSequence',
                        'FirstClearEventBatchCount', 'FirstClearEventCount', 'FirstClearRemainingCount',
                        'FirstClearStartSequence', 'FirstClearEndSequence',
                        'FirstClearRemainingStartSequence', 'FirstClearRemainingEndSequence',
                        'FirstPostClearEventBatchCount', 'FirstPostClearEventBatchDelta',
                        'FirstPostClearEventCount', 'SecondClearEventBatchCount', 'SecondClearEventCount',
                        'SecondClearEventBatchDelta', 'SecondClearGeneration', 'SecondClearModelGeneration',
                        'SecondClearRemainingCount',
                        'SecondClearStartSequence', 'SecondClearEndSequence',
                        'SecondClearRemainingStartSequence', 'SecondClearRemainingEndSequence',
                        'FinalCommandSequence', 'FinalX', 'FinalY')) {
                        foreach ($invalid in @('1', $true, $null, 1.5)) {
                            $bad = $eventsJson | ConvertFrom-Json
                            $bad.$field = $invalid
                            Expect-Rejected $menu $terminal $bad $exit "event numeric field $field with coerced value"
                        }
                        $bad = $eventsJson | ConvertFrom-Json
                        $bad.PSObject.Properties.Remove($field)
                        Expect-Rejected $menu $terminal $bad $exit "missing event numeric field $field"
                    }
                    foreach ($field in @('Phase', 'ContentHash', 'RulesetHash', 'FirstClearEventKind',
                        'FirstPostClearEventKind', 'SecondClearEventKind')) {
                        $bad = $eventsJson | ConvertFrom-Json
                        $bad.$field = $null
                        Expect-Rejected $menu $terminal $bad $exit "event string field $field with null value"
                        $bad = $eventsJson | ConvertFrom-Json
                        $bad.$field = 1
                        Expect-Rejected $menu $terminal $bad $exit "event string field $field with wrong type"
                        $bad = $eventsJson | ConvertFrom-Json
                        $bad.PSObject.Properties.Remove($field)
                        Expect-Rejected $menu $terminal $bad $exit "missing event string field $field"
                    }
                    foreach ($field in @('Passed', 'Phase')) {
                        $bad = $exitJson | ConvertFrom-Json
                        if ($field -eq 'Passed') { $bad.$field = $false } else { $bad.$field = 'Recovery' }
                        Expect-Rejected $menu $terminal $events $bad "exit field $field with inconsistent value"
                        $bad = $exitJson | ConvertFrom-Json
                        $bad.PSObject.Properties.Remove($field)
                        Expect-Rejected $menu $terminal $events $bad "missing exit field $field"
                    }
                    foreach ($field in @('HandshakeExitCleanupFailureCount',
                        'HandshakeExitPostExitMenuPublicationAttemptCount',
                        'ReturnExitCleanupFailureCount', 'ReturnExitPostExitMenuPublicationAttemptCount')) {
                        foreach ($invalid in @('0', $true, $null, 0.5)) {
                            $bad = $exitJson | ConvertFrom-Json
                            $bad.$field = $invalid
                            Expect-Rejected $menu $terminal $events $bad "exit numeric field $field with coerced value"
                        }
                        $bad = $exitJson | ConvertFrom-Json
                        $bad.PSObject.Properties.Remove($field)
                        Expect-Rejected $menu $terminal $events $bad "missing exit numeric field $field"
                    }
                    foreach ($missing in @('menu-events', 'menu-exit')) {
                        Save-Evidence $menu $terminal $events $exit
                        $missingPath = Join-Path $work ($phase.ToLowerInvariant() + '-' + $missing + '.json')
                        if (-not (Test-Path -LiteralPath $missingPath -PathType Leaf)) {
                            throw "Missing-artifact fixture did not write $missing evidence before deletion."
                        }
                        [IO.File]::Delete($missingPath)
                        if (Test-Path -LiteralPath $missingPath) {
                            throw "Missing-artifact fixture failed to delete $missing evidence."
                        }
                        $rejected = $false
                        try { Assert-SessionMenuEvidence $work $phase | Out-Null } catch { $rejected = $true }
                        if (-not $rejected) { throw "Missing $missing evidence was accepted for phase '$phase'." }
                    }
                    foreach ($flag in @('Passed', 'ReturnedToSetup', 'BookmarkPreserved', 'OldWorldCleared',
                        'Rejoined', 'ResumeDefaultsPreserved', 'SettledReturnConfirmed',
                        'ConnectCancellationConfirmed', 'RemoteDefaultsPreserved', 'LocalConfirmedTokenPreserved',
                        'LocalTokenDurableWhenWritable', 'InMemoryTokenWarningWhenBlocked')) {
                        foreach ($invalid in @($false, 'true', $null)) {
                            $bad = $menuJson | ConvertFrom-Json
                            $bad.$flag = $invalid
                            Expect-Rejected $bad $terminal $events $exit "menu flag $flag with coerced/false value"
                        }
                        $bad = $menuJson | ConvertFrom-Json
                        $bad.PSObject.Properties.Remove($flag)
                        Expect-Rejected $bad $terminal $events $exit "missing menu flag $flag"
                    }
                    foreach ($field in @('EntityId', 'ClassId', 'LevelId', 'InitialSequence', 'FinalSequence',
                        'InitialX', 'InitialY', 'RejoinedX', 'RejoinedY', 'FinalX', 'FinalY',
                        'InitialGeneration', 'ReturnedGeneration', 'RejoinedGeneration', 'PendingCountAtRefusal',
                        'InitialModelGeneration', 'RejoinedModelGeneration')) {
                        foreach ($invalid in @('1', $true, $null, 1.5)) {
                            $bad = $menuJson | ConvertFrom-Json
                            $bad.$field = $invalid
                            Expect-Rejected $bad $terminal $events $exit "menu numeric field $field with coerced value"
                        }
                        $bad = $menuJson | ConvertFrom-Json
                        $bad.PSObject.Properties.Remove($field)
                        Expect-Rejected $bad $terminal $events $exit "missing menu numeric field $field"
                    }
                    $eventChanges = @(
                        { param($m, $t, $e, $x) $m.InitialModelGeneration = 2 },
                        { param($m, $t, $e, $x) $m.RejoinedModelGeneration = 4 },
                        { param($m, $t, $e, $x) $m.FinalSequence = 5 },
                        { param($m, $t, $e, $x) $t.Sequence = 6 },
                        { param($m, $t, $e, $x) $t.X = 1 },
                        { param($m, $t, $e, $x) $t.Y = 4 },
                        { param($m, $t, $e, $x) $e.Phase = 'Recovery' },
                        { param($m, $t, $e, $x) $e.PriorMenuReturnSequence = 3 },
                        { param($m, $t, $e, $x) $e.PriorMenuReturnX = 1 },
                        { param($m, $t, $e, $x) $e.PriorMenuReturnY = 3 },
                        { param($m, $t, $e, $x) $e.EntityId = 3 },
                        { param($m, $t, $e, $x) $e.ContentHash = ('c' * 64) },
                        { param($m, $t, $e, $x) $e.RulesetHash = ('c' * 64) },
                        { param($m, $t, $e, $x) $e.InitialModelGeneration = 2 },
                        { param($m, $t, $e, $x) $e.InitialGeneration = 2 },
                        { param($m, $t, $e, $x) $e.InitialX = 1 },
                        { param($m, $t, $e, $x) $e.InitialY = 2 },
                        { param($m, $t, $e, $x) $e.FirstClearModelGeneration = 2 },
                        { param($m, $t, $e, $x) $e.SecondReturnedModelGeneration = 1 },
                        { param($m, $t, $e, $x) $e.SecondRejoinedModelGeneration = 3 },
                        { param($m, $t, $e, $x) $e.SecondClearModelGeneration = 3 },
                        { param($m, $t, $e, $x) $e.FirstClearGeneration = 1 },
                        { param($m, $t, $e, $x) $e.SecondReturnedGeneration = 2 },
                        { param($m, $t, $e, $x) $e.SecondRejoinedGeneration = 3 },
                        { param($m, $t, $e, $x) $e.SecondClearGeneration = 3 },
                        { param($m, $t, $e, $x) $e.InitialSequence = 2 },
                        { param($m, $t, $e, $x) $e.FirstApproachAccepted = $false },
                        { param($m, $t, $e, $x) $e.FirstApproachInitialSequence = 3 },
                        { param($m, $t, $e, $x) $e.FirstApproachFinalSequence = 5 },
                        { param($m, $t, $e, $x) $e.FirstClearDialogueSequence = 4 },
                        { param($m, $t, $e, $x) $e.FirstPostClearDialogueSequence = 7 },
                        { param($m, $t, $e, $x) $e.SecondModelDialogueSequence = 8 },
                        { param($m, $t, $e, $x) $e.FinalMovementSequence = 9 },
                        { param($m, $t, $e, $x) $e.FirstClearEventBatchCount = 0 },
                        { param($m, $t, $e, $x) $e.FirstClearEventCount = 0 },
                        { param($m, $t, $e, $x) $e.FirstClearEventKind = ' ' },
                        { param($m, $t, $e, $x) $e.FirstClearEventKind = 'Damage' },
                        { param($m, $t, $e, $x) $e.FirstClearRemainingCount = 1 },
                        { param($m, $t, $e, $x) $e.FirstClearStartSequence = -1; $e.FirstClearEndSequence = 1; $e.FirstClearRemainingStartSequence = 1; $e.FirstClearRemainingEndSequence = 1 },
                        { param($m, $t, $e, $x) $e.FirstClearStartSequence = [long]::MaxValue },
                        { param($m, $t, $e, $x) $e.FirstClearEndSequence = 13 },
                        { param($m, $t, $e, $x) $e.FirstClearRemainingStartSequence = 11 },
                        { param($m, $t, $e, $x) $e.FirstPostClearEventBatchCount = 0 },
                        { param($m, $t, $e, $x) $e.FirstPostClearEventBatchCount = 1 },
                        { param($m, $t, $e, $x) $e.FirstClearEventBatchCount = 3 },
                        { param($m, $t, $e, $x) $e.FirstPostClearEventBatchDelta = 0 },
                        { param($m, $t, $e, $x) $e.FirstPostClearEventBatchDelta = -1 },
                        { param($m, $t, $e, $x) $e.FirstPostClearEventBatchDelta = 3 },
                        { param($m, $t, $e, $x) $e.FirstPostClearEventCount = 0 },
                        { param($m, $t, $e, $x) $e.FirstPostClearEventKind = ' ' },
                        { param($m, $t, $e, $x) $e.FirstPostClearEventKind = 'Damage' },
                        { param($m, $t, $e, $x) $e.SecondClearEventBatchCount = 0 },
                        { param($m, $t, $e, $x) $e.SecondClearEventCount = 0 },
                        { param($m, $t, $e, $x) $e.SecondClearEventBatchDelta = 0 },
                        { param($m, $t, $e, $x) $e.SecondClearEventBatchDelta = -1 },
                        { param($m, $t, $e, $x) $e.SecondClearEventBatchDelta = 2 },
                        { param($m, $t, $e, $x) $e.SecondClearEventKind = 'Damage' },
                        { param($m, $t, $e, $x) $e.SecondClearRemainingCount = 1 },
                        { param($m, $t, $e, $x) $e.SecondClearStartSequence = -1; $e.SecondClearEndSequence = 1; $e.SecondClearEventCount = 2; $e.SecondClearRemainingStartSequence = 1; $e.SecondClearRemainingEndSequence = 1 },
                        { param($m, $t, $e, $x) $e.SecondClearStartSequence = [long]::MaxValue },
                        { param($m, $t, $e, $x) $e.SecondClearEndSequence = 22 },
                        { param($m, $t, $e, $x) $e.SecondClearRemainingEndSequence = 22 },
                        { param($m, $t, $e, $x) $e.FinalCommandSequence = 7 },
                        { param($m, $t, $e, $x) $e.FinalX = 1 },
                        { param($m, $t, $e, $x) $e.FinalY = 4 },
                        { param($m, $t, $e, $x) $e.EntityId = 3 },
                        { param($m, $t, $e, $x) $e.BookmarkBytesPreserved = $false },
                        { param($m, $t, $e, $x) $e.IdentityPreserved = $false },
                        { param($m, $t, $e, $x) $e.FinalMovementAccepted = $false },
                        { param($m, $t, $e, $x) $x.HandshakeExitCleanupFailureCount = 1 },
                        { param($m, $t, $e, $x) $x.HandshakeExitPostExitMenuPublicationAttemptCount = 1 },
                        { param($m, $t, $e, $x) $x.HandshakeExitPostExitMenuPublicationAttemptCount = -1 },
                        { param($m, $t, $e, $x) $x.ReturnExitCleanupFailureCount = -1 },
                        { param($m, $t, $e, $x) $x.ReturnExitPostExitMenuPublicationAttemptCount = 1 },
                        { param($m, $t, $e, $x) $x.ReturnExitCleanupGateObserved = $false },
                        { param($m, $t, $e, $x) $x.ReturnExitStillPendingWhileGated = $false },
                        { param($m, $t, $e, $x) $x.Phase = 'Recovery' }
                    )
                    foreach ($change in $eventChanges) {
                        $badMenu = $menuJson | ConvertFrom-Json
                        $badTerminal = $terminalJson | ConvertFrom-Json
                        $badEvents = $eventsJson | ConvertFrom-Json
                        $badExit = $exitJson | ConvertFrom-Json
                        & $change $badMenu $badTerminal $badEvents $badExit
                        $mutation = [regex]::Match($change.ToString(), '\$(?:m|t|e|x)\.([A-Za-z][A-Za-z0-9_]*)')
                        $case = if ($mutation.Success) { 'mutation field ' + $mutation.Groups[1].Value } else { 'event evidence mutation' }
                        Expect-Rejected $badMenu $badTerminal $badEvents $badExit $case
                    }
                    $changes = @(
                        { param($m, $t) $m.Phase = 'Recovery' },
                        { param($m, $t) $m.Mode = 'Multiplayer' },
                        { param($m, $t) $m.ContentHash = 'bad' },
                        { param($m, $t) $m.RulesetHash = ('c' * 64) },
                        { param($m, $t) $m.EntityId = 0 },
                        { param($m, $t) $m.ClassId = 0 },
                        { param($m, $t) $m.LevelId = -1 },
                        { param($m, $t) $m.InitialSequence = 0 },
                        { param($m, $t) $m.FinalSequence = 5 },
                        { param($m, $t) $m.RejoinedX = 1 },
                        { param($m, $t) $m.RejoinedY = 2 },
                        { param($m, $t) $m.FinalX = 1 },
                        { param($m, $t) $m.FinalY = 3 },
                        { param($m, $t) $m.InitialY = [long]::MaxValue },
                        { param($m, $t) $m.ContentHash = @(('a' * 64), ('b' * 64)) },
                        { param($m, $t) $m.InitialGeneration = 0 },
                        { param($m, $t) $m.ReturnedGeneration = 1 },
                        { param($m, $t) $m.RejoinedGeneration = 2 },
                        { param($m, $t) $m.PendingCountAtRefusal = 4 },
                        { param($m, $t) $m.PendingReturnRejected = 'true' },
                        { param($m, $t) $m.BookmarkReturnRejected = $null },
                        { param($m, $t) $t.Passed = $false },
                        { param($m, $t) $t.ContentHash = ('c' * 64) },
                        { param($m, $t) $t.Sequence = 5 },
                        { param($m, $t) $t.Sequence = '4' },
                        { param($m, $t) $t.X = 1 },
                        { param($m, $t) $t.Y = 4 },
                        { param($m, $t) $t.LevelId = 1 }
                    )
                    foreach ($change in $changes) {
                        $badMenu = $menuJson | ConvertFrom-Json
                        $badTerminal = $terminalJson | ConvertFrom-Json
                        & $change $badMenu $badTerminal
                        $mutation = [regex]::Match($change.ToString(), '\$(?:m|t)\.([A-Za-z][A-Za-z0-9_]*)')
                        $case = if ($mutation.Success) { 'mutation field ' + $mutation.Groups[1].Value } else { 'menu/terminal mutation' }
                        Expect-Rejected $badMenu $badTerminal $events $exit $case
                    }
                    if ($creating) {
                        foreach ($flag in @('PendingReturnRejected', 'BookmarkReturnRejected')) {
                            $bad = $menuJson | ConvertFrom-Json; $bad.$flag = $false
                            Expect-Rejected $bad $terminal $events $exit "creation refusal flag $flag set false"
                        }
                    }
                    $createdEvidence = [pscustomobject]@{ Menu = $accepted.Menu; Events = $accepted.Events }
                    $resumedMenu = $menuJson | ConvertFrom-Json
                    $resumedMenu.InitialSequence = 9; $resumedMenu.InitialX = 0; $resumedMenu.InitialY = 4
                    $resumedEvidence = [pscustomobject]@{
                        Menu = $resumedMenu
                        Events = [pscustomobject]@{ InitialSequence = 9 }
                    }
                    Assert-SessionMenuColdResumeEvidence $createdEvidence $resumedEvidence
                    foreach ($change in @(
                        { param($m, $e) $m.InitialSequence = 10 },
                        { param($m, $e) $m.InitialX = 1 },
                        { param($m, $e) $m.InitialY = 5 },
                        { param($m, $e) $m.EntityId = 3 },
                        { param($m, $e) $m.ClassId = 2 },
                        { param($m, $e) $m.LevelId = 1 },
                        { param($m, $e) $m.ContentHash = ('c' * 64) },
                        { param($m, $e) $m.RulesetHash = ('c' * 64) },
                        { param($m, $e) $e.InitialSequence = 10 }
                    )) {
                        $badResumeMenu = $resumedMenu | ConvertTo-Json -Depth 5 | ConvertFrom-Json
                        $badResumeEvents = [pscustomobject]@{ InitialSequence = 9 }
                        & $change $badResumeMenu $badResumeEvents
                        $rejected = $false
                        try {
                            Assert-SessionMenuColdResumeEvidence $createdEvidence ([pscustomobject]@{
                                Menu = $badResumeMenu; Events = $badResumeEvents
                            })
                        } catch { $rejected = $true }
                        if (-not $rejected) {
                            $mutation = [regex]::Match($change.ToString(), '\$(?:m|e)\.([A-Za-z][A-Za-z0-9_]*)')
                            $case = if ($mutation.Success) { 'mutation field ' + $mutation.Groups[1].Value } else { 'cold-resume mutation' }
                            throw "Inconsistent cold-resume evidence was accepted for phase '$phase' ($case)."
                        }
                    }
                }
                'PASS'
                """;
            Assert.Contains("PASS", RunPowerShell(BuildDriverFunctionHarness(
                ["Assert-SessionMenuEvidence", "Assert-SessionMenuColdResumeEvidence"],
                "$work = " + QuotePowerShell(root) + "\r\n" + body), root),
                StringComparison.Ordinal);
        } finally {
            DeleteOwnedTempDirectory(root, "Godevilution-Session-Menu-Evidence-");
        }
    }

    [Theory]
    [InlineData("godot-client/run-local-client.ps1", "SessionMenuAcceptance")]
    [InlineData("tools/test-godot-session.ps1", "SessionMenu")]
    public void SessionMenuAcceptanceGuardsRefuseOtherProfilesBeforeAnyLaunch(string script, string option)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var body = option == "SessionMenuAcceptance" ? """
            & $guards -SessionMenuAcceptance -SessionDirectory 'fixture' -AcceptancePhase Create | Out-Null
            & $guards -SessionMenuAcceptance -SessionDirectory 'fixture' -AcceptancePhase Resume | Out-Null
            $badArguments = @(
                @{ SessionMenuAcceptance = $true },
                @{ SessionMenuAcceptance = $true; AcceptancePhase = 'Create' },
                @{ SessionMenuAcceptance = $true; SessionDirectory = 'fixture'; AcceptancePhase = 'Recovery' },
                @{ SessionMenuAcceptance = $true; SessionDirectory = 'fixture'; AcceptancePhase = 'Create'; PreviewContent = $true },
                @{ SessionMenuAcceptance = $true; SessionDirectory = 'fixture'; AcceptancePhase = 'Create'; Multiplayer = $true },
                @{ SessionMenuAcceptance = $true; SessionDirectory = 'fixture'; AcceptancePhase = 'Create'; CathedralProfileDirectory = 'fixture' })
            """ : """
            & $guards -SessionMenu | Out-Null
            $badArguments = @(
                @{ SessionMenu = $true; Journey = $true },
                @{ SessionMenu = $true; Lifecycle = $true },
                @{ SessionMenu = $true; MultiplayerRecovery = $true },
                @{ SessionMenu = $true; Cooperative = $true },
                @{ SessionMenu = $true; PresentationPackProfile = 'atlas' },
                @{ SessionMenu = $true; CathedralDepthProfileDirectory = 'fixture' })
            """;
        body += """

            foreach ($arguments in $badArguments) {
                $rejected = $false
                try { & $guards @arguments | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'An incompatible session-menu profile reached launch.' }
            }
            'PASS'
            """;
        Assert.Contains("PASS", RunPowerShell(BuildLauncherGuardHarness(body, script)), StringComparison.Ordinal);
    }

    [Fact]
    public void LauncherModeGuardRequiresKnownModeAndExplicitCohortSessionDirectory()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = NewRoot("Godevilution-Cohort-Launcher-Guard-");
        Directory.CreateDirectory(root);
        try {
            var body = new StringBuilder()
                .AppendLine("$cohortRoot = " + QuotePowerShell(root))
                .AppendLine("$legacy = @(& $guards -PersistenceMode legacy)")
                .AppendLine("if ($legacy -notcontains 'GUARDS-PASSED') { throw 'Legacy mode was rejected.' }")
                .AppendLine("$cohort = @(& $guards -PersistenceMode cohort -SessionDirectory $cohortRoot)")
                .AppendLine("if ($cohort -notcontains 'GUARDS-PASSED') { throw 'Explicit cohort mode was rejected.' }")
                .AppendLine("$rejected = $false")
                .AppendLine("try { & $guards -PersistenceMode invalid -SessionDirectory $cohortRoot | Out-Null } catch { $rejected = $true }")
                .AppendLine("if (-not $rejected) { throw 'An unknown persistence mode was accepted.' }")
                .AppendLine("$rejected = $false")
                .AppendLine("try { & $guards -PersistenceMode cohort | Out-Null } catch { $rejected = $true }")
                .AppendLine("if (-not $rejected) { throw 'Cohort mode did not require an explicit session directory.' }")
                .AppendLine("'PASS'")
                .ToString();
            Assert.Contains(
                "PASS",
                RunPowerShell(BuildLauncherGuardHarness(body.ToString()), root),
                StringComparison.Ordinal);
        } finally {
            DeleteOwnedTempDirectory(root, "Godevilution-Cohort-Launcher-Guard-");
        }
    }

    [Fact]
    public void CohortCheckpointHelperAcceptsCanonicalSingleAndMultiplayerFixtures()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var single = CreateFixture(
            AuthoritativeGameMode.SinglePlayer,
            ["cohort-single-player"]);
        var multiplayer = CreateFixture(
            AuthoritativeGameMode.Multiplayer,
            ["cohort-multiplayer-one", "cohort-multiplayer-two"]);
        try {
            RunCheckpointHelper(single, "SinglePlayer", [single.BookmarkPaths[0]]);
            RunCheckpointHelper(
                multiplayer,
                "Multiplayer",
                [multiplayer.BookmarkPaths[1], multiplayer.BookmarkPaths[0]],
                [multiplayer.ParticipantEntityIds[1], multiplayer.ParticipantEntityIds[0]]);
        } finally {
            DeleteOwnedTempDirectory(single.Root, "Godevilution-Cohort-Launcher-Fixture-");
            DeleteOwnedTempDirectory(multiplayer.Root, "Godevilution-Cohort-Launcher-Fixture-");
        }
    }

    [Fact]
    public void CohortCheckpointHelperRejectsCorruptionIdentityAndMissingLegacyRoots()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var corrupt = CreateFixture(
            AuthoritativeGameMode.SinglePlayer,
            ["cohort-corrupt"]);
        var invalidUtf8 = CreateFixture(
            AuthoritativeGameMode.SinglePlayer,
            ["cohort-invalid-utf8"]);
        var identity = CreateFixture(
            AuthoritativeGameMode.SinglePlayer,
            ["cohort-identity"]);
        var missing = CreateFixture(
            AuthoritativeGameMode.SinglePlayer,
            ["cohort-missing"]);
        var legacy = CreateFixture(
            AuthoritativeGameMode.SinglePlayer,
            ["cohort-legacy"]);
        try {
            File.WriteAllText(
                corrupt.CheckpointPath,
                File.ReadAllText(corrupt.CheckpointPath).Replace(
                    "\"Tick\":20",
                    "\"Tick\":21",
                    StringComparison.Ordinal));
            RunCheckpointHelperExpectRejected(
                corrupt,
                "SinglePlayer",
                [corrupt.BookmarkPaths[0]],
                corrupt.ParticipantEntityIds);
            File.WriteAllBytes(invalidUtf8.CheckpointPath, [0xFF, 0xFE, 0xFD]);
            RunCheckpointHelperExpectRejected(
                invalidUtf8,
                "SinglePlayer",
                [invalidUtf8.BookmarkPaths[0]],
                invalidUtf8.ParticipantEntityIds);
            RunCheckpointHelperExpectRejected(
                identity,
                "SinglePlayer",
                [identity.BookmarkPaths[0]],
                identity.ParticipantEntityIds,
                contentHash: RulesetHash);
            File.Delete(missing.CheckpointPath);
            RunCheckpointHelperExpectRejected(
                missing,
                "SinglePlayer",
                [missing.BookmarkPaths[0]],
                missing.ParticipantEntityIds);

            File.WriteAllText(
                Path.Combine(legacy.SaveDirectory, "legacy.session.json"),
                "legacy-root");
            RunCheckpointHelperExpectRejected(
                legacy,
                "SinglePlayer",
                legacy.BookmarkPaths,
                legacy.ParticipantEntityIds);
        } finally {
            DeleteOwnedTempDirectory(corrupt.Root, "Godevilution-Cohort-Launcher-Fixture-");
            DeleteOwnedTempDirectory(invalidUtf8.Root, "Godevilution-Cohort-Launcher-Fixture-");
            DeleteOwnedTempDirectory(identity.Root, "Godevilution-Cohort-Launcher-Fixture-");
            DeleteOwnedTempDirectory(missing.Root, "Godevilution-Cohort-Launcher-Fixture-");
            DeleteOwnedTempDirectory(legacy.Root, "Godevilution-Cohort-Launcher-Fixture-");
        }
    }

    [Fact]
    public void CohortCheckpointHelperRejectsWrongDuplicateMissingAndForeignParticipants()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var wrongBookmark = CreateFixture(
            AuthoritativeGameMode.Multiplayer,
            ["cohort-wrong-bookmark-one", "cohort-wrong-bookmark-two"]);
        var duplicateBookmark = CreateFixture(
            AuthoritativeGameMode.Multiplayer,
            ["cohort-duplicate-one", "cohort-duplicate-two"]);
        var missingBookmark = CreateFixture(
            AuthoritativeGameMode.Multiplayer,
            ["cohort-missing-bookmark-one", "cohort-missing-bookmark-two"]);
        var foreignParticipant = CreateFixture(
            AuthoritativeGameMode.Multiplayer,
            ["cohort-foreign-one", "cohort-foreign-two"],
            bookmarkTokens: ["cohort-foreign-one", "cohort-bookmark-not-in-roster"]);
        var missingParticipant = CreateFixture(
            AuthoritativeGameMode.SinglePlayer,
            ["cohort-missing-participant"],
            bookmarkTokens: ["cohort-missing-participant", "cohort-extra-bookmark"]);
        try {
            File.WriteAllText(wrongBookmark.BookmarkPaths[1], "cohort-wrong-owner");
            RunCheckpointHelperExpectRejected(
                wrongBookmark,
                "Multiplayer",
                wrongBookmark.BookmarkPaths,
                wrongBookmark.ParticipantEntityIds);

            File.WriteAllText(
                duplicateBookmark.BookmarkPaths[1],
                "cohort-duplicate-one");
            RunCheckpointHelperExpectRejected(
                duplicateBookmark,
                "Multiplayer",
                duplicateBookmark.BookmarkPaths,
                duplicateBookmark.ParticipantEntityIds);

            File.Delete(missingBookmark.BookmarkPaths[1]);
            RunCheckpointHelperExpectRejected(
                missingBookmark,
                "Multiplayer",
                missingBookmark.BookmarkPaths,
                missingBookmark.ParticipantEntityIds);

            RunCheckpointHelperExpectRejected(
                foreignParticipant,
                "Multiplayer",
                foreignParticipant.BookmarkPaths,
                foreignParticipant.ParticipantEntityIds);
            RunCheckpointHelperExpectRejected(
                missingParticipant,
                "SinglePlayer",
                missingParticipant.BookmarkPaths,
                [1, 2]);
        } finally {
            DeleteOwnedTempDirectory(wrongBookmark.Root, "Godevilution-Cohort-Launcher-Fixture-");
            DeleteOwnedTempDirectory(duplicateBookmark.Root, "Godevilution-Cohort-Launcher-Fixture-");
            DeleteOwnedTempDirectory(missingBookmark.Root, "Godevilution-Cohort-Launcher-Fixture-");
            DeleteOwnedTempDirectory(foreignParticipant.Root, "Godevilution-Cohort-Launcher-Fixture-");
            DeleteOwnedTempDirectory(missingParticipant.Root, "Godevilution-Cohort-Launcher-Fixture-");
        }
    }

    [Fact]
    public void CohortCheckpointHelperReadsCanonicalCheckpointWithSharedDeleteHandle()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var fixture = CreateFixture(
            AuthoritativeGameMode.SinglePlayer,
            ["cohort-shared-delete"]);
        try {
            using var shared = new FileStream(
                fixture.CheckpointPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite | FileShare.Delete,
                4096,
                FileOptions.DeleteOnClose);
            RunCheckpointHelper(
                fixture,
                "SinglePlayer",
                fixture.BookmarkPaths,
                fixture.ParticipantEntityIds);
        } finally {
            DeleteOwnedTempDirectory(fixture.Root, "Godevilution-Cohort-Launcher-Fixture-");
        }
    }

    [Fact]
    public void CohortCheckpointHelperRejectsSwappedSceneBindings()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var fixture = CreateFixture(AuthoritativeGameMode.Multiplayer, ["scene-one", "scene-two"]);
        try {
            RunCheckpointHelperExpectRejected(fixture, "Multiplayer", fixture.BookmarkPaths,
                [fixture.ParticipantEntityIds[1], fixture.ParticipantEntityIds[0]]);
        } finally {
            DeleteOwnedTempDirectory(fixture.Root, "Godevilution-Cohort-Launcher-Fixture-");
        }
    }

    [Fact]
    public void CohortCheckpointHelperRejectsResignedTypeShapeAndModuleErrors()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var fixture = CreateFixture(AuthoritativeGameMode.SinglePlayer, ["strict-fixture"]);
        try {
            var original = File.ReadAllText(fixture.CheckpointPath);
            var suffix = original.LastIndexOf(",\"IntegritySha256\":\"", StringComparison.Ordinal);
            Assert.True(suffix > 0);
            var payload = original[..suffix] + "}";
            var invalidPayloads = new[] {
                payload.Replace("\"FormatVersion\":1", "\"FormatVersion\":\"1\"", StringComparison.Ordinal),
                payload.Replace("\"EntityId\":1", "\"EntityId\":\"1\"", StringComparison.Ordinal),
                payload.Replace("\"Tick\":20", "\"Tick\":20.0", StringComparison.Ordinal),
                payload.Replace("\"Generation\":1", "\"Generation\":1,\"Unexpected\":0", StringComparison.Ordinal),
                payload.Replace("bW9kdWxlLXN0YXRl", "wA==", StringComparison.Ordinal),
            };
            foreach (var invalidPayload in invalidPayloads) {
                Assert.NotEqual(payload, invalidPayload);
                var integrity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(invalidPayload))).ToLowerInvariant();
                File.WriteAllText(fixture.CheckpointPath,
                    invalidPayload[..^1] + ",\"IntegritySha256\":\"" + integrity + "\"}",
                    new UTF8Encoding(false));
                RunCheckpointHelperExpectRejected(fixture, "SinglePlayer", fixture.BookmarkPaths);
            }
        } finally {
            DeleteOwnedTempDirectory(fixture.Root, "Godevilution-Cohort-Launcher-Fixture-");
        }
    }

    [Fact]
    public void CohortSceneEntityProofCannotFallBackToMissingOrCoercedBindings()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var body = """
            if ((Get-CohortSceneEntityId ([pscustomobject]@{ EntityId = 1 })) -ne 1) { throw 'Valid scene binding rejected.' }
            foreach ($invalid in @([pscustomobject]@{}, [pscustomobject]@{EntityId=0},
                [pscustomobject]@{EntityId='1'}, [pscustomobject]@{EntityId=1.0}, [pscustomobject]@{EntityId=$true})) {
                $rejected = $false
                try { Get-CohortSceneEntityId $invalid | Out-Null } catch { $rejected = $true }
                if (-not $rejected) { throw 'Invalid scene binding was accepted.' }
            }
            'PASS'
            """;
        Assert.Contains("PASS", RunPowerShell(BuildPowerShellHarness(
            ["Test-CohortJsonInteger", "Get-CohortSceneEntityId"], body)), StringComparison.Ordinal);
    }

    [Fact]
    public void HarmlessFixtureTimeoutJoinsOwnedChildBeforeRemovingItsScript()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var failure = Assert.Throws<TimeoutException>(() => RunPowerShell("Start-Sleep -Seconds 120\r\n", timeoutMilliseconds: 1000));
        Assert.Equal(true, failure.Data["ExitConfirmed"]);
        Assert.False(File.Exists((string)failure.Data["ScriptPath"]!));
    }

    private static void RunCheckpointHelper(
        Fixture fixture,
        string expectedGameMode,
        IReadOnlyList<string> bookmarkPaths,
        IReadOnlyList<uint>? expectedEntityIds = null,
        string? contentHash = null,
        string? rulesetHash = null)
    {
        var body = new StringBuilder()
            .AppendLine("$saveDirectory = " + QuotePowerShell(fixture.SaveDirectory))
            .AppendLine("$bookmarkPaths = @(" + string.Join(", ", bookmarkPaths.Select(QuotePowerShell)) + ")")
            .AppendLine("$expectedEntityIds = @(" + string.Join(", ", expectedEntityIds ?? fixture.ParticipantEntityIds) + ")")
            .AppendLine("Assert-CohortCheckpoint -SaveDirectory $saveDirectory -ExpectedGameMode "
                + QuotePowerShell(expectedGameMode)
                + " -ContentHash " + QuotePowerShell(contentHash ?? ContentHash)
                + " -RulesetHash " + QuotePowerShell(rulesetHash ?? RulesetHash)
                + " -BookmarkPaths $bookmarkPaths -ExpectedEntityIds $expectedEntityIds | Out-Null")
            .AppendLine("'PASS'")
            .ToString();
        Assert.Contains(
            "PASS",
            RunPowerShell(BuildPowerShellHarness(CohortCheckpointFunctions, body), fixture.Root),
            StringComparison.Ordinal);
    }

    private static void RunCheckpointHelperExpectRejected(
        Fixture fixture,
        string expectedGameMode,
        IReadOnlyList<string> bookmarkPaths,
        IReadOnlyList<uint>? expectedEntityIds = null,
        string? contentHash = null,
        string? rulesetHash = null)
    {
        var body = new StringBuilder()
            .AppendLine("$saveDirectory = " + QuotePowerShell(fixture.SaveDirectory))
            .AppendLine("$bookmarkPaths = @(" + string.Join(", ", bookmarkPaths.Select(QuotePowerShell)) + ")")
            .AppendLine("$expectedEntityIds = @(" + string.Join(", ", expectedEntityIds ?? fixture.ParticipantEntityIds) + ")")
            .AppendLine("$rejected = $false")
            .AppendLine("try {")
            .AppendLine("    Assert-CohortCheckpoint -SaveDirectory $saveDirectory -ExpectedGameMode "
                + QuotePowerShell(expectedGameMode)
                + " -ContentHash " + QuotePowerShell(contentHash ?? ContentHash)
                + " -RulesetHash " + QuotePowerShell(rulesetHash ?? RulesetHash)
                + " -BookmarkPaths $bookmarkPaths -ExpectedEntityIds $expectedEntityIds | Out-Null")
            .AppendLine("} catch { $rejected = $true }")
            .AppendLine("if (-not $rejected) { throw 'Malformed cohort fixture was accepted.' }")
            .AppendLine("'PASS'")
            .ToString();
        Assert.Contains(
            "PASS",
            RunPowerShell(BuildPowerShellHarness(CohortCheckpointFunctions, body), fixture.Root),
            StringComparison.Ordinal);
    }

    private static Fixture CreateFixture(
        AuthoritativeGameMode gameMode,
        IReadOnlyList<string> participantTokens,
        IReadOnlyList<string>? bookmarkTokens = null)
    {
        var root = NewRoot("Godevilution-Cohort-Launcher-Fixture-");
        var saveDirectory = Path.Combine(root, "save");
        Directory.CreateDirectory(saveDirectory);
        var participants = participantTokens.Select((token, index) =>
            new AuthoritativeCohortParticipantCheckpoint(
                SessionIdForToken(token),
                checked((uint)(index + 1)),
                [])).ToArray();
        var document = new AuthoritativeCohortDocument(
            AuthoritativeCohortStore.CurrentFormatVersion,
            1,
            new AuthoritativeCohortIdentity(
                "devilution.diablo",
                1,
                "0.2.0",
                ContentHash,
                RulesetHash,
                20,
                gameMode),
            20,
            checked((uint)(participants.Length + 1)),
            1,
            participants,
            "module-state");
        using (var store = new AuthoritativeCohortStore(saveDirectory))
            store.Save(document);

        var bookmarkValues = bookmarkTokens ?? participantTokens;
        var bookmarkPaths = bookmarkValues.Select((token, index) => {
            var path = Path.Combine(root, $"bookmark-{index + 1}.txt");
            File.WriteAllText(path, token, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return path;
        }).ToArray();
        return new Fixture(
            root,
            saveDirectory,
            Path.Combine(saveDirectory, AuthoritativeCohortStore.CheckpointFileName),
            bookmarkPaths,
            participants.Select(participant => participant.EntityId).ToArray());
    }

    private static string BuildPowerShellHarness(
        IReadOnlyList<string> functionNames,
        string body,
        string relativeScriptPath = "godot-client/run-local-client.ps1")
    {
        var launcherPath = Path.Combine(FindRepositoryRoot(), relativeScriptPath);
        var lines = new List<string> {
            "$ErrorActionPreference = 'Stop'",
            "$launcherPath = " + QuotePowerShell(launcherPath),
            "$launcherText = [IO.File]::ReadAllText($launcherPath)",
            "$tokens = $null",
            "$parseErrors = $null",
            "$launcherAst = [System.Management.Automation.Language.Parser]::ParseInput($launcherText, [ref]$tokens, [ref]$parseErrors)",
            "if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }",
        };
        foreach (var functionName in functionNames) {
            lines.Add("$functionName = " + QuotePowerShell(functionName));
            lines.Add("$functionAst = $launcherAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $functionName }, $true) | Select-Object -First 1");
            lines.Add("if ($null -eq $functionAst) { throw \"Could not extract function $functionName from launcher.\" }");
            lines.Add(". ([scriptblock]::Create($functionAst.Extent.Text))");
        }
        lines.Add(body.Trim());
        return string.Join("\r\n", lines) + "\r\n";
    }

    private static string BuildDriverFunctionHarness(IReadOnlyList<string> functionNames, string body)
        => BuildPowerShellHarness(functionNames, body, "tools/test-godot-session.ps1");

    private static string BuildLauncherGuardHarness(string body, string script = "godot-client/run-local-client.ps1")
    {
        var launcherPath = Path.Combine(FindRepositoryRoot(), script);
        var lines = new[] {
            "$ErrorActionPreference = 'Stop'",
            "$launcherPath = " + QuotePowerShell(launcherPath),
            "$launcherText = [IO.File]::ReadAllText($launcherPath)",
            "$guardEnd = $launcherText.IndexOf('$repositoryRoot = ', [StringComparison]::Ordinal)",
            "if ($guardEnd -le 0) { throw 'Could not isolate launcher mode guards.' }",
            "$guards = [scriptblock]::Create($launcherText.Substring(0, $guardEnd) + \"`r`n'GUARDS-PASSED'\")",
            body.Trim(),
        };
        return string.Join("\r\n", lines) + "\r\n";
    }

    private static string RunPowerShell(string script, string? fixtureRoot = null, int timeoutMilliseconds = 60_000)
    {
        var scriptPath = Path.Combine(
            Path.GetTempPath(),
            $"godevilution-cohort-launcher-test-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(
            scriptPath,
            script,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var safeToDeleteScript = true;
        try {
            var startInfo = new ProcessStartInfo {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start PowerShell.");
            safeToDeleteScript = false;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMilliseconds)) {
                Exception? cleanupFailure = null;
                try { process.Kill(entireProcessTree: true); } catch (Exception exception) { cleanupFailure = exception; }
                try { safeToDeleteScript = process.WaitForExit(5000); }
                catch (Exception exception) { cleanupFailure ??= exception; }
                if (!safeToDeleteScript && fixtureRoot is not null)
                    RetainedFixtureRoots.TryAdd(Path.GetFullPath(fixtureRoot), 0);
                var failure = new TimeoutException("PowerShell behavioral harness timed out; "
                    + (safeToDeleteScript ? "owned child exit confirmed." : "cleanup unconfirmed; script and fixture retained."), cleanupFailure);
                failure.Data["ExitConfirmed"] = safeToDeleteScript;
                failure.Data["ScriptPath"] = scriptPath;
                failure.Data["OwnedProcessId"] = process.Id;
                throw failure;
            }
            safeToDeleteScript = true;
            var output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
            Assert.True(
                process.ExitCode == 0,
                $"PowerShell behavioral harness failed ({process.ExitCode}):{Environment.NewLine}{output}");
            return output;
        } finally {
            if (safeToDeleteScript && File.Exists(scriptPath))
                File.Delete(scriptPath);
        }
    }

    private static string QuotePowerShell(string value)
        => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }

    private static string NewRoot(string prefix)
        => Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));

    private static string SessionIdForToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();

    private static void DeleteOwnedTempDirectory(string directory, string prefix)
    {
        var full = Path.GetFullPath(directory);
        if (RetainedFixtureRoots.ContainsKey(full))
            return;
        var parent = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(full);
        if (!string.Equals(
                Path.GetDirectoryName(full)?.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                parent,
                StringComparison.OrdinalIgnoreCase)
            || !name.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(name[prefix.Length..], "N", out _))
            throw new InvalidOperationException("Refusing to delete an unowned launcher test directory.");
        if (!Directory.Exists(full))
            return;
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Refusing to recurse through a reparse point.");
        Directory.Delete(full, recursive: true);
    }

    private sealed record Fixture(
        string Root,
        string SaveDirectory,
        string CheckpointPath,
        IReadOnlyList<string> BookmarkPaths,
        IReadOnlyList<uint> ParticipantEntityIds);
}
