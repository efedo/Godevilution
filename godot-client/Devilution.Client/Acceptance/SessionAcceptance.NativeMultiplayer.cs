using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;
using Environment = System.Environment;

namespace Devilution.Client.Acceptance;

/**
 * Batches 1.5a/1.5b: two real clients play one native multiplayer run. Slot 1 creates the
 * Warrior that generates the run; slot 2 joins as a Rogue. Create meets on L1, Journey also
 * fights there, and Resume restores both on a fresh host. Shared single-player helpers assume
 * one listed player, so this file selects the local player by name.
 * Batch 2.2b: with GODEVILUTION_NATIVE_MULTIPLAYER_TARGET=5 both join as test-participant
 * Warriors (the participant is class-bound) and meet on L5 through the clvl 8 Town warp.
 */
public partial class SessionAcceptance
{
    private sealed record NativeMultiplayerCheckpoint(uint EntityId, uint ClassId, int X, int Y, ulong Experience,
        uint PeerEntityId, int PeerX, int PeerY, ulong Sequence);

    private async Task RunNativeMultiplayerAsync(string directory, string phase)
    {
        Require(int.TryParse(Environment.GetEnvironmentVariable("GODEVILUTION_LOCAL_PLAYER"), out var slot)
            && slot is 1 or 2, "Native multiplayer acceptance requires player slot 1 or 2.");
        var peerSlot = 3 - slot;
        var peerDirectory = System.IO.Path.Combine(System.IO.Directory.GetParent(directory)!.FullName, $"player-{peerSlot}");
        var prefix = phase.ToLowerInvariant();
        var resuming = phase == "NativeMultiplayerResume";
        var target = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_MULTIPLAYER_TARGET") is { Length: > 0 } value
            ? uint.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : 1U;
        Require(target is 1 or 5, "Native multiplayer acceptance targets L1 or L5.");
        var arrived = $"{prefix}-l{target}.json";
        var classId = slot == 1 || target == 5 ? 0U : 1U;
        var peerClassId = slot == 2 || target == 5 ? 0U : 1U;
        var name = $"Native-MP-{slot}";
        var peerName = $"Native-MP-{peerSlot}";
        NativeMultiplayerCheckpoint? saved = null;
        if (resuming) {
            using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
                System.IO.Path.Combine(directory, "nativemultiplayerjourney.json")));
            Require(document.RootElement.GetProperty("Passed").GetBoolean(), "Resume requires passing journey evidence.");
            saved = System.Text.Json.JsonSerializer.Deserialize<NativeMultiplayerCheckpoint>(
                document.RootElement.GetProperty("Checkpoint").GetRawText())!;
        }
        Require(resuming == System.IO.File.Exists(System.IO.Path.Combine(directory, "resume-token.txt")),
            "Creation needs a fresh bookmark; resume needs this player's bookmark.");
        // The run's generating participant connects first in every phase.
        if (slot == 2)
            await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, arrived)),
                $"The first participant did not reach L{target}.", 300);
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        timingObservedMain = main;
        AddChild(main);
        await Frame();
        await Frame();
        var setup = main.GetNode<CharacterSetupPresentation>("CharacterSetup");
        Require(setup.Visible && !main.TransportConnected, "Each participant must enter through the real setup form.");
        Require(((OptionButton)setup.FindChild("SessionMode", true, false)).Selected == (resuming ? 1 : 0),
            "The participant's bookmark did not select the expected session mode.");
        if (!resuming) {
            ((LineEdit)setup.FindChild("CharacterName", true, false)).Text = name;
            var selector = (OptionButton)setup.FindChild("CharacterClass", true, false);
            selector.Select(selector.GetItemIndex((int)classId));
        }
        await Click(((Button)setup.FindChild("Connect", true, false)).GetGlobalRect().GetCenter());
        await Until(() => main.TransportConnected && main.ObservedSnapshot?.Players.Any(player => player.CharacterName == name) == true,
            "No authoritative native multiplayer snapshot arrived.", 120);
        var identity = main.ObservedIdentity!;
        var local = NativeMultiplayerLocal(main, name);
        RegisterJourneySafety(main, local.EntityId);
        Require(identity.GameMode == GameMode.Multiplayer && local.CharacterClassId == classId && local.Life > 0,
            "The participant did not enter native multiplayer as its selected class.");
        if (resuming) {
            var hello = main.ObservedInitialConnection?.ServerHello;
            Require(local.EntityId == saved!.EntityId && local.LevelId == target && local.PositionX == saved.X && local.PositionY == saved.Y
                && local.Experience == saved.Experience && hello is { ResumedSessionMatched: true }
                && hello.LastClientSequence == saved.Sequence,
                $"The fresh host did not restore the participant's L{target} state: {local.EntityId}/{local.LevelId}/{local.PositionX},{local.PositionY}/{local.Experience}, cursor {hello?.LastClientSequence}; saved {saved}.");
        } else {
            Require(local.LevelId == 0, "The participant must start in Town.");
            if (target == 5)
                Require(local.CharacterLevel >= 8, "The L5 Town warp needs a clvl 8 test participant.");
            await NativeMultiplayerTravelAsync(main, name, target);
        }
        local = NativeMultiplayerLocal(main, name);
        var monsters = main.ObservedSnapshot!.Monsters.Where(monster => monster.LevelId == target).ToArray();
        Require(monsters.Length > 0, $"Native multiplayer L{target} has no generated monsters.");
        WriteEvidence(directory, arrived, new { Slot = slot, local.EntityId });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, arrived))
            && main.ObservedSnapshot!.Players.Count == 2 && main.ObservedSnapshot.Players.All(player => player.LevelId == target),
            $"Both participants did not become visible together on L{target}.", 300);
        var peer = main.ObservedSnapshot!.Players.Single(player => player.CharacterName == peerName);
        Require(peer.EntityId != local.EntityId && peer.CharacterClassId == peerClassId,
            "The peer participant's identity or class is wrong.");
        if (resuming)
            Require(peer.EntityId == saved!.PeerEntityId && peer.PositionX == saved.PeerX && peer.PositionY == saved.PeerY,
                $"The fresh host did not restore the peer's L{target} position.");
        // Native Catacombs drawing arrives in batch 2.3.
        var peerDraw = target == 1 ? await NativeMultiplayerPeerDrawAsync(main, directory, prefix, local.EntityId, peer.EntityId) : null;
        object? fight = null;
        // L5 fights wait for Catacombs monster AI and doors (batches 2.4/2.5); L5 is a travel and resume smoke.
        if (phase == "NativeMultiplayerJourney" && target == 1) {
            // One participant fights at a time, so neither attack races the other's target.
            if (slot == 2)
                await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-fought.json")),
                    "The first participant did not finish its fight.", 600, healWhileWaiting: true);
            fight = await NativeMultiplayerFightAsync(main, name, target);
            WriteEvidence(directory, prefix + "-fought.json", new { Fought = true });
            await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-fought.json")),
                "The peer did not finish its fight.", 600, healWhileWaiting: true);
        }
        if (resuming) {
            // The restored participant accepts the exact next ordinary command.
            var first = await NativeMultiplayerStepAsync(main, name);
            Require(first == saved!.Sequence + 1, $"The resumed participant's first command took sequence {first}, not {saved.Sequence + 1}.");
        }
        await WaitForJourneySettlementAsync(main, "The participant before its checkpoint", 30);
        WriteEvidence(directory, prefix + "-settled.json", new { Settled = true });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-settled.json")),
            "The peer did not settle.", 120, healWhileWaiting: true);
        var final = main.ObservedSnapshot!;
        local = final.Players.Single(player => player.CharacterName == name);
        peer = final.Players.Single(player => player.CharacterName == peerName);
        var checkpoint = new NativeMultiplayerCheckpoint(local.EntityId, classId, local.PositionX, local.PositionY,
            local.Experience, peer.EntityId, peer.PositionX, peer.PositionY, main.ObservedLastQueuedSequence);
        WriteEvidence(directory, prefix + ".json", new {
            Passed = true, Mode = identity.GameMode.ToString(), ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash, Slot = slot, ClassId = classId, local.EntityId,
            PeerEntityId = peer.EntityId, PeerClassId = peer.CharacterClassId, LevelId = local.LevelId,
            local.CharacterLevel, Monsters = monsters.Length, MaxMonsterHitPointsQ6 = monsters.Max(monster => monster.HitPointsQ6),
            PeerDraw = peerDraw, Fight = fight, Checkpoint = checkpoint,
        });
        // Keep both scenes connected until each has written its evidence.
        WriteEvidence(directory, prefix + "-done.json", new { Done = true });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-done.json")),
            "The peer did not finish.", 120);
    }

    private static PlayerSnapshot NativeMultiplayerLocal(Main main, string name)
        => main.ObservedSnapshot!.Players.Single(player => player.CharacterName == name);

    /**
     * Waits until L1 draws both this participant and its peer, with native player art when a
     * player pack is configured, and saves a capture when the client has a window.
     */
    private async Task<object> NativeMultiplayerPeerDrawAsync(Main main, string directory, string prefix, uint localId, uint peerId)
    {
        var playerArt = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_PLAYER_PACK"));
        bool Drawn(uint entityId) => main.ObservedNativePlayerDrawingFor(entityId) is { } draw
            && (!playerArt || draw is { IsLoaded: true, UsedFallback: false });
        try {
            await Until(() => main.ObservedNativeSceneDrawing is { NativeSceneActive: true, LevelId: 1 } && Drawn(localId) && Drawn(peerId),
                "L1 did not draw both multiplayer participants.", 30);
        } catch (InvalidOperationException failure) {
            throw new InvalidOperationException($"{failure.Message} Scene {main.ObservedNativeSceneDrawing}; "
                + $"local {main.ObservedNativePlayerDrawingFor(localId)}; peer {main.ObservedNativePlayerDrawingFor(peerId)}; "
                + string.Join(", ", main.ObservedSnapshot!.Players.Select(player => $"{player.EntityId}@{player.LevelId}:{player.PositionX},{player.PositionY}")), failure);
        }
        string? capture = null;
        if (NativeWarriorVisible) {
            capture = prefix + "-peers.png";
            await Capture(System.IO.Path.Combine(directory, capture));
        }
        var peer = main.ObservedNativePlayerDrawingFor(peerId)!;
        return new { NativeArt = playerArt, PeerClip = peer.ClipKey, PeerFallback = peer.UsedFallback, Capture = capture };
    }

    /** Travels the Town route to L1 (Cathedral stairs) or L5 (the multiplayer Town warp). */
    private async Task NativeMultiplayerTravelAsync(Main main, string name, uint target)
    {
        await WaitForJourneySettlementAsync(main, "The native multiplayer Town");
        var routes = main.ObservedSnapshot!.LevelGeometry.Single(level => level.LevelId == 0)
            .WarpTriggers.OrderBy(route => route.WarpId).ToArray();
        var index = Array.FindIndex(routes, route => route.Available && route.TargetLevelId == target);
        Require(index >= 0, $"Native multiplayer Town did not publish its route to L{target}.");
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        await SelectTab(sections, 4);
        await SelectRoute(main, (OptionButton)main.FindChild("Routes", true, false), index);
        var cancel = (Button)main.FindChild("CancelTravel", true, false);
        // Travel stops when its path is blocked (Town residents walk); restart it from the new position.
        for (var attempt = 0; attempt < 6; attempt++) {
            await ClickControl(main, sections, 4, "Travel");
            await Until(() => !cancel.Disabled || NativeMultiplayerLocal(main, name).LevelId == target,
                "The real Travel button did not start route input.", 5);
            await Until(() => {
                var player = NativeMultiplayerLocal(main, name);
                Require(player.Life > 0, $"The participant died walking to L{target}.");
                return (player.LevelId == target || cancel.Disabled) && main.ObservedPendingCommandCount == 0
                    && AcceptanceCommandBoundary.HasLatestAppliedReceipt(main.ObservedSnapshot!.Tick,
                        main.ObservedCommandResult, main.ObservedLastQueuedSequence);
            }, $"The participant's travel neither stopped nor reached L{target}.", 120,
                beforePoll: () => GuardJourneyTravelAsync(main, 0));
            if (NativeMultiplayerLocal(main, name).LevelId == target) {
                await Until(() => main.ObservedPendingCommandCount == 0
                    && AcceptanceCommandBoundary.HasLatestAppliedReceipt(main.ObservedSnapshot!.Tick,
                        main.ObservedCommandResult, main.ObservedLastQueuedSequence),
                    $"Travel commands did not drain on L{target}.", 15);
                return;
            }
            var stopped = NativeMultiplayerLocal(main, name);
            GD.Print($"Native multiplayer travel stopped at {stopped.PositionX},{stopped.PositionY}: "
                + $"{((Label)main.FindChild("TravelStatus", true, false)).Text}; {main.ObservedCommandResult}");
            await WaitForJourneySettlementAsync(main, "The stopped native multiplayer travel", 15);
        }
        throw new InvalidOperationException($"The participant did not reach native multiplayer L{target} after six travel attempts.");
    }

    /** Ordinary adjacent attacks on the nearest monster of the level until this participant earns experience. */
    private async Task<object> NativeMultiplayerFightAsync(Main main, string name, uint levelId)
    {
        var before = NativeMultiplayerLocal(main, name);
        var attacks = 0;
        for (var work = 0; work < 200; work++) {
            await JourneySafetyCheckpointAsync(main, JourneySafetyActivity.Combat);
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single(candidate => candidate.CharacterName == name);
            Require(player.Life > 0 && player.LevelId == levelId, $"The participant died or left L{levelId} while fighting.");
            if (player.Experience > before.Experience)
                return new { Attacks = attacks, ExperienceBefore = before.Experience, ExperienceAfter = player.Experience };
            var target = snapshot.Monsters.Where(monster => monster.Alive && !monster.IsPlayerMinion && monster.LevelId == levelId)
                .OrderBy(monster => NativeMonsterDistance(player, monster)).ThenBy(monster => monster.EntityId).FirstOrDefault();
            Require(target is not null, $"No living L{levelId} monster remains to fight.");
            var sequence = main.ObservedLastQueuedSequence;
            if (NativeDistance(player.PositionX, player.PositionY, target!.PositionX, target.PositionY) <= 1) {
                await ClickWorld(main, target.PositionX, target.PositionY, MouseButton.Right);
                attacks++;
            } else {
                var geometry = DepthGeometry(main, levelId);
                var step = FindNativeClassApproachStep(main, snapshot, player, geometry,
                    (x, y) => NativeDistance(x, y, target.PositionX, target.PositionY) <= 1);
                if (step is null) {
                    await Frame();
                    continue;
                }
                await Click(main.ObservedTileScreenPosition(step.Value.X, step.Value.Y), walk: true);
            }
            await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence > sequence
                && main.ObservedSnapshot!.Tick >= result.AppliedTick,
                $"A native multiplayer fight command got no receipt: queued {main.ObservedLastQueuedSequence} after {sequence}, last {main.ObservedCommandResult}.", 15);
            await WaitForJourneySettlementAsync(main, "Native multiplayer combat action boundary");
        }
        throw new InvalidOperationException("The participant earned no experience within its fight budget.");
    }

    /**
     * One accepted ordinary walk step to an open neighbouring tile; returns the first attempt's sequence.
     * A clock-gated TooLate refusal changes nothing, so it is repeated.
     */
    private async Task<ulong> NativeMultiplayerStepAsync(Main main, string name)
    {
        ulong? first = null;
        for (var attempt = 0; attempt < 5; attempt++) {
            var receipt = await NativeMultiplayerStepOnceAsync(main, name);
            first ??= receipt.ClientSequence;
            if (receipt is { Status: CommandStatus.Rejected, RejectReason: CommandRejectReason.TooLate })
                continue;
            Require(IsAcceptedOrSuccessfulDuplicate(receipt), $"The resumed participant's step was refused: {receipt}.");
            return first.Value;
        }
        throw new InvalidOperationException("The resumed participant's step stayed TooLate.");
    }

    private async Task<CommandResult> NativeMultiplayerStepOnceAsync(Main main, string name)
    {
        var snapshot = main.ObservedSnapshot!;
        var player = snapshot.Players.Single(candidate => candidate.CharacterName == name);
        var geometry = DepthGeometry(main, player.LevelId);
        var step = FindNativeClassApproachStep(main, snapshot, player, geometry,
            (x, y) => NativeDistance(x, y, player.PositionX, player.PositionY) == 1
                && !snapshot.Players.Any(other => other.PositionX == x && other.PositionY == y));
        Require(step is not null, "The resumed participant has no open neighbouring tile.");
        var sequence = main.ObservedLastQueuedSequence;
        await Click(main.ObservedTileScreenPosition(step!.Value.X, step.Value.Y), walk: true);
        await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence > sequence
            && main.ObservedSnapshot!.Tick >= result.AppliedTick,
            $"The resumed participant's step got no receipt: queued {main.ObservedLastQueuedSequence} after {sequence}, last {main.ObservedCommandResult}.", 15);
        return main.ObservedCommandResult!;
    }
}
