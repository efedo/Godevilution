using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;
using Environment = System.Environment;
using static Devilution.Client.Protocol.AuthoritativeCommandAckValidator;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    // This is deliberately a separate opt-in acceptance profile. It uses the
    // authored town-loop-preview encounter through real controls and records
    // only observations which the peer and the fresh host can independently
    // verify.
    private async Task RunCooperativeAsync(string directory, string phase)
    {
        Require(Environment.GetEnvironmentVariable("GODEVILUTION_PERSISTENCE_MODE") == "cohort",
            "Cooperative acceptance requires complete-cohort persistence.");
        Require(int.TryParse(Environment.GetEnvironmentVariable("GODEVILUTION_LOCAL_PLAYER"), out var slot)
            && slot is 1 or 2, "Cooperative acceptance requires player slot 1 or 2.");

        var creating = phase == "CooperativeJourney";
        var prefix = phase.ToLowerInvariant();
        var peerSlot = 3 - slot;
        var peerDirectory = System.IO.Path.Combine(System.IO.Directory.GetParent(directory)!.FullName, $"player-{peerSlot}");
        CooperativeEvidence? previous = creating ? null : ReadCooperativeEvidence(directory, "cooperativejourney.json");
        var firstSlot = creating ? 1 : 2;
        if (slot != firstSlot)
            await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-connected.json")),
                "The first cooperative client did not connect.");

        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        timingObservedMain = main;
        AddChild(main);
        await Frame();
        await Frame();
        var setup = main.GetNode<CharacterSetupPresentation>("CharacterSetup");
        Require(setup.Visible && !main.TransportConnected, "Cooperative acceptance must enter through the real setup form.");
        var mode = (OptionButton)setup.FindChild("SessionMode", true, false);
        Require(mode.Selected == (creating ? 0 : 1), "The cooperative setup form selected an unexpected session mode.");
        if (creating) {
            ((LineEdit)setup.FindChild("CharacterName", true, false)).Text = $"M1-Cooperative-Player-{slot}";
            ((OptionButton)setup.FindChild("CharacterClass", true, false)).Select(1);
        } else {
            Require(previous!.Passed && ((LineEdit)setup.FindChild("ResumeToken", true, false)).Text.Length > 0,
                "Cooperative resume requires the private persisted resume token.");
        }

        await Click(((Button)setup.FindChild("Connect", true, false)).GetGlobalRect().GetCenter());
        await Until(() => main.TransportConnected && main.ObservedSnapshot?.Players.Count > 0,
            "No authoritative cooperative player snapshot arrived.");
        var identity = main.ObservedIdentity!;
        Require(identity.GameMode == GameMode.Multiplayer, "Cooperative acceptance did not connect to multiplayer mode.");
        var local = FindCooperativePlayer(main.ObservedSnapshot!, slot);
        if (previous is not null) {
            Require(identity.ContentManifestHash == previous.ContentHash && identity.RulesetIdentityHash == previous.RulesetHash,
                "Cooperative resume changed the content or ruleset identity.");
            Require(local.EntityId == previous.EntityId && local.LevelId == previous.LevelId
                && local.PositionX == previous.PositionX && local.PositionY == previous.PositionY,
                "Fresh-host cooperative resume changed the durable local player position or identity.");
        }

        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(directory, "resume-token.txt")),
            "The cooperative player resume token was not persisted.");
        if (creating && slot == firstSlot) {
            await UntilCooperativeSettled(main, "The first cooperative player did not settle before clearing the town spawn.");
            await MoveCooperativeOneTileAsync(main, FindCooperativePlayer(main.ObservedSnapshot!, slot), null);
            local = FindCooperativePlayer(main.ObservedSnapshot!, slot);
        }
        WriteEvidence(directory, prefix + "-connected.json", new { Ready = true, Slot = slot, local.EntityId });
        await Until(() => main.ObservedSnapshot!.Players.Count == 2,
            "The second cooperative player did not enter the authoritative roster.");
        RequireCooperativePair(main.ObservedSnapshot!, slot, peerSlot);
        var visible = DisplayServer.GetName() != "headless";
        var drawObserved = false;
        Snapshot? restoredBeforeControls = null;

        if (creating) {
            if (slot == 2) {
                // Prepare through ordinary route/cancel controls while both
                // players are safe in town, rather than leave the first actor
                // under monster attack during the peer's full town journey.
                await TravelCooperativeToDungeonAsync(main, stageNearStair: true);
                var staged = FindCooperativePlayer(main.ObservedSnapshot!, slot);
                WriteEvidence(directory, prefix + "-stair-ready.json", new {
                    Ready = true, Slot = slot, staged.EntityId, X = staged.PositionX, Y = staged.PositionY,
                });
                await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-entry-clear.json")),
                    "Player one did not clear the shared dungeon entry before player two travelled.");
            } else {
                await Until(() => {
                    var markerPath = System.IO.Path.Combine(peerDirectory, prefix + "-stair-ready.json");
                    if (!System.IO.File.Exists(markerPath))
                        return false;
                    using var marker = JsonDocument.Parse(System.IO.File.ReadAllText(markerPath));
                    var staged = FindCooperativePlayer(main.ObservedSnapshot!, peerSlot);
                    return staged.EntityId == marker.RootElement.GetProperty("EntityId").GetUInt32()
                        && staged.PositionX == marker.RootElement.GetProperty("X").GetInt32()
                        && staged.PositionY == marker.RootElement.GetProperty("Y").GetInt32()
                        && staged.LevelId == 0 && staged.CurrentAction is null && staged.CombatMode == PlayerCombatMode.Stand;
                }, "Player one did not observe the peer's settled ordinary stair approach.");
            }
            await TravelCooperativeToDungeonAsync(main);
            await UntilCooperativeSettled(main, "Dungeon entry did not settle for the local cooperative player.");
            if (slot == 1) {
                local = FindCooperativePlayer(main.ObservedSnapshot!, slot);
                var peerAtEntry = FindCooperativePlayer(main.ObservedSnapshot!, peerSlot);
                await MoveCooperativeOneTileAsync(main, local, peerAtEntry);
                WriteEvidence(directory, prefix + "-entry-clear.json", new {
                    Passed = true, Slot = slot, X = FindCooperativePlayer(main.ObservedSnapshot!, slot).PositionX,
                    Y = FindCooperativePlayer(main.ObservedSnapshot!, slot).PositionY,
                });
            }
            WriteEvidence(directory, prefix + "-dungeon-ready.json", new { Ready = true, Slot = slot });
        } else {
            Require(local.LevelId == 1, "Cooperative resume did not restore both players to the dungeon.");
            restoredBeforeControls = main.ObservedSnapshot!;
            AssertCooperativeWorldRestored(restoredBeforeControls, previous!, slot, peerSlot);
            if (visible) {
                await RecordCooperativeDrawAsync(main, local.EntityId, FindCooperativePlayer(main.ObservedSnapshot!, peerSlot).EntityId,
                    directory, prefix, "restored");
                drawObserved = true;
            }
            WriteEvidence(directory, prefix + "-restored-ready.json", new { Ready = true, Slot = slot });
        }
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory,
            creating ? "cooperativejourney-dungeon-ready.json" : "cooperativeresume-restored-ready.json")),
            "The peer did not reach the cooperative dungeon barrier.");
        await Until(() => main.ObservedSnapshot!.Players.All(player => player.LevelId == 1),
            "The cooperative pair did not share the dungeon level.");

        // Capture restore before releasing controls; later peer movement must
        // never replace the actual pre-control evidence with an expected value.
        var initialSnapshot = restoredBeforeControls ?? main.ObservedSnapshot!;
        var initialLocal = FindCooperativePlayer(initialSnapshot, slot);
        var initialPeer = FindCooperativePlayer(initialSnapshot, peerSlot);
        if (visible && creating) {
            await RecordCooperativeDrawAsync(main, initialLocal.EntityId, initialPeer.EntityId,
                directory, prefix, "entry");
            drawObserved = true;
        }
        var initialX = initialLocal.PositionX;
        var initialY = initialLocal.PositionY;
        var initialPeerX = initialPeer.PositionX;
        var initialPeerY = initialPeer.PositionY;
        var restoredEntityId = previous is null ? 0U : initialLocal.EntityId;
        var restoredPeerEntityId = previous is null ? 0U : initialPeer.EntityId;
        var restoredX = previous is null ? 0 : initialX;
        var restoredY = previous is null ? 0 : initialY;
        var restoredPeerX = previous is null ? 0 : initialPeerX;
        var restoredPeerY = previous is null ? 0 : initialPeerY;
        // A completed death is removed from fresh-host snapshots. Keep its
        // observed journey ID for the defeat/absence assertions; do not demand
        // resurrection merely to rediscover that identity on resume.
        var monsterId = previous is not null ? previous.MonsterId
            : (initialSnapshot.Monsters.FirstOrDefault(candidate => candidate.LevelId == 1)
                ?? throw new InvalidOperationException("The authored cooperative dungeon monster is missing.")).EntityId;
        Require(monsterId > 0, "The cooperative encounter identity is missing.");
        var pickupReceiptSequence = previous?.PickupReceiptSequence ?? 0UL;
        var pickupAppliedTick = previous?.PickupAppliedTick ?? 0UL;
        var pickupSequence = previous?.PickupSequence ?? 0UL;
        var pickupStatus = previous?.PickupStatus ?? string.Empty;
        if (creating) {
            if (slot == 1) {
                await CooperativeAttackOnceAsync(main, monsterId, "Player one attack");
                WriteEvidence(directory, prefix + "-hit-slot1.json", new { Passed = true, Slot = slot, MonsterId = monsterId,
                    HitPoints = main.ObservedSnapshot!.Monsters.Single(monster => monster.EntityId == monsterId).HitPoints,
                    ReceiptSequence = main.ObservedCommandResult!.ServerReceiptSequence, AppliedTick = main.ObservedCommandResult.AppliedTick });
                await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-hit-slot2.json")),
                    "Player two did not land its acknowledged attack.");
            } else {
                await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-hit-slot1.json")),
                    "Player one did not publish its acknowledged attack.");
                await Until(() => {
                    using var hit = JsonDocument.Parse(System.IO.File.ReadAllText(
                        System.IO.Path.Combine(peerDirectory, prefix + "-hit-slot1.json")));
                    return main.ObservedSnapshot!.Monsters.Single(monster => monster.EntityId == monsterId).HitPoints
                        <= hit.RootElement.GetProperty("HitPoints").GetInt32();
                }, "Player two did not observe player one's authoritative damage.");
                await CooperativeAttackOnceAsync(main, monsterId, "Player two attack");
                WriteEvidence(directory, prefix + "-hit-slot2.json", new { Passed = true, Slot = slot, MonsterId = monsterId,
                    HitPoints = main.ObservedSnapshot!.Monsters.Single(monster => monster.EntityId == monsterId).HitPoints,
                    ReceiptSequence = main.ObservedCommandResult!.ServerReceiptSequence, AppliedTick = main.ObservedCommandResult.AppliedTick });
            }
            if (slot == 1) {
                await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-hit-slot2.json")),
                    "Player two did not publish its acknowledged attack.");
                await Until(() => {
                    using var hit = JsonDocument.Parse(System.IO.File.ReadAllText(
                        System.IO.Path.Combine(peerDirectory, prefix + "-hit-slot2.json")));
                    return main.ObservedSnapshot!.Monsters.Single(monster => monster.EntityId == monsterId).HitPoints
                        <= hit.RootElement.GetProperty("HitPoints").GetInt32();
                }, "Player one did not observe player two's authoritative damage.");
                await CooperativeDefeatAsync(main, monsterId);
                WriteEvidence(directory, prefix + "-defeated.json", new { Passed = true, MonsterId = monsterId });
            } else {
                await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-defeated.json")),
                    "Player one did not complete the authored encounter.");
            }
            await Until(() => !main.ObservedSnapshot!.Monsters.Any(candidate => candidate.EntityId == monsterId && candidate.Alive),
                "The shared authored monster did not remain defeated for both clients.");
            if (visible) {
                await RecordCooperativeDrawAsync(main, FindCooperativePlayer(main.ObservedSnapshot!, 1).EntityId,
                    FindCooperativePlayer(main.ObservedSnapshot!, 2).EntityId, directory, prefix, "defeated");
                drawObserved = true;
            }
            var drop = main.ObservedSnapshot!.WorldItems.FirstOrDefault(item => item.EntityId == 1000009)
                ?? throw new InvalidOperationException("The authored cooperative drop is missing.");
            Require(drop.ItemSeed == 6001, "The cooperative encounter produced an unexpected item seed.");
            WriteEvidence(directory, prefix + "-drop-ready.json", new { Passed = true, Slot = slot,
                MonsterId = monsterId, DropEntityId = drop.EntityId, DropSeed = drop.ItemSeed });
            await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-drop-ready.json")),
                "The peer did not observe the shared drop.");
            var ownerEntityId = FindCooperativePlayer(main.ObservedSnapshot!, 1).EntityId;
            if (slot == 1) {
                var queuedBeforePickup = main.ObservedLastQueuedSequence;
                await ClickWorld(main, drop.PositionX, drop.PositionY);
                await Until(() => main.ObservedSnapshot!.WorldItems.All(item => item.EntityId != drop.EntityId)
                    && FindCooperativePlayer(main.ObservedSnapshot!, 1).Inventory.Count(item => item.ItemSeed == drop.ItemSeed) == 1
                    && main.ObservedPendingCommandCount == 0,
                    "The owner did not pick up the shared drop through the real world control.", 15);
                var receipt = main.ObservedCommandResult
                    ?? throw new InvalidOperationException("The owner pickup did not expose an authoritative receipt.");
                Require(receipt.ClientSequence == main.ObservedLastQueuedSequence
                    && receipt.ClientSequence > queuedBeforePickup && IsAcceptedOrSuccessfulDuplicate(receipt)
                    && main.ObservedSnapshot!.Tick >= receipt.AppliedTick,
                    "The owner pickup did not receive an accepted authoritative receipt.");
                WriteEvidence(directory, prefix + "-pickup.json", new { Passed = true, Slot = slot,
                    OwnerEntityId = ownerEntityId, DropEntityId = drop.EntityId, DropSeed = drop.ItemSeed,
                    PickupSequence = receipt.ClientSequence, ReceiptSequence = receipt.ServerReceiptSequence, AppliedTick = receipt.AppliedTick,
                    Status = receipt.Status.ToString() });
                pickupSequence = receipt.ClientSequence;
                pickupReceiptSequence = receipt.ServerReceiptSequence;
                pickupAppliedTick = receipt.AppliedTick;
                pickupStatus = receipt.Status.ToString();
            } else {
                await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-pickup.json")),
                    "The owner did not publish the authoritative pickup receipt.");
                using var pickup = JsonDocument.Parse(System.IO.File.ReadAllText(
                    System.IO.Path.Combine(peerDirectory, prefix + "-pickup.json")));
                pickupSequence = pickup.RootElement.GetProperty("PickupSequence").GetUInt64();
                pickupReceiptSequence = pickup.RootElement.GetProperty("ReceiptSequence").GetUInt64();
                pickupAppliedTick = pickup.RootElement.GetProperty("AppliedTick").GetUInt64();
                pickupStatus = pickup.RootElement.GetProperty("Status").GetString() ?? string.Empty;
            }
            await Until(() => main.ObservedSnapshot!.WorldItems.All(item => item.EntityId != drop.EntityId),
                "The picked-up world item remained visible to the peer.");
            Require(FindCooperativePlayer(main.ObservedSnapshot!, 1).Inventory.Count(item => item.ItemSeed == drop.ItemSeed) == 1
                && FindCooperativePlayer(main.ObservedSnapshot!, 2).Inventory.All(item => item.ItemSeed != drop.ItemSeed),
                "Cooperative loot ownership duplicated or lost the item.");
            if (visible) {
                await RecordCooperativeDrawAsync(main, FindCooperativePlayer(main.ObservedSnapshot!, 1).EntityId,
                    FindCooperativePlayer(main.ObservedSnapshot!, 2).EntityId, directory, prefix, "loot");
                drawObserved = true;
            }
        }

        local = FindCooperativePlayer(main.ObservedSnapshot!, slot);
        var peer = FindCooperativePlayer(main.ObservedSnapshot!, peerSlot);
        var beforeMove = local.Clone();
        if (slot == 2) {
            await Until(() => {
                var markerPath = System.IO.Path.Combine(peerDirectory, prefix + "-moved.json");
                if (!System.IO.File.Exists(markerPath))
                    return false;
                using var marker = JsonDocument.Parse(System.IO.File.ReadAllText(markerPath));
                var markerId = marker.RootElement.GetProperty("EntityId").GetUInt32();
                var markerX = marker.RootElement.GetProperty("X").GetInt32();
                var markerY = marker.RootElement.GetProperty("Y").GetInt32();
                var observed = main.ObservedSnapshot!.Players.SingleOrDefault(player => player.EntityId == markerId);
                return observed is not null && observed.PositionX == markerX && observed.PositionY == markerY
                    && observed.CurrentAction is null && observed.CombatMode == PlayerCombatMode.Stand;
            }, "Player one did not complete and publish the serialized final cooperative movement.");
            peer = FindCooperativePlayer(main.ObservedSnapshot!, peerSlot);
        }
        await MoveCooperativeOneTileAsync(main, local, peer);
        local = FindCooperativePlayer(main.ObservedSnapshot!, slot);
        peer = FindCooperativePlayer(main.ObservedSnapshot!, peerSlot);
        WriteEvidence(directory, prefix + "-moved.json", new {
            Passed = true, Slot = slot, EntityId = local.EntityId, X = local.PositionX, Y = local.PositionY,
            ReceiptSequence = main.ObservedCommandResult?.ServerReceiptSequence ?? 0,
            AppliedTick = main.ObservedCommandResult?.AppliedTick ?? 0,
        });
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + "-moved.json")),
            "The peer did not complete its final cooperative movement.");
        await Until(() => {
            using var moved = JsonDocument.Parse(System.IO.File.ReadAllText(
                System.IO.Path.Combine(peerDirectory, prefix + "-moved.json")));
            var peerId = moved.RootElement.GetProperty("EntityId").GetUInt32();
            var peerX = moved.RootElement.GetProperty("X").GetInt32();
            var peerY = moved.RootElement.GetProperty("Y").GetInt32();
            var observed = main.ObservedSnapshot!.Players.SingleOrDefault(player => player.EntityId == peerId);
            return observed is not null && observed.PositionX == peerX && observed.PositionY == peerY
                && observed.CurrentAction is null && observed.CombatMode == PlayerCombatMode.Stand;
        }, "The final cooperative movement was not observed at the peer's authoritative position.");
        local = FindCooperativePlayer(main.ObservedSnapshot!, slot);
        peer = FindCooperativePlayer(main.ObservedSnapshot!, peerSlot);
        var finalReceipt = main.ObservedCommandResult
            ?? throw new InvalidOperationException("The final cooperative movement did not produce a command receipt.");
        Require(IsAcceptedOrSuccessfulDuplicate(finalReceipt)
            && finalReceipt.ClientSequence == main.ObservedLastQueuedSequence
            && finalReceipt.ClientSequence > 0 && finalReceipt.ServerReceiptSequence > 0 && finalReceipt.AppliedTick > 0,
            "The final cooperative movement did not retain an exact accepted canonical receipt.");
        if (previous is not null) {
            Require(finalReceipt.ClientSequence > previous.Sequence
                && finalReceipt.ServerReceiptSequence > previous.ReceiptSequence
                && finalReceipt.AppliedTick >= previous.AppliedTick
                && (local.PositionX != previous.PositionX || local.PositionY != previous.PositionY),
                "Fresh-host cooperative resume did not publish a new settled movement receipt and position.");
        }
        var owner = FindCooperativePlayer(main.ObservedSnapshot!, 1);
        var sharedDrop = main.ObservedSnapshot!.WorldItems.FirstOrDefault(item => item.EntityId == 1000009);
        var world = (Control)main.FindChild("WorldArea", true, false);
        if (visible) {
            await RecordCooperativeDrawAsync(main, local.EntityId, peer.EntityId, directory, prefix, "final");
            drawObserved = true;
        }
        Require(main.ObservedPlayerScreenPosition is { } localScreen && world.GetGlobalRect().HasPoint(localScreen)
            && main.ObservedEntityScreenPosition(peer.EntityId) is { } peerScreen
            && world.GetGlobalRect().HasPoint(peerScreen),
            "The cooperative final roster is not visibly presented inside the world area.");
        var final = new CooperativeEvidence(true, identity.GameMode.ToString(), identity.ContentManifestHash,
            identity.RulesetIdentityHash, slot, local.EntityId, peer.EntityId, local.LevelId, local.PositionX,
            local.PositionY, peer.PositionX, peer.PositionY, monsterId, !main.ObservedSnapshot!.Monsters.Any(item => item.EntityId == monsterId && item.Alive),
             1000009, 6001, owner.EntityId, owner.Inventory.Count(item => item.ItemSeed == 6001),
             FindCooperativePlayer(main.ObservedSnapshot!, 2).Inventory.Count(item => item.ItemSeed == 6001),
             sharedDrop is not null, finalReceipt.ClientSequence,
             finalReceipt.ServerReceiptSequence, finalReceipt.AppliedTick, finalReceipt.Status.ToString(),
             local.PositionX, local.PositionY, pickupSequence, pickupReceiptSequence, pickupAppliedTick, pickupStatus, visible, drawObserved,
             initialX, initialY, initialPeerX, initialPeerY,
             restoredEntityId, restoredPeerEntityId, restoredX, restoredY, restoredPeerX, restoredPeerY);
        WriteEvidence(directory, prefix + ".json", final);
        await Until(() => System.IO.File.Exists(System.IO.Path.Combine(peerDirectory, prefix + ".json")),
            "The peer did not finish its cooperative state assertions.");
        Require(beforeMove.EntityId == local.EntityId, "The final cooperative control rebound the local player.");
    }

    private async Task TravelCooperativeToDungeonAsync(Main main, bool stageNearStair = false)
    {
        await UntilCooperativeSettled(main, "The town did not settle before dungeon travel.");
        var origin = FindCooperativePlayer(main.ObservedSnapshot!, ParseLocalSlot());
        var projectedRoutes = main.ObservedSnapshot!.LevelGeometry.Single(level => level.LevelId == origin.LevelId)
            .WarpTriggers.OrderBy(route => route.WarpId).ToArray();
        var routeIndex = Array.FindIndex(projectedRoutes, route => route.Available && route.TargetLevelId == 1);
        Require(routeIndex >= 0, "The server did not project an available dungeon stair.");
        var dungeonRoute = projectedRoutes[routeIndex];
        var sections = (TabContainer)main.FindChild("HudSections", true, false);
        var routes = (OptionButton)main.FindChild("Routes", true, false);
        await SelectTab(sections, sections.GetTabCount() - 1);
        Require(routes.ItemCount > 0, "The town-loop-preview did not expose a dungeon route.");
        await SelectRoute(routes, routeIndex);
        var travel = (Button)main.FindChild("Travel", true, false);
        var cancel = (Button)main.FindChild("CancelTravel", true, false);
        Require(!travel.Disabled, "The projected cooperative dungeon route is disabled.");
        var queuedBefore = main.ObservedLastQueuedSequence;
        await Click(travel.GetGlobalRect().GetCenter());
        // Travel starts a repeater; its first intent is queued by a later
        // Main._Process cycle, not synchronously by the button release.
        await Until(() => main.ObservedLastQueuedSequence > queuedBefore,
            "Dungeon travel did not queue a new authoritative command.", 5);
        var inputSequence = main.ObservedLastQueuedSequence;
        await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence == inputSequence
            && IsAcceptedOrSuccessfulDuplicate(result) && result.ServerReceiptSequence > 0 && result.AppliedTick > 0,
            "Dungeon travel did not receive an authoritative receipt.");
        if (stageNearStair) {
            await Until(() => {
                var player = FindCooperativePlayer(main.ObservedSnapshot!, ParseLocalSlot());
                Require(player.LevelId == origin.LevelId && player.Life > 0 && !cancel.Disabled,
                    "Cooperative stair preparation stopped or entered the dungeon before cancellation.");
                return Math.Max(Math.Abs(player.PositionX - dungeonRoute.TriggerX),
                    Math.Abs(player.PositionY - dungeonRoute.TriggerY)) <= 4;
            }, "The peer did not approach the dungeon stair through ordinary movement.", 60);
            await Click(cancel.GetGlobalRect().GetCenter());
            await Until(() => cancel.Disabled && main.ObservedPendingCommandCount == 0
                && main.ObservedCommandResult is { } receipt && receipt.ClientSequence == main.ObservedLastQueuedSequence
                && IsAcceptedOrSuccessfulDuplicate(receipt) && main.ObservedSnapshot!.Tick >= receipt.AppliedTick
                && FindCooperativePlayer(main.ObservedSnapshot!, ParseLocalSlot()) is {
                    CurrentAction: null, CombatMode: PlayerCombatMode.Stand,
                }, "The cooperative stair approach did not settle after real cancellation.", 15);
            var staged = FindCooperativePlayer(main.ObservedSnapshot!, ParseLocalSlot());
            Require(staged.LevelId == origin.LevelId, "Stair preparation entered the dungeon prematurely.");
            // Step perpendicular to the projected final route leg using a
            // real safe tile click so the first player can pass.
            await MoveCooperativeOneTileAsync(main, staged,
                FindCooperativePlayer(main.ObservedSnapshot!, 3 - ParseLocalSlot()),
                preferVertical: staged.PositionY == dungeonRoute.TriggerY);
            var parked = FindCooperativePlayer(main.ObservedSnapshot!, ParseLocalSlot()).Clone();
            var parkedSequence = main.ObservedLastQueuedSequence;
            var parkedTick = main.ObservedSnapshot!.Tick;
            Require(parked.LevelId == origin.LevelId && Math.Max(Math.Abs(parked.PositionX - dungeonRoute.TriggerX),
                Math.Abs(parked.PositionY - dungeonRoute.TriggerY)) > 1,
                "The parked cooperative peer did not retain safe separation from the stair.");
            await Until(() => main.ObservedSnapshot!.Tick >= parkedTick + 4,
                "No snapshots arrived to verify the parked cooperative peer.", 5);
            var stillParked = FindCooperativePlayer(main.ObservedSnapshot!, ParseLocalSlot());
            Require(cancel.Disabled && main.ObservedPendingCommandCount == 0
                && main.ObservedLastQueuedSequence == parkedSequence
                && main.ObservedCommandResult!.ClientSequence == parkedSequence
                && stillParked.LevelId == parked.LevelId && stillParked.PositionX == parked.PositionX
                && stillParked.PositionY == parked.PositionY && stillParked.CurrentAction is null
                && stillParked.CombatMode == PlayerCombatMode.Stand,
                "Cancelled cooperative travel continued queuing or moving the parked peer.");
            return;
        }
        await Until(() => FindCooperativePlayer(main.ObservedSnapshot!, ParseLocalSlot()).LevelId == 1
            && main.ObservedPendingCommandCount == 0,
            "The cooperative pair did not reach the authored dungeon.", 60);
    }

    private async Task CooperativeAttackOnceAsync(Main main, uint monsterId, string action)
    {
        var local = FindCooperativePlayer(main.ObservedSnapshot!, ParseLocalSlot());
        for (var attempt = 0; attempt < 12; attempt++) {
            var target = main.ObservedSnapshot!.Monsters.Single(monster => monster.EntityId == monsterId);
            var before = target.HitPoints;
            var queuedBefore = main.ObservedLastQueuedSequence;
            await ClickWorld(main, target.PositionX, target.PositionY, MouseButton.Right);
            var inputSequence = main.ObservedLastQueuedSequence;
            Require(inputSequence > queuedBefore, $"{action} did not queue a new authoritative command.");
            await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence == inputSequence,
                $"{action} did not receive an authoritative command receipt.", 15);
            var receipt = main.ObservedCommandResult!;
            Require(IsAcceptedOrSuccessfulDuplicate(receipt), $"{action} was rejected: {receipt.RejectReason}.");
            Require(receipt.ServerReceiptSequence > 0 && receipt.AppliedTick > 0,
                $"{action} did not expose a canonical server receipt.");
            // ACK precedes the server snapshot on the wire. A pre-command idle
            // snapshot cannot classify an in-flight shot as a settled miss.
            var snapshotRevisionAtReceipt = main.ObservedSnapshotRevision;
            var localEntityId = local.EntityId;
            var landed = false;
            await Until(() => {
                var snapshot = main.ObservedSnapshot!;
                var currentTarget = snapshot.Monsters.SingleOrDefault(monster => monster.EntityId == monsterId);
                landed = currentTarget is null || !currentTarget.Alive || currentTarget.HitPoints < before;
                var player = FindCooperativePlayer(snapshot, ParseLocalSlot());
                var settled = main.ObservedSnapshotRevision > snapshotRevisionAtReceipt
                    && snapshot.Tick >= receipt.AppliedTick
                    && main.ObservedPendingCommandCount == 0 && player.CurrentAction is null
                    && player.CombatMode == PlayerCombatMode.Stand
                    && snapshot.Projectiles.All(projectile => projectile.SourceEntityId != localEntityId
                        || projectile.TargetEntityId != monsterId);
                return settled;
            }, $"{action} accepted but did not settle its damage outcome.", 15);
            if (landed) {
                Require(local.EntityId == FindCooperativePlayer(main.ObservedSnapshot!, ParseLocalSlot()).EntityId,
                    $"{action} changed the local durable player identity.");
                return;
            }
        }
        throw new InvalidOperationException($"{action} exhausted accepted ordinary attempts without landing damage.");
    }

    private async Task CooperativeDefeatAsync(Main main, uint monsterId)
    {
        var attacks = 0;
        while (main.ObservedSnapshot!.Monsters.Any(monster => monster.EntityId == monsterId && monster.Alive)) {
            Require(++attacks <= 30, "The authored cooperative encounter did not defeat its monster normally.");
            await CooperativeAttackOnceAsync(main, monsterId, "Cooperative defeat attack");
        }
    }

    private async Task MoveCooperativeOneTileAsync(Main main, PlayerSnapshot local, PlayerSnapshot? peer,
        bool preferVertical = false)
    {
        var geometry = main.ObservedSnapshot!.LevelGeometry.Single(level => level.LevelId == local.LevelId);
        var offsets = preferVertical
            ? new[] { (X: 0, Y: 1), (X: 0, Y: -1), (X: 1, Y: 0), (X: -1, Y: 0) }
            : new[] { (X: 1, Y: 0), (X: -1, Y: 0), (X: 0, Y: 1), (X: 0, Y: -1) };
        foreach (var offset in offsets) {
            var x = local.PositionX + offset.X;
            var y = local.PositionY + offset.Y;
            if (x < 0 || y < 0 || x >= geometry.Width || y >= geometry.Height
                || (peer is not null && peer.LevelId == local.LevelId && x == peer.PositionX && y == peer.PositionY))
                continue;
            var cell = checked(y * geometry.Width + x);
            if (geometry.BlockedCells.Contains(cell) || geometry.StaticObjectBlockedCells.Contains(cell)
                || geometry.WarpTriggers.Any(trigger => trigger.Available && trigger.TriggerX == x && trigger.TriggerY == y)
                || main.ObservedSnapshot!.Monsters.Any(monster => monster.LevelId == local.LevelId
                    && monster.Alive && monster.PositionX == x && monster.PositionY == y)
                || main.ObservedSnapshot.Objects.Any(worldObject => worldObject.LevelId == local.LevelId
                    && !worldObject.Destroyed && worldObject.PositionX == x && worldObject.PositionY == y))
                continue;
            var queuedBefore = main.ObservedLastQueuedSequence;
            await Click(main.ObservedTileScreenPosition(x, y), walk: true);
            var inputSequence = main.ObservedLastQueuedSequence;
            Require(inputSequence > queuedBefore, "The cooperative movement did not queue a new authoritative command.");
            await Until(() => main.ObservedCommandResult is { } result && result.ClientSequence == inputSequence
                && IsAcceptedOrSuccessfulDuplicate(result) && result.ServerReceiptSequence > 0 && result.AppliedTick > 0
                && FindCooperativePlayer(main.ObservedSnapshot!, ParseLocalSlot()) is { } moved
                && moved.PositionX == x && moved.PositionY == y && moved.CurrentAction is null
                && moved.CombatMode == PlayerCombatMode.Stand,
                "The final cooperative control did not settle on a distinct authoritative tile.", 15);
            return;
        }
        throw new InvalidOperationException("The authored dungeon offered no safe final cooperative control tile.");
    }

    private async Task UntilCooperativeSettled(Main main, string failure)
        => await Until(() => main.ObservedPendingCommandCount == 0
            && FindCooperativePlayer(main.ObservedSnapshot!, ParseLocalSlot()) is { CurrentAction: null, CombatMode: PlayerCombatMode.Stand }, failure, 15);

    private void AssertCooperativeWorldRestored(Snapshot snapshot, CooperativeEvidence previous, int slot, int peerSlot)
    {
        var local = FindCooperativePlayer(snapshot, slot);
        var peer = FindCooperativePlayer(snapshot, peerSlot);
        Require(local.EntityId == previous.EntityId && peer.EntityId == previous.PeerEntityId
            && local.PositionX == previous.PositionX && local.PositionY == previous.PositionY
            && peer.PositionX == previous.PeerPositionX && peer.PositionY == previous.PeerPositionY,
            "Fresh-host cooperative resume changed a durable player ID or position.");
        Require(!snapshot.Monsters.Any(monster => monster.EntityId == previous.MonsterId && monster.Alive)
            && snapshot.WorldItems.All(item => item.EntityId != previous.DropEntityId)
            && FindCooperativePlayer(snapshot, 1).Inventory.Count(item => item.ItemSeed == previous.DropSeed) == 1
            && FindCooperativePlayer(snapshot, 2).Inventory.All(item => item.ItemSeed != previous.DropSeed),
            "Fresh-host cooperative resume changed defeated-world or loot ownership state.");
    }

    private static PlayerSnapshot FindCooperativePlayer(Snapshot snapshot, int slot)
        => snapshot.Players.Single(player => player.CharacterName == $"M1-Cooperative-Player-{slot}");

    private async Task RecordCooperativeDrawAsync(Main main, uint localEntityId, uint peerEntityId,
        string directory, string prefix, string milestone)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var worldArea = (Control)main.FindChild("WorldArea", true, false);
        var world = worldArea.GetChildren().OfType<WorldPresentation>().Single();
        var observations = new List<Devilution.Client.PresentationAssets.PresentationPlayerTextureBindingObservation>();
        foreach (var entityId in new[] { localEntityId, peerEntityId }) {
            var observation = world.ObservePlayerTextureBinding(entityId);
            Require(observation?.Binding is { IsLoaded: true, UsedFallback: false },
                $"The {milestone} milestone did not record a loaded per-entity player draw for {entityId}.");
            observations.Add(observation!);
        }
        // Bindings are retained actual lookups, not per-frame draw stamps.
        // Inspect the paired milestone PNGs separately to verify visibility.
        WriteEvidence(directory, $"{prefix}-{milestone}-draw.json", new {
            Passed = true, Milestone = milestone, SnapshotTick = main.ObservedSnapshot!.Tick,
            Observations = observations,
        });
        await Capture(System.IO.Path.Combine(directory, $"{prefix}-{milestone}.png"));
    }

    private static CooperativeEvidence ReadCooperativeEvidence(string directory, string name)
        => JsonSerializer.Deserialize<CooperativeEvidence>(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, name)))
            ?? throw new InvalidDataException("Cooperative evidence is empty.");

    private static void RequireCooperativePair(Snapshot snapshot, int localSlot, int peerSlot)
    {
        var local = FindCooperativePlayer(snapshot, localSlot);
        var peer = FindCooperativePlayer(snapshot, peerSlot);
        Require(local.EntityId != peer.EntityId && local.LevelId == peer.LevelId,
            "The cooperative roster contains duplicate IDs or divergent levels.");
    }

    private static int ParseLocalSlot()
        => int.Parse(Environment.GetEnvironmentVariable("GODEVILUTION_LOCAL_PLAYER")!);

    private sealed record CooperativeEvidence(bool Passed, string Mode, string ContentHash, string RulesetHash,
        int Slot, uint EntityId, uint PeerEntityId, uint LevelId, int PositionX, int PositionY,
        int PeerPositionX, int PeerPositionY, uint MonsterId, bool MonsterDefeated, uint DropEntityId,
        uint DropSeed, uint OwnerEntityId, int OwnerInventoryCount, int PeerInventoryCount, bool DropPresent,
        ulong Sequence, ulong ReceiptSequence, ulong AppliedTick, string ReceiptStatus, int PostMoveX, int PostMoveY,
        ulong PickupSequence, ulong PickupReceiptSequence, ulong PickupAppliedTick, string PickupStatus, bool Visible, bool DrawObserved,
        int InitialX, int InitialY, int InitialPeerX, int InitialPeerY,
        uint RestoredEntityId = 0, uint RestoredPeerEntityId = 0, int RestoredX = 0, int RestoredY = 0,
        int RestoredPeerX = 0, int RestoredPeerY = 0);
}
