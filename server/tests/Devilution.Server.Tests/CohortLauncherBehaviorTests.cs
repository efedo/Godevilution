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

        var launcher = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "godot", "run-local-client.ps1"));
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
        string relativeScriptPath = "godot/run-local-client.ps1")
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

    private static string BuildLauncherGuardHarness(string body)
    {
        var launcherPath = Path.Combine(FindRepositoryRoot(), "godot", "run-local-client.ps1");
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
