using System.Text.Json;
using Devilution.Protocol.V1;
using Godot;
using Environment = System.Environment;

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
            await MultiplayerMove(main, Key.Down, 0, 1, 0);
            await MultiplayerMove(main, Key.Down, 0, 1, 1);
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
        await MultiplayerMove(main, Key.Right, 1, 0, sequence);
        await PressMultiplayerKey(Key.F);
        await Until(() => main.ObservedCommandResult is { Status: CommandStatus.Accepted } result
            && result.ClientSequence == sequence + 2 && main.ObservedPendingCommandCount == 0
            && main.ObservedSnapshot!.Players.First().HostileMode != before.Hostile,
            "Friendly-mode input did not change only the local authoritative player.");
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

    private async Task MultiplayerMove(Main main, Key key, int x, int y, ulong previousSequence)
    {
        var before = main.ObservedSnapshot!.Players.First();
        await PressMultiplayerKey(key);
        try {
            await Until(() => {
                var result = main.ObservedCommandResult;
                if (result is not null && result.ClientSequence > previousSequence)
                    Require(result.Status == CommandStatus.Accepted, $"Movement {key} rejected: {result}.");
                return result is not null && result.ClientSequence == previousSequence + 1 && main.ObservedPendingCommandCount == 0
                    && main.ObservedSnapshot!.Tick >= result.AppliedTick
                    && main.ObservedSnapshot.Players.First() is { CurrentAction: null, CombatMode: PlayerCombatMode.Stand } moved
                    && moved.EntityId == before.EntityId && moved.PositionX == before.PositionX + x && moved.PositionY == before.PositionY + y;
            }, "Multiplayer movement did not settle.", 10);
        } catch (Exception exception) {
            throw new InvalidOperationException($"Movement {key}: previous sequence {previousSequence}; "
                + $"connected {main.TransportConnected}; pending {main.ObservedPendingCommandCount}; result {main.ObservedCommandResult}; "
                + $"local {ObservePlayer(main.ObservedSnapshot!.Players.First())}; expected ({before.PositionX + x},{before.PositionY + y}).", exception);
        }
    }

    private async Task PressMultiplayerKey(Key key)
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
    private sealed record MultiplayerMoveEvidence(MultiplayerPlayerState Player, uint EntityId);
    private sealed record MultiplayerEvidence(bool Passed, string Mode, string ContentHash, string RulesetHash, int Slot,
        uint EntityId, uint PeerEntityId, MultiplayerPlayerState Local, MultiplayerPlayerState Remote, ulong Sequence, bool Visible);
}
