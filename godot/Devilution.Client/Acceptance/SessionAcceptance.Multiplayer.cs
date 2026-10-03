using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Godot;
using System.Security.Cryptography;
using Environment = System.Environment;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    /** Two independent real scenes coordinate observations, never gameplay state. */
    private async Task RunMultiplayerAsync(string directory, string phase)
    {
        AcceptanceAckLossRelay? relay = null;
        Exception? primary = null;
        try {
            if (phase == "MultiplayerPendingRecovery") {
                var previous = ReadMultiplayerEvidence(directory, "multiplayerresume.json");
                relay = new AcceptanceAckLossRelay(ClientConnectionOptions.FromEnvironment().Port, previous.Sequence + 1);
            }
            await RunMultiplayerCoreAsync(directory, phase, relay);
        } catch (Exception exception) {
            primary = exception;
            throw;
        } finally {
            if (relay is not null)
                try { await relay.DisposeAsync(); } catch (Exception cleanup) when (primary is not null) {
                    primary.Data["AcceptanceRelayCleanup"] = cleanup.Message;
                }
        }
    }

    private async Task RunMultiplayerCoreAsync(string directory, string phase, AcceptanceAckLossRelay? relay)
    {
        Require(int.TryParse(Environment.GetEnvironmentVariable("GODEVILUTION_LOCAL_PLAYER"), out var slot)
            && slot is 1 or 2, "Multiplayer acceptance requires player slot 1 or 2.");
        var peerSlot = 3 - slot;
        var peerDirectory = System.IO.Path.Combine(System.IO.Directory.GetParent(directory)!.FullName, $"player-{peerSlot}");
        var prefix = phase.ToLowerInvariant();
        var creating = phase == "MultiplayerCreate";
        var recovery = phase is "MultiplayerRecovery" or "MultiplayerPendingRecovery";
        var cohort = Environment.GetEnvironmentVariable("GODEVILUTION_PERSISTENCE_MODE") == "cohort";
        Require(!recovery || cohort, "Multiplayer recovery is only valid for a complete-cohort session.");
        // Reverse the connection order at restart: entity allocation must not be
        // confused with durable character/token identity or command ownership.
        var firstSlot = creating ? 1 : 2;
        MultiplayerEvidence? previous = creating
            ? null
            : ReadMultiplayerEvidence(directory, recovery ? "multiplayerresume.json" : "multiplayercreate.json");
        Require(creating != System.IO.File.Exists(System.IO.Path.Combine(directory, "resume-token.txt")),
            "Creation needs a fresh bookmark; resume needs this player's existing bookmark.");
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        timingObservedMain = main;
        AddChild(main);
        await Frame();
        await Frame();
        var setup = main.GetNode<CharacterSetupPresentation>("CharacterSetup");
        if (relay is not null)
            setup.ShowSetup(ClientConnectionOptions.FromEnvironment() with {
                Port = relay.Port, ResumeToken = LocalSessionBookmark.Read(System.IO.Path.Combine(directory, "resume-token.txt"))!,
            });
        Require(setup.Visible && !main.TransportConnected, "Each player must enter through the real setup form.");
        var mode = (OptionButton)setup.FindChild("SessionMode", true, false);
        Require(mode.Selected == (creating ? 0 : 1), "The player-specific bookmark did not select the correct session mode.");
        if (creating) {
            ((LineEdit)setup.FindChild("CharacterName", true, false)).Text = $"M1-Player-{slot}";
            ((OptionButton)setup.FindChild("CharacterClass", true, false)).Select(1);
        } else {
            Require(previous!.Passed && ((LineEdit)setup.FindChild("ResumeToken", true, false)).Text.Length > 0,
                "Resume requires successful creation evidence and the private player bookmark.");
        }
        var connectionBarrier = recovery ? prefix + "-connected.json" : prefix + "-ready.json";
        if (slot != firstSlot)
            await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, connectionBarrier)),
                "The first player did not connect and prepare its own position.");
        await Click(((Button)setup.FindChild("Connect", true, false)).GetGlobalRect().GetCenter());
        await Until(() => main.TransportConnected && main.ObservedSnapshot?.Players.Count > 0,
            "No authoritative player snapshot arrived.");
        var identity = main.ObservedIdentity!;
        var local = main.ObservedSnapshot!.Players.First();
        Require(identity.GameMode == GameMode.Multiplayer && local.CharacterName == $"M1-Player-{slot}"
            && local.CharacterClassId == 1, "The receiving character is not first, or its identity/mode is wrong.");
        Require(local.LevelId == 0, "The multiplayer acceptance route must start in town.");
        if (previous is not null) {
            Require(identity.ContentManifestHash == previous.ContentHash && identity.RulesetIdentityHash == previous.RulesetHash,
                "Restart changed the multiplayer content/ruleset identity.");
            Require(ObservePlayer(local) == previous.Local, "The player's own durable state was not restored.");
            if (cohort)
                Require(local.EntityId == previous.EntityId,
                    "Complete-cohort resume changed the durable local entity binding.");
        }
        if (creating && slot == firstSlot) {
            await MultiplayerMove(main, Key.Down, 0, 1, 0, directory, prefix);
            await MultiplayerMove(main, Key.Down, 0, 1, 1, directory, prefix);
        }
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(directory, "resume-token.txt")),
            "The player-specific resume bookmark was not persisted.");
        WriteEvidence(directory, connectionBarrier, new { Ready = true, Slot = slot });
        await Until(() => main.ObservedSnapshot!.Players.Count == 2,
            "The second authoritative player did not become visible.");
        var pair = main.ObservedSnapshot!.Players;
        local = pair.First();
        var remote = pair.Last();
        Require(local.EntityId != remote.EntityId && remote.CharacterName == $"M1-Player-{peerSlot}",
            "The two player identities are duplicated or swapped.");
        if (previous is not null) {
            Require(ObservePlayer(remote) == previous.Remote, "The peer's durable state was not restored.");
            if (cohort)
                Require(remote.EntityId == previous.PeerEntityId,
                    "Complete-cohort resume changed the durable peer entity binding.");
        }
        if (recovery) {
            if (relay is not null)
                await VerifyMultiplayerPendingRecoveryAsync(main, directory, prefix, slot, previous!, identity, relay);
            else
                await VerifyMultiplayerRecoveryAsync(main, directory, prefix, slot, previous!, identity);
            return;
        }
        // Both must check the initial restored pair before either changes it.
        WriteEvidence(directory, prefix + "-joined.json", new { Joined = true });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-joined.json")),
            "The peer did not verify the initial shared state.");
        var before = ObservePlayer(local);
        var sequence = previous?.Sequence ?? main.ObservedCommandResult?.ClientSequence ?? 0;
        await MultiplayerMove(main, Key.Right, 1, 0, sequence, directory, prefix);
        await KeyCommand(main, Key.F, sequence + 1, directory, prefix,
            snapshot => snapshot.Players.First() is { CurrentAction: null, CombatMode: PlayerCombatMode.Stand } player
                && player.EntityId == local.EntityId && player.HostileMode != before.Hostile
                && player.PositionX == before.X + 1 && player.PositionY == before.Y,
            "Friendly mode toggled for the same settled local player.");
        var moved = main.ObservedSnapshot!.Players.First();
        Require(moved.EntityId == local.EntityId && moved.PositionX == before.X + 1 && moved.PositionY == before.Y,
            "The local command moved or rebound the wrong player.");
        var localState = ObservePlayer(moved);
        WriteEvidence(directory, prefix + "-moved.json", new MultiplayerMoveEvidence(localState, moved.EntityId));
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-moved.json")),
            "The peer did not complete its own authoritative movement/state inputs.");
        var peerMove = JsonSerializer.Deserialize<MultiplayerMoveEvidence>(System.IO.File.ReadAllText(
            System.IO.Path.Combine(peerDirectory, prefix + "-moved.json")))!;
        await Until(() => main.ObservedSnapshot!.Players.Count == 2
            && ObservePlayer(main.ObservedSnapshot.Players.Last()) == peerMove.Player,
            "The peer's movement and friendly-mode change did not reach this client.");
        Require(ObservePlayer(main.ObservedSnapshot!.Players.First()) == localState,
            "Peer commands changed this player's state.");
        Require(main.ObservedSnapshot.Players.Last().EntityId == peerMove.EntityId,
            "The two clients disagree about the current peer entity ID.");
        await Until(() => main.ObservedEntityScreenPosition(peerMove.EntityId) is { } displayed
            && displayed.DistanceTo(main.ObservedTileScreenPosition(peerMove.Player.X, peerMove.Player.Y)) < 1,
            "The remote actor was not presented at its settled authoritative tile.");
        var world = (Control)main.FindChild("WorldArea", true, false);
        Require(world.GetGlobalRect().HasPoint(main.ObservedEntityScreenPosition(peerMove.EntityId)!.Value)
            && world.GetGlobalRect().HasPoint(main.ObservedPlayerScreenPosition!.Value),
            "The local or remote player is clipped outside the visible world area.");
        var visible = DisplayServer.GetName() != "headless";
        if (visible)
            await Capture(System.IO.Path.Combine(directory, prefix + "-players.png"));
        WriteEvidence(directory, prefix + ".json", new MultiplayerEvidence(true, identity.GameMode.ToString(),
            identity.ContentManifestHash, identity.RulesetIdentityHash, slot, local.EntityId, peerMove.EntityId,
            localState, peerMove.Player, sequence + 2, main.ObservedCommandResult?.ServerReceiptSequence ?? 0,
            main.ObservedCommandResult?.AppliedTick ?? 0, visible));
        // Keep both clients connected until both finished their observations.
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + ".json")),
            "The peer did not finish its shared-state/rendering assertions.");
    }

    private Task MultiplayerMove(Main main, Key key, int x, int y, ulong previousSequence,
        string directory, string prefix)
    {
        var before = main.ObservedSnapshot!.Players.First();
        return KeyCommand(main, key, previousSequence, directory, prefix,
            snapshot => snapshot.Players.First() is { CurrentAction: null, CombatMode: PlayerCombatMode.Stand } moved
                && moved.EntityId == before.EntityId && moved.PositionX == before.PositionX + x && moved.PositionY == before.PositionY + y,
            $"Same settled local player at ({before.PositionX + x},{before.PositionY + y}).");
    }

    /** Shared real-key acceptance with exact receipt and bounded failure evidence. */
    private async Task KeyCommand(Main main, Key key, ulong previousSequence,
        string directory, string prefix, Func<Snapshot, bool> settled, string expected)
    {
        var trace = new List<AcceptanceCommandSample>();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var droppedSamples = 0;
        var passed = false;
        void Observe(string stage, bool force = false)
        {
            var snapshot = main.ObservedSnapshot;
            var player = snapshot?.Players.FirstOrDefault();
            var result = main.ObservedCommandResult;
            var state = new AcceptanceCommandState(main.TransportConnected, main.ObservedPendingCommandCount,
                main.ObservedLastQueuedSequence, main.ObservedSnapshotRevision, snapshot?.Tick,
                player?.EntityId, player?.PositionX, player?.PositionY, player?.CombatMode.ToString(),
                player?.CurrentAction?.ToString(), player?.HostileMode,
                result?.ClientSequence, result?.Status.ToString(), result?.RejectReason.ToString(),
                result?.AppliedTick, result?.ServerReceiptSequence);
            if (!force && trace.LastOrDefault()?.State == state)
                return;
            // Keep the initial boundary and latest observations even on timeout.
            if (trace.Count == 128) {
                trace.RemoveAt(1);
                droppedSamples++;
            }
            trace.Add(new AcceptanceCommandSample(elapsed.Elapsed.TotalMilliseconds, stage, state));
        }
        Observe("BeforeInput", force: true);
        try {
            await PressAcceptanceKey(key);
            Observe("AfterInput", force: true);
            await Until(() => {
                Observe("AwaitingOutcome");
                var result = main.ObservedCommandResult;
                if (result is not null && result.ClientSequence > previousSequence) {
                    Require(result.ClientSequence == previousSequence + 1, "Input advanced an unexpected command sequence.");
                    Require(IsAcceptedOrSuccessfulDuplicate(result), $"Input {key} rejected: {result}.");
                }
                var snapshot = main.ObservedSnapshot;
                return result is not null && result.ClientSequence == previousSequence + 1
                    && main.ObservedLastQueuedSequence == result.ClientSequence && main.ObservedPendingCommandCount == 0
                    && snapshot is not null && snapshot.Tick >= result.AppliedTick && settled(snapshot);
            }, $"Acceptance input {key} did not settle: {expected}", 10);
            passed = true;
        } catch (Exception exception) {
            Observe("Failure", force: true);
            throw new InvalidOperationException($"Input {key}: previous sequence {previousSequence}; "
                + $"{trace.Last().State}; expected {expected}", exception);
        } finally {
            Observe(passed ? "Passed" : "Failed", force: true);
            // Only bounded observations: never bookmarks, credentials or raw snapshots.
            WriteEvidence(directory, $"{prefix}-command-{previousSequence + 1}.json", new {
                Passed = passed, Key = key.ToString(), PreviousSequence = previousSequence, Expected = expected,
                DroppedSamples = droppedSamples, Samples = trace,
            });
        }
    }

    private async Task PressAcceptanceKey(Key key)
    {
        GetViewport().GuiReleaseFocus();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        await Frame();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Frame();
    }

    private static MultiplayerPlayerState ObservePlayer(PlayerSnapshot player)
        => new(player.CharacterName, player.CharacterClassId, player.LevelId, player.PositionX, player.PositionY,
            player.Gold, player.HostileMode);

    private async Task VerifyMultiplayerRecoveryAsync(Main main, string directory, string prefix, int slot,
        MultiplayerEvidence previous, AuthoritativeServerIdentity identity)
    {
        var rootDirectory = System.IO.Directory.GetParent(directory)!.FullName;
        var peerSlot = 3 - slot;
        var peerDirectory = System.IO.Path.Combine(rootDirectory, $"player-{peerSlot}");
        var status = (Label)main.FindChild("ConnectionStatus", true, false);
        var baseline = main.ObservedSnapshot
            ?? throw new InvalidOperationException("Recovery did not receive a complete roster snapshot.");
        Require(baseline.Players.Count == 2, "Recovery requires the complete two-player roster before outage.");
        var localBefore = baseline.Players.Single(player => player.CharacterName == $"M1-Player-{slot}");
        var peerBefore = baseline.Players.Single(player => player.CharacterName == $"M1-Player-{peerSlot}");
        Require(localBefore.EntityId == previous.EntityId && peerBefore.EntityId == previous.PeerEntityId,
            "Recovery changed a durable local or peer entity binding before the outage.");
        Require(ObservePlayer(localBefore) == previous.Local && ObservePlayer(peerBefore) == previous.Remote,
            "Recovery changed the complete durable roster before the outage.");
        Require(previous.ReceiptSequence > 0, "The resume phase did not publish a canonical command receipt.");
        Require(main.ObservedClockGatedScheduling, "Recovery requires clock-gated authoritative scheduling.");

        WriteEvidence(directory, prefix + "-ready.json", new {
            Ready = true, Slot = slot, EntityId = localBefore.EntityId, PeerEntityId = peerBefore.EntityId,
            Tick = baseline.Tick, SnapshotFingerprint = SnapshotFingerprint(baseline),
            Sequence = previous.Sequence, ReceiptSequence = previous.ReceiptSequence,
            ContentHash = identity.ContentManifestHash, RulesetHash = identity.RulesetIdentityHash,
            ClockGated = main.ObservedClockGatedScheduling,
        });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(rootDirectory, prefix + "-server-stopped.json"))
            && !main.TransportConnected, "The owned complete-cohort host did not stop or this client did not lose transport.");
        await Until(() => status.Text.Contains("Connection lost") && status.Text.Contains("reconnecting"),
            "The client did not show explicit multiplayer disconnect/reconnect feedback.");

        var disconnected = main.ObservedSnapshot
            ?? throw new InvalidOperationException("The disconnected client lost its last authoritative roster.");
        Require(disconnected.Players.Count == 2, "The disconnected snapshot did not retain the complete roster.");
        var disconnectedFingerprint = SnapshotFingerprint(disconnected);
        var disconnectedRevision = main.ObservedSnapshotRevision;
        var disconnectedLastQueued = main.ObservedLastQueuedSequence;
        var disconnectedResultFingerprint = CommandFingerprint(main.ObservedCommandResult);
        var disconnectedScreen = main.ObservedPlayerScreenPosition
            ?? throw new InvalidOperationException("The disconnected client lost its last rendered local player.");
        var disconnectedPeerScreen = main.ObservedEntityScreenPosition(peerBefore.EntityId)
            ?? throw new InvalidOperationException("The disconnected client lost its last rendered peer.");
        var offline = System.Diagnostics.Stopwatch.StartNew();
        while (offline.Elapsed < TimeSpan.FromSeconds(2)) {
            await PressAcceptanceKey(slot == 1 ? Key.Down : Key.Up);
            Require(!main.TransportConnected, "The complete-cohort host restarted before both offline barriers completed.");
            Require(SnapshotFingerprint(main.ObservedSnapshot!) == disconnectedFingerprint
                && main.ObservedSnapshotRevision == disconnectedRevision,
                "The authoritative roster or snapshot revision changed while the host was offline.");
            Require(main.ObservedPendingCommandCount == 0
                && main.ObservedLastQueuedSequence == disconnectedLastQueued
                && CommandFingerprint(main.ObservedCommandResult) == disconnectedResultFingerprint,
                "Disconnected actual input changed pending, queued, or receipt state.");
            Require(main.ObservedPlayerScreenPosition is { } screen
                && screen.DistanceTo(disconnectedScreen) < 0.01f,
                "Disconnected actual input moved the rendered player.");
            Require(main.ObservedEntityScreenPosition(peerBefore.EntityId) is { } offlinePeerScreen
                && offlinePeerScreen.DistanceTo(disconnectedPeerScreen) < 0.01f,
                "Disconnected actual input moved the rendered peer.");
            Require(status.Text.Contains("Connection lost"), "Multiplayer disconnect feedback disappeared offline.");
        }
        WriteEvidence(directory, prefix + "-offline-ready.json", new {
            Passed = true, Slot = slot, OfflineSeconds = offline.Elapsed.TotalSeconds,
            SnapshotFingerprint = disconnectedFingerprint, SnapshotRevision = disconnectedRevision,
            Tick = disconnected.Tick, Sequence = previous.Sequence, ReceiptSequence = previous.ReceiptSequence,
        });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(rootDirectory, prefix + "-server-restarted.json")),
            "The launcher did not restart the complete-cohort host after both offline barriers.");

        await Until(() => main.TransportConnected && main.ObservedSnapshotRevision > disconnectedRevision,
            "This Godot client did not reconnect to the replacement complete-cohort host.");
        var reconnected = main.ObservedSnapshot
            ?? throw new InvalidOperationException("Reconnect did not publish a complete roster snapshot.");
        Require(main.ObservedIdentity == identity && main.ObservedClockGatedScheduling,
            "Reconnect changed the authoritative identity or scheduling epoch.");
        Require(reconnected.Players.Count == 2
            && reconnected.Players.Select(player => player.EntityId).Distinct().Count() == 2,
            "Reconnect did not retain exactly two distinct roster entities.");
        var localAfter = reconnected.Players.Single(player => player.CharacterName == $"M1-Player-{slot}");
        var peerAfter = reconnected.Players.Single(player => player.CharacterName == $"M1-Player-{peerSlot}");
        Require(localAfter.EntityId == localBefore.EntityId && peerAfter.EntityId == peerBefore.EntityId,
            "Reconnect allocated duplicate or unstable player entities.");
        Require(ObservePlayer(localAfter) == ObservePlayer(localBefore)
            && ObservePlayer(peerAfter) == ObservePlayer(peerBefore),
            "Reconnect changed durable roster state while no command was accepted offline.");
        Require(reconnected.Tick >= disconnected.Tick,
            "Reconnect returned a snapshot older than the disconnected authoritative tick.");

        WriteEvidence(directory, prefix + "-rejoined.json", new {
            Rejoined = true, Slot = slot, Tick = reconnected.Tick,
            EntityId = localAfter.EntityId, PeerEntityId = peerAfter.EntityId,
            SnapshotFingerprint = SnapshotFingerprint(reconnected),
        });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-rejoined.json")),
            "The peer did not verify its restored complete roster before post-reconnect input.");

        var previousSequence = previous.Sequence;
        await KeyCommand(main, Key.Right, previousSequence, directory, prefix,
            snapshot => snapshot.Players.Single(player => player.EntityId == localAfter.EntityId) is {
                    CurrentAction: null, CombatMode: PlayerCombatMode.Stand } moved
                && moved.EntityId == localAfter.EntityId && moved.PositionX == localAfter.PositionX + 1
                && moved.PositionY == localAfter.PositionY,
            "Post-reconnect actual control settled for the same durable local entity.");
        var result = main.ObservedCommandResult!;
        Require(result.ClientSequence == previousSequence + 1 && IsAcceptedOrSuccessfulDuplicate(result)
            && result.ServerReceiptSequence > previous.ReceiptSequence
            && result.AppliedTick >= reconnected.Tick && result.AppliedTick > previous.AppliedTick,
            "Post-reconnect control did not produce a new accepted receipt.");
        var movedLocal = main.ObservedSnapshot!.Players.Single(player => player.EntityId == localAfter.EntityId);
        WriteEvidence(directory, prefix + "-moved.json", new MultiplayerMoveEvidence(
            ObservePlayer(movedLocal), movedLocal.EntityId, result.ServerReceiptSequence, result.AppliedTick));
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-moved.json")),
            "The peer did not complete its accepted post-reconnect control.");
        var peerMove = JsonSerializer.Deserialize<MultiplayerMoveEvidence>(System.IO.File.ReadAllText(
            System.IO.Path.Combine(peerDirectory, prefix + "-moved.json")))
            ?? throw new InvalidDataException("Peer post-reconnect evidence is empty.");
        Require(peerMove.ReceiptSequence > previous.ReceiptSequence && peerMove.AppliedTick >= reconnected.Tick,
            "The peer did not publish a post-reconnect accepted receipt.");
        await Until(() => main.ObservedSnapshot!.Players.Single(player => player.EntityId == peerBefore.EntityId) is { } peer
            && ObservePlayer(peer) == peerMove.Player,
            "The peer's accepted post-reconnect movement was not visible here.");
        Require(ObservePlayer(main.ObservedSnapshot!.Players.Single(player => player.EntityId == localAfter.EntityId))
            == ObservePlayer(movedLocal), "The peer's post-reconnect control changed this player's settled state.");
        await Until(() => main.ObservedEntityScreenPosition(peerMove.EntityId) is { } peerScreen
            && peerScreen.DistanceTo(main.ObservedTileScreenPosition(peerMove.Player.X, peerMove.Player.Y)) < 1,
            "The peer's accepted post-reconnect movement was not visible on the actual world.");
        var world = (Control)main.FindChild("WorldArea", true, false);
        Require(world.GetGlobalRect().HasPoint(main.ObservedPlayerScreenPosition!.Value)
            && world.GetGlobalRect().HasPoint(main.ObservedEntityScreenPosition(peerMove.EntityId)!.Value),
            "The post-reconnect local or remote player was clipped outside the world area.");
        var visible = DisplayServer.GetName() != "headless";
        if (visible)
            await Capture(System.IO.Path.Combine(directory, prefix + "-players.png"));
        WriteEvidence(directory, prefix + ".json", new MultiplayerRecoveryEvidence(true,
            identity.GameMode.ToString(), identity.ContentManifestHash, identity.RulesetIdentityHash,
            slot, localAfter.EntityId, peerMove.EntityId, ObservePlayer(movedLocal), peerMove.Player,
            result.ClientSequence, result.ServerReceiptSequence, result.AppliedTick, reconnected.Tick,
            disconnectedFingerprint, true, visible));
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + ".json")),
            "The peer did not finish its shared post-reconnect visibility assertions.");
    }

    private static string SnapshotFingerprint(Snapshot snapshot)
        => Convert.ToHexString(SHA256.HashData(snapshot.ToByteArray()));

    private static string CommandFingerprint(CommandResult? result)
        => result is null ? string.Empty : Convert.ToHexString(SHA256.HashData(result.ToByteArray()));

    private static MultiplayerEvidence ReadMultiplayerEvidence(string directory, string name)
        => JsonSerializer.Deserialize<MultiplayerEvidence>(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, name)))
            ?? throw new InvalidDataException("Multiplayer evidence is empty.");

    private sealed record MultiplayerPlayerState(string Name, uint ClassId, uint LevelId, int X, int Y, uint Gold, bool Hostile);
    private sealed record AcceptanceCommandSample(double Milliseconds, string Stage, AcceptanceCommandState State);
    private sealed record AcceptanceCommandState(bool Connected, int Pending, ulong LastQueuedSequence,
        ulong SnapshotRevision, ulong? SnapshotTick, uint? EntityId, int? X, int? Y, string? CombatMode,
        string? Action, bool? Hostile, ulong? ResultSequence, string? Status, string? RejectReason,
        ulong? AppliedTick, ulong? ReceiptSequence);
    private sealed record MultiplayerMoveEvidence(MultiplayerPlayerState Player, uint EntityId,
        ulong ReceiptSequence = 0, ulong AppliedTick = 0);
    private sealed record MultiplayerEvidence(bool Passed, string Mode, string ContentHash, string RulesetHash, int Slot,
        uint EntityId, uint PeerEntityId, MultiplayerPlayerState Local, MultiplayerPlayerState Remote,
        ulong Sequence, ulong ReceiptSequence, ulong AppliedTick, bool Visible);
    private sealed record MultiplayerRecoveryEvidence(bool Passed, string Mode, string ContentHash,
        string RulesetHash, int Slot, uint EntityId, uint PeerEntityId, MultiplayerPlayerState Local,
        MultiplayerPlayerState Remote, ulong Sequence, ulong ReceiptSequence, ulong AppliedTick,
        ulong ReconnectTick, string DisconnectedSnapshotFingerprint, bool ClockGated, bool Visible);
}
