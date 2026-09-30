using System.Text.Json;
using Devilution.Protocol.V1;
using Godot;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private const int PerformanceWarmupRounds = 5;
    private const int PerformanceMeasuredRounds = 15;
    private const int PerformanceTotalRounds = PerformanceWarmupRounds + PerformanceMeasuredRounds;
    private static readonly TimeSpan PerformanceMeasurementTail = TimeSpan.FromSeconds(1.25);
    private static readonly TimeSpan PerformanceDeadline = TimeSpan.FromSeconds(120);

    private async Task RunPerformanceAsync(string directory)
    {
        var participants = ReadPerformanceParticipants();
        var slot = ReadPerformanceSlot(participants);
        var sharedDirectory = System.Environment.GetEnvironmentVariable(
            "GODEVILUTION_PERFORMANCE_SHARED_DIRECTORY")
            ?? throw new InvalidOperationException("A shared performance directory is required.");
        Require(!string.IsNullOrWhiteSpace(sharedDirectory)
            && System.IO.Path.IsPathFullyQualified(sharedDirectory),
            "Performance requires an absolute shared marker directory.");
        Require(System.IO.Directory.Exists(sharedDirectory),
            "Performance shared marker directory does not exist.");
        Require(System.IO.Directory.Exists(directory),
            "Performance private evidence directory does not exist.");

        var playerName = $"M0-Performance-{slot}";
        var expectedMode = participants == 1 ? GameMode.SinglePlayer : GameMode.Multiplayer;
        var bookmarkPath = System.IO.Path.Combine(directory, "resume-token.txt");
        Require(!System.IO.File.Exists(bookmarkPath),
            "Performance requires a fresh private character directory.");

        Main? main = null;
        var recordingStarted = false;
        try {
            main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
            AddChild(main);
            await Frame();
            await Frame();

            var setup = main.GetNode<CharacterSetupPresentation>("CharacterSetup");
            Require(setup.Visible && !main.TransportConnected,
                "Performance must enter through the real new-character form.");
            var mode = (OptionButton)setup.FindChild("SessionMode", true, false);
            Require(mode.Selected == 0, "Performance must use the new-character form.");
            ((LineEdit)setup.FindChild("CharacterName", true, false)).Text = playerName;
            ((OptionButton)setup.FindChild("CharacterClass", true, false)).Select(1);

            await Click(((Button)setup.FindChild("Connect", true, false))
                .GetGlobalRect().GetCenter());
            await PerformanceUntil(
                () => main.TransportConnected
                    && main.ObservedSnapshot?.Players.Any(player =>
                        player.CharacterName == playerName) == true
                    && main.ObservedIdentity is not null,
                "Performance character did not connect through the real form.");
            Require(!setup.Visible, "Performance setup did not close after connection.");
            var publicIdentity = RequirePerformanceIdentity(main, expectedMode);

            await StagePerformancePlayer(main, playerName, slot);
            WritePerformanceMarker(
                sharedDirectory,
                $"performance-ready-{slot}.json",
                new PerformanceMarker(0, slot, participants, true,
                    publicIdentity.Mode, publicIdentity.ContentHash, publicIdentity.RulesetHash));
            await WaitForPerformanceMarkers(
                sharedDirectory,
                "performance-ready",
                round: 0,
                participants,
                publicIdentity,
                expectedSlot: null);
            WritePerformanceObservation(directory, main, main.ObservedSnapshot, participants, slot, 0, "staging-entry");
            await PerformanceUntil(
                () => PerformanceWorkloadObservations.AllParticipantsStaged(main.ObservedSnapshot, participants),
                "Performance did not observe every final staged and settled participant.");
            RequirePerformanceIdentityStable(main, publicIdentity, expectedMode);
            WritePerformanceObservation(directory, main, main.ObservedSnapshot, participants, slot, 0, "staging-settled");
            WritePerformanceMarker(sharedDirectory, $"performance-staging-observed-{slot}.json",
                new PerformanceMarker(0, slot, participants, true,
                    publicIdentity.Mode, publicIdentity.ContentHash, publicIdentity.RulesetHash));
            await WaitForPerformanceMarkers(sharedDirectory, "performance-staging-observed", 0,
                participants, publicIdentity, expectedSlot: null);

            for (var round = 1; round <= PerformanceWarmupRounds; round++) {
                await RunPerformanceRound(
                    main,
                    directory,
                    sharedDirectory,
                    playerName,
                    slot,
                    participants,
                    publicIdentity,
                    round);
            }

            recordingStarted = main.StartPerformanceRecording(
                participants,
                warmupSeconds: 0);
            Require(recordingStarted,
                "Performance frame recording was not enabled with a fresh output directory.");
            WritePerformanceMarker(
                sharedDirectory,
                $"performance-recording-{slot}.json",
                new PerformanceMarker(
                    PerformanceWarmupRounds,
                    slot,
                    participants,
                    true,
                    publicIdentity.Mode,
                    publicIdentity.ContentHash,
                    publicIdentity.RulesetHash));
            await WaitForPerformanceMarkers(
                sharedDirectory,
                "performance-recording",
                PerformanceWarmupRounds,
                participants,
                publicIdentity,
                expectedSlot: null);

            for (var round = PerformanceWarmupRounds + 1;
                 round <= PerformanceTotalRounds;
                 round++) {
                await RunPerformanceRound(
                    main,
                    directory,
                    sharedDirectory,
                    playerName,
                    slot,
                    participants,
                    publicIdentity,
                    round);
            }

            WritePerformanceMarker(
                sharedDirectory,
                $"performance-finished-{slot}.json",
                new PerformanceMarker(
                    PerformanceTotalRounds,
                    slot,
                    participants,
                    true,
                    publicIdentity.Mode,
                    publicIdentity.ContentHash,
                    publicIdentity.RulesetHash));
            await WaitForPerformanceMarkers(
                sharedDirectory,
                "performance-finished",
                PerformanceTotalRounds,
                participants,
                publicIdentity,
                expectedSlot: null);
            // Keep the recorder active beyond the fifteenth settled round so
            // the measured window is unambiguously at least fifteen seconds.
            await ToSignal(
                GetTree().CreateTimer(PerformanceMeasurementTail.TotalSeconds),
                SceneTreeTimer.SignalName.Timeout);
            await PerformanceUntil(() => {
                RequirePerformanceIdentityStable(main, publicIdentity, expectedMode);
                var snapshot = main.ObservedSnapshot;
                return main.ObservedPendingCommandCount == 0
                    && snapshot is not null
                    && snapshot.Players.Count == participants
                    && snapshot.Players.All(player =>
                        player.CombatMode == PlayerCombatMode.Stand
                        && player.CurrentAction is null);
            }, "Performance final state did not settle before recording stopped.");

            var frameEvidencePath = main.ClientFrameEvidencePath;
            Require(!string.IsNullOrWhiteSpace(frameEvidencePath),
                "Performance frame evidence path was not published.");
            var frameEvidenceName = System.IO.Path.GetFileName(frameEvidencePath!);
            Require(!string.IsNullOrWhiteSpace(frameEvidenceName),
                "Performance frame evidence filename was not published.");
            Require(main.StopPerformanceRecording(workloadPassed: true),
                "Performance frame recording did not stop successfully.");
            recordingStarted = false;
            Require(System.IO.File.Exists(frameEvidencePath),
                "Performance frame evidence file was not written.");
            WriteEvidence(
                directory,
                "performance.json",
                new PerformanceEvidence(
                    true,
                    "Performance",
                    publicIdentity.Mode,
                    publicIdentity.ContentHash,
                    publicIdentity.RulesetHash,
                    PerformanceWarmupRounds,
                    PerformanceMeasuredRounds,
                    participants,
                    slot,
                    frameEvidenceName));
            WritePerformanceMarker(
                sharedDirectory,
                $"performance-stopped-{slot}.json",
                new PerformanceMarker(
                    PerformanceTotalRounds,
                    slot,
                    participants,
                    true,
                    publicIdentity.Mode,
                    publicIdentity.ContentHash,
                    publicIdentity.RulesetHash));
            await WaitForPerformanceMarkers(
                sharedDirectory,
                "performance-stopped",
                PerformanceTotalRounds,
                participants,
                publicIdentity,
                expectedSlot: null);
            if (!string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
                await Capture(System.IO.Path.Combine(directory, "performance-view.png"));
            var completionPrefix = string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase)
                ? "performance-exit-ready"
                : "performance-captured";
            WritePerformanceMarker(
                sharedDirectory,
                $"{completionPrefix}-{slot}.json",
                new PerformanceMarker(
                    PerformanceTotalRounds,
                    slot,
                    participants,
                    true,
                    publicIdentity.Mode,
                    publicIdentity.ContentHash,
                    publicIdentity.RulesetHash));
            await WaitForPerformanceMarkers(
                sharedDirectory,
                completionPrefix,
                PerformanceTotalRounds,
                participants,
                publicIdentity,
                expectedSlot: null);
        } catch {
            if (recordingStarted && main is not null) {
                try {
                    main.StopPerformanceRecording(workloadPassed: false);
                } catch {
                    // Preserve the original acceptance failure.
                }
            }
            throw;
        }
    }

    private async Task StagePerformancePlayer(Main main, string playerName, int slot)
    {
        var keys = slot switch {
            1 => new[] { Key.Down, Key.Down, Key.Down },
            2 => new[] { Key.Right, Key.Right, Key.Right },
            3 => new[] { Key.Down },
            4 => new[] { Key.Right, Key.Down },
            _ => throw new InvalidOperationException("Unsupported performance slot."),
        };
        foreach (var key in keys) {
            var player = FindPerformancePlayer(main.ObservedSnapshot, playerName);
            await SubmitPerformanceMove(
                main,
                playerName,
                key,
                player.PositionX + (key == Key.Right ? 1 : 0),
                player.PositionY + (key == Key.Down ? 1 : 0));
        }
    }

    private async Task RunPerformanceRound(
        Main main,
        string directory,
        string sharedDirectory,
        string playerName,
        int slot,
        int participants,
        PerformanceIdentity identity,
        int round)
    {
        RequirePerformanceIdentityStable(
            main,
            identity,
            participants == 1 ? GameMode.SinglePlayer : GameMode.Multiplayer);
        var entrySnapshot = main.ObservedSnapshot;
        WritePerformanceObservation(directory, main, entrySnapshot, participants, slot, round, "round-entry");
        var before = CapturePerformancePlayers(entrySnapshot, participants);
        if (slot == 1) {
            var key = round % 2 == 1 ? Key.Right : Key.Left;
            var expectedX = round % 2 == 1 ? 1 : 0;
            await SubmitPerformanceMove(main, playerName, key, expectedX, 3);
            WritePerformanceMarker(
                sharedDirectory,
                $"performance-round-{round}.json",
                new PerformanceMarker(
                    round,
                    1,
                    participants,
                    true,
                    identity.Mode,
                    identity.ContentHash,
                    identity.RulesetHash));
        }

        var roundMarker = await WaitForPerformanceMarker(
            System.IO.Path.Combine(sharedDirectory, $"performance-round-{round}.json"),
            $"Missing performance-round-{round}.json performance marker.");
        RequirePerformanceMarker(roundMarker, round, 1, participants, identity,
            $"Invalid public performance-round-{round}.json marker.");
        var observationRecorded = false;
        await PerformanceUntil(() => {
            RequirePerformanceIdentityStable(
                main,
                identity,
                participants == 1 ? GameMode.SinglePlayer : GameMode.Multiplayer);
            var snapshot = main.ObservedSnapshot;
            if (!observationRecorded) {
                var matches = Enumerable.Range(1, participants).Select(peerSlot => {
                    var player = snapshot?.Players.SingleOrDefault(candidate =>
                        candidate.CharacterName == $"M0-Performance-{peerSlot}");
                    return player is not null && before.TryGetValue(player.CharacterName, out var prior)
                        && SamePerformanceState(prior, player, allowPositionChange: peerSlot == 1);
                }).ToArray();
                WritePerformanceObservation(directory, main, snapshot, participants, slot, round,
                    "round-first-check", matches);
                observationRecorded = true;
            }
            if (snapshot is null || snapshot.Players.Count != participants)
                return false;
            var leader = FindPerformancePlayer(snapshot, "M0-Performance-1");
            var expectedX = round % 2 == 1 ? 1 : 0;
            if (leader.PositionX != expectedX
                || leader.PositionY != 3
                || leader.CombatMode != PlayerCombatMode.Stand
                || leader.CurrentAction is not null)
                return false;
            foreach (var player in snapshot.Players) {
                if (!before.TryGetValue(player.CharacterName, out var prior))
                    return false;
                if (player.CharacterName == "M0-Performance-1") {
                    if (!SamePerformanceState(prior, player, allowPositionChange: true))
                        return false;
                } else if (!SamePerformanceState(prior, player, allowPositionChange: false)) {
                    return false;
                }
            }
            return true;
        }, $"Performance round {round} did not settle on every participant.");

        WritePerformanceMarker(
            sharedDirectory,
            $"performance-observed-{round}-{slot}.json",
            new PerformanceMarker(
                round,
                slot,
                participants,
                true,
                identity.Mode,
                identity.ContentHash,
                identity.RulesetHash));
        await WaitForPerformanceMarkers(
            sharedDirectory,
            "performance-observed",
            round,
            participants,
            identity,
            expectedSlot: null,
            includeRoundSlot: true);
        await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
    }

    private async Task SubmitPerformanceMove(
        Main main,
        string playerName,
        Key key,
        int expectedX,
        int expectedY)
    {
        var previousSequence = main.ObservedCommandResult?.ClientSequence ?? 0;
        GetViewport().GuiReleaseFocus();
        Input.ParseInputEvent(new InputEventKey {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = true,
        });
        await Frame();
        Input.ParseInputEvent(new InputEventKey {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = false,
        });
        await Frame();
        await PerformanceUntil(() => {
            var result = main.ObservedCommandResult;
            if (result is not null && result.ClientSequence > previousSequence) {
                Require(result.ClientSequence == previousSequence + 1,
                    "Performance input advanced an unexpected command sequence.");
                Require(IsAcceptedOrSuccessfulDuplicate(result),
                    $"Performance input {key} was rejected.");
            }
            var snapshot = main.ObservedSnapshot;
            if (result is null
                || result.ClientSequence != previousSequence + 1
                || !IsAcceptedOrSuccessfulDuplicate(result)
                || main.ObservedPendingCommandCount != 0
                || snapshot is null)
                return false;
            var player = FindPerformancePlayer(snapshot, playerName);
            return player.PositionX == expectedX
                && player.PositionY == expectedY
                && player.CombatMode == PlayerCombatMode.Stand
                && player.CurrentAction is null;
        }, $"Performance input {key} did not settle at ({expectedX},{expectedY}).");
    }

    private async Task WaitForPerformanceMarkers(
        string sharedDirectory,
        string prefix,
        int round,
        int participants,
        PerformanceIdentity identity,
        int? expectedSlot,
        bool includeRoundSlot = false)
    {
        for (var slot = 1; slot <= participants; slot++) {
            if (expectedSlot is not null && slot != expectedSlot.Value)
                continue;
            var name = includeRoundSlot
                ? $"{prefix}-{round}-{slot}.json"
                : $"{prefix}-{slot}.json";
            var marker = await WaitForPerformanceMarker(
                System.IO.Path.Combine(sharedDirectory, name),
                $"Missing {name} performance marker.");
            RequirePerformanceMarker(marker, round, slot, participants, identity,
                $"Invalid public performance marker {name}.");
        }
    }

    private static void RequirePerformanceMarker(
        PerformanceMarker marker,
        int round,
        int slot,
        int participants,
        PerformanceIdentity identity,
        string failure)
    {
        Require(marker.Round == round
            && marker.Slot == slot
            && marker.ParticipantCount == participants
            && marker.Ready
            && marker.Passed
            && marker.Mode == identity.Mode
            && marker.ContentHash == identity.ContentHash
            && marker.RulesetHash == identity.RulesetHash,
            failure);
    }

    private async Task<PerformanceMarker> WaitForPerformanceMarker(
        string path,
        string failure)
    {
        PerformanceMarker? marker = null;
        await PerformanceUntil(() => {
            if (!System.IO.File.Exists(path))
                return false;
            marker = JsonSerializer.Deserialize<PerformanceMarker>(
                System.IO.File.ReadAllText(path));
            Require(marker is not null, $"Performance marker is empty: {path}.");
            return true;
        }, failure);
        return marker!;
    }

    private async Task PerformanceUntil(Func<bool> condition, string failure)
    {
        while (!condition()) {
            Require(deadline.Elapsed < PerformanceDeadline, failure);
            await ToSignal(
                GetTree().CreateTimer(0.1),
                SceneTreeTimer.SignalName.Timeout);
        }
    }

    private static void WritePerformanceMarker(
        string directory,
        string name,
        PerformanceMarker marker)
    {
        WriteEvidence(directory, name, marker);
    }

    private static void WritePerformanceObservation(string directory, Main main, Snapshot? snapshot,
        int participants, int slot, int round, string boundary, bool[]? stableSlots = null)
    {
        // Initial bounded observations survive an earlier peer's timeout cleanup.
        // Controlled slot booleans identify mismatches without player state or credentials.
        WriteEvidence(directory, $"performance-observation-{round}-{boundary}.json", new {
            Phase = "Performance", Slot = slot, Round = round, Boundary = boundary,
            ParticipantCount = participants, ObservedParticipantCount = snapshot?.Players.Count ?? 0,
            main.ObservedSnapshotRevision, main.TransportConnected, main.ObservedClockGatedScheduling,
            StagedSlots = PerformanceWorkloadObservations.StagedSlots(snapshot, participants),
            SettledSlots = PerformanceWorkloadObservations.SettledSlots(snapshot, participants),
            StableSlots = stableSlots,
        });
    }

    private static PerformanceIdentity RequirePerformanceIdentity(
        Main main,
        GameMode expectedMode)
    {
        var identity = main.ObservedIdentity
            ?? throw new InvalidOperationException("Performance server identity is missing.");
        Require(identity.GameMode == expectedMode,
            $"Performance observed {identity.GameMode} instead of {expectedMode} mode.");
        Require(main.ObservedClockGatedScheduling,
            "Performance requires clock-gated command scheduling.");
        Require(!string.IsNullOrWhiteSpace(identity.BuildId)
            && !string.IsNullOrWhiteSpace(identity.ProtocolSchemaVersion)
            && !string.IsNullOrWhiteSpace(identity.ContentManifestHash)
            && !string.IsNullOrWhiteSpace(identity.RulesetIdentityHash)
            && identity.TickRateHz > 0,
            "Performance requires complete public server identity.");
        return new PerformanceIdentity(
            identity.BuildId,
            identity.ProtocolSchemaVersion,
            identity.TickRateHz,
            identity.GameMode.ToString(),
            identity.ContentManifestHash,
            identity.RulesetIdentityHash);
    }

    private static void RequirePerformanceIdentityStable(
        Main main,
        PerformanceIdentity expected,
        GameMode expectedMode)
    {
        var actual = RequirePerformanceIdentity(main, expectedMode);
        Require(actual == expected, "Performance public server identity changed during the workload.");
    }

    private static PlayerSnapshot FindPerformancePlayer(
        Snapshot? snapshot,
        string playerName)
    {
        var player = snapshot?.Players.SingleOrDefault(
            candidate => candidate.CharacterName == playerName);
        return player ?? throw new InvalidOperationException(
            $"Performance participant '{playerName}' is missing from the authoritative snapshot.");
    }

    private static Dictionary<string, PerformancePlayerState> CapturePerformancePlayers(
        Snapshot? snapshot,
        int participants)
    {
        var players = snapshot?.Players
            ?? throw new InvalidOperationException(
                "Performance baseline did not contain an authoritative snapshot.");
        Require(players.Count == participants,
            "Performance baseline did not contain every participant.");
        return players.ToDictionary(
            player => player.CharacterName,
            player => new PerformancePlayerState(
                player.EntityId,
                player.CharacterName,
                player.CharacterClassId,
                player.LevelId,
                player.PositionX,
                player.PositionY,
                player.Gold,
                player.Experience,
                player.Life,
                player.Mana,
                player.HostileMode,
                player.CombatMode,
                player.CurrentAction is null));
    }

    private static bool SamePerformanceState(
        PerformancePlayerState before,
        PlayerSnapshot after,
        bool allowPositionChange)
    {
        return before.EntityId == after.EntityId
            && before.Name == after.CharacterName
            && before.ClassId == after.CharacterClassId
            && before.LevelId == after.LevelId
            && (allowPositionChange
                || (before.X == after.PositionX && before.Y == after.PositionY))
            && before.Gold == after.Gold
            && before.Experience == after.Experience
            && before.Life == after.Life
            && before.Mana == after.Mana
            && before.Hostile == after.HostileMode
            && after.CombatMode == PlayerCombatMode.Stand
            && after.CurrentAction is null;
    }

    private static int ReadPerformanceParticipants()
    {
        var raw = System.Environment.GetEnvironmentVariable(
            "GODEVILUTION_PERFORMANCE_PARTICIPANTS");
        Require(int.TryParse(raw, out var participants)
            && participants is 1 or 4,
            "Performance participants must be 1 or 4.");
        return participants;
    }

    private static int ReadPerformanceSlot(int participants)
    {
        var raw = System.Environment.GetEnvironmentVariable("GODEVILUTION_LOCAL_PLAYER");
        Require(int.TryParse(raw, out var slot)
            && slot >= 1
            && slot <= participants,
            "Performance local player must be within the participant count.");
        return slot;
    }

    private sealed record PerformanceIdentity(
        string BuildId,
        string ProtocolSchemaVersion,
        uint TickRateHz,
        string Mode,
        string ContentHash,
        string RulesetHash);

    private sealed record PerformanceMarker(
        int Round,
        int Slot,
        int ParticipantCount,
        bool Passed,
        string Mode,
        string ContentHash,
        string RulesetHash)
    {
        // The launcher uses this bounded public marker to gate startup; it has
        // no credential, character, command, or snapshot state.
        public bool Ready { get; init; } = true;
    }

    private sealed record PerformanceEvidence(
        bool Passed,
        string Phase,
        string Mode,
        string ContentHash,
        string RulesetHash,
        int WarmupRounds,
        int MeasuredRounds,
        int ParticipantCount,
        int Slot,
        string FrameEvidence);

    private sealed record PerformancePlayerState(
        uint EntityId,
        string Name,
        uint ClassId,
        uint LevelId,
        int X,
        int Y,
        uint Gold,
        uint Experience,
        int Life,
        int Mana,
        bool Hostile,
        PlayerCombatMode CombatMode,
        bool Settled);
}
