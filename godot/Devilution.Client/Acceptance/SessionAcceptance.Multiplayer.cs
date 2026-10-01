using System.Text.Json;
using Devilution.Protocol.V1;
using Godot;
using Environment = System.Environment;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    /** Two independent real scenes coordinate observations, never gameplay state. */
    private async Task RunMultiplayerAsync(string directory, string phase)
    {
        Require(int.TryParse(Environment.GetEnvironmentVariable("GODEVILUTION_LOCAL_PLAYER"), out var slot)
            && slot is 1 or 2, "Multiplayer acceptance requires player slot 1 or 2.");
        var peerSlot = 3 - slot;
        var peerDirectory = System.IO.Path.Combine(System.IO.Directory.GetParent(directory)!.FullName, $"player-{peerSlot}");
        var prefix = phase.ToLowerInvariant();
        var creating = phase == "MultiplayerCreate";
        // Reverse the connection order at restart: entity allocation must not be
        // confused with durable character/token identity or command ownership.
        var firstSlot = creating ? 1 : 2;
        MultiplayerEvidence? previous = creating ? null : ReadMultiplayerEvidence(directory, "multiplayercreate.json");
        Require(creating != System.IO.File.Exists(System.IO.Path.Combine(directory, "resume-token.txt")),
            "Creation needs a fresh bookmark; resume needs this player's existing bookmark.");
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        await Frame();
        await Frame();
        var setup = main.GetNode<CharacterSetupPresentation>("CharacterSetup");
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
        if (slot != firstSlot)
            await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-ready.json")),
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
        }
        if (creating && slot == firstSlot) {
            await MultiplayerMove(main, Key.Down, 0, 1, 0, directory, prefix);
            await MultiplayerMove(main, Key.Down, 0, 1, 1, directory, prefix);
        }
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(directory, "resume-token.txt")),
            "The player-specific resume bookmark was not persisted.");
        WriteEvidence(directory, prefix + "-ready.json", new { Ready = true, Slot = slot });
        await Until(() => main.ObservedSnapshot!.Players.Count == 2,
            "The second authoritative player did not become visible.");
        var pair = main.ObservedSnapshot!.Players;
        local = pair.First();
        var remote = pair.Last();
        Require(local.EntityId != remote.EntityId && remote.CharacterName == $"M1-Player-{peerSlot}",
            "The two player identities are duplicated or swapped.");
        if (previous is not null)
            Require(ObservePlayer(remote) == previous.Remote, "The peer's durable state was not restored.");
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
            localState, peerMove.Player, sequence + 2, visible));
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

    private static MultiplayerEvidence ReadMultiplayerEvidence(string directory, string name)
        => JsonSerializer.Deserialize<MultiplayerEvidence>(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, name)))
            ?? throw new InvalidDataException("Multiplayer evidence is empty.");

    private sealed record MultiplayerPlayerState(string Name, uint ClassId, uint LevelId, int X, int Y, uint Gold, bool Hostile);
    private sealed record AcceptanceCommandSample(double Milliseconds, string Stage, AcceptanceCommandState State);
    private sealed record AcceptanceCommandState(bool Connected, int Pending, ulong LastQueuedSequence,
        ulong SnapshotRevision, ulong? SnapshotTick, uint? EntityId, int? X, int? Y, string? CombatMode,
        string? Action, bool? Hostile, ulong? ResultSequence, string? Status, string? RejectReason,
        ulong? AppliedTick, ulong? ReceiptSequence);
    private sealed record MultiplayerMoveEvidence(MultiplayerPlayerState Player, uint EntityId);
    private sealed record MultiplayerEvidence(bool Passed, string Mode, string ContentHash, string RulesetHash, int Slot,
        uint EntityId, uint PeerEntityId, MultiplayerPlayerState Local, MultiplayerPlayerState Remote, ulong Sequence, bool Visible);
}
