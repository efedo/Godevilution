using System.Diagnostics.CodeAnalysis;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private sealed record NativeCatacombsObjectCheckpoint(uint DoorLevel, uint DoorEntity,
        uint ChestLevel, uint ChestEntity, uint DropEntity, uint ItemSeed, uint StableItemId, bool Gold, uint PickupValue, bool Consumable,
        uint DoorTrapSource = 0, ulong DoorTrapTick = 0);
    private sealed record NativeCatacombsUnreachableObject(uint Level, uint Entity, uint NativeType, string Reason);

    private NativeCatacombsDoorTrapObservation.Firing? nativeCatacombsTrappedDoor;
    private NativeCatacombsObjectCheckpoint? nativeCatacombsObjects;
    private NativeCatacombsDoorTrapObservation.Firing? nativeCatacombsRequiredDoorFiring;
    private NativeCatacombsRequiredDoor? nativeCatacombsRequiredDoor;
    private uint nativeCatacombsRequiredDoorEntity;
    private readonly AcceptanceStageBudget nativeChestBudget = new("Catacombs chest and pickup", 60);
    private readonly AcceptanceStageBudget nativeRequiredDoorBudget = new("Required Catacombs door", 180);
    private AcceptanceStageBudget? nativeObjectStageBudget;

    private IDisposable NativeObjectStage(AcceptanceStageBudget budget)
    {
        if (nativeObjectStageBudget is not null) throw new InvalidOperationException("Native object stages cannot overlap.");
        var scope = budget.Enter(() => nativeObjectStageBudget = null);
        nativeObjectStageBudget = budget;
        return scope;
    }

    private void ConfigureNativeCatacombsRequiredDoor()
    {
        nativeCatacombsRequiredDoor = NativeCatacombsRequiredDoor.ForRun(NativeWarriorRequiresTrappedDoor,
            System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_DEPTH_TRAPPED_DOOR_LEVEL"),
            System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_DEPTH_TRAPPED_DOOR_X"),
            System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_DEPTH_TRAPPED_DOOR_Y"));
    }
    private readonly List<NativeCatacombsUnreachableObject> nativeCatacombsUnreachableObjects = [];
    private static bool NativeWarriorRequiresTrappedDoor
        => System.Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_DEPTH_REQUIRE_TRAPPED_DOOR") == "1";

    /** The same cardinal walks and world clicks as the ordinary chest journey; transient blockers are resolved first. */
    private async Task<NativeCatacombsObjectCheckpoint?> TryNativeCatacombsObjectsAsync(Main main)
    {
        var snapshot = main.ObservedSnapshot!;
        var levelId = snapshot.Players.Single().LevelId;
        if (levelId is < 5 or > 8)
            return nativeCatacombsObjects;
        if (nativeCatacombsRequiredDoor is { } required && required.Level == levelId)
            await RequireNativeCatacombsDoorAsync(main, required);
        if (nativeCatacombsObjects is not null)
            return WithNativeCatacombsDoor(nativeCatacombsObjects);
        using var timed = NativeWarriorStep("objects", $"L{levelId} chest and pickup");
        using var stage = NativeObjectStage(nativeChestBudget);
        var attempted = new HashSet<uint>();
        // Stay within the current open floor and the same 24-step limit. Clear
        // nearby actors that cross the route; never open a door or hunt another room.
        for (var attempt = 0; attempt < 4; attempt++) {
            snapshot = main.ObservedSnapshot!;
            var geometry = DepthGeometryFromSnapshot(snapshot, levelId);
            var candidate = snapshot.Objects.Where(value => value.LevelId == levelId && value.NativeChestInteractable
                    && !value.Activated && !value.Destroyed && !attempted.Contains(value.EntityId)
                    && value.NativeObjectId is 5 or 6 or 7 or 68 or 69 or 70)
                .Select(value => (Object: value, Path: NativeObjectApproachPath.Find(snapshot, geometry, value, ignoreActors: true)))
                .Where(value => value.Path is { Count: <= 24 })
                .OrderBy(value => value.Path!.Count).ThenBy(value => value.Object.EntityId).FirstOrDefault();
            if (candidate.Object is not { } chest) return null;
            attempted.Add(chest.EntityId);
            await UseNativeHealingPotionIfNeeded(main);
            try {
                await MoveAdjacentToNativePosition(main,
                    current => current.Objects.Single(value => value.EntityId == chest.EntityId),
                    NativeObjectObstruction(main, chest, allowClosedDoors: false));
            } catch (NativeObjectApproachUnavailableException exception) when (main.ObservedSnapshot!.Players.Single() is { Life: > 0 } alive
                && alive.LevelId == levelId) {
                RecordUnreachableNativeCatacombsObject(chest, exception);
                await SettleInterruptedNativeDoorApproachAsync(main);
                continue;
            }
            var oldItems = main.ObservedSnapshot!.WorldItems.Select(value => value.EntityId).ToHashSet();
            var sequence = main.ObservedLastQueuedSequence;
            GetViewport().GuiReleaseFocus();
            await ClickWorld(main, chest.PositionX, chest.PositionY);
            await AcceptedAndSettled(main, sequence, "Open a native Catacombs chest");
            var opened = main.ObservedSnapshot!.Objects.Single(value => value.EntityId == chest.EntityId);
            Require(opened.Activated && opened.AnimationFrame == chest.AnimationFrame + 2
                && !opened.NativeChestInteractable, "The Catacombs chest did not retain its native opened state.");
            await ObserveNativeWarriorOperatedObjectDrawAsync(main, opened);
            var drops = main.ObservedSnapshot.WorldItems.Where(value => value.LevelId == levelId
                    && !oldItems.Contains(value.EntityId)).OrderBy(value => value.EntityId).ToArray();
            foreach (var drop in drops) {
                await UseNativeHealingPotionIfNeeded(main);
                try {
                    await MoveAdjacentToNativePosition(main, current => {
                        var item = current.WorldItems.Single(value => value.EntityId == drop.EntityId);
                        return new ObjectSnapshot { EntityId = item.EntityId, LevelId = item.LevelId,
                            PositionX = item.PositionX, PositionY = item.PositionY };
                    }, NativeObjectObstruction(main, new ObjectSnapshot { EntityId = drop.EntityId, LevelId = levelId,
                        PositionX = drop.PositionX, PositionY = drop.PositionY }, allowClosedDoors: false, pickup: true), pickup: true);
                } catch (NativeObjectApproachUnavailableException exception) when (main.ObservedSnapshot!.Players.Single() is { Life: > 0 } alive
                    && alive.LevelId == levelId) {
                    RecordUnreachableNativeCatacombsObject(new ObjectSnapshot { EntityId = drop.EntityId,
                        LevelId = levelId, PositionX = drop.PositionX, PositionY = drop.PositionY }, exception);
                    await SettleInterruptedNativeDoorApproachAsync(main);
                    continue; // Try the other native drops, then another chest, without losing the evidence.
                }
                var goldBefore = main.ObservedSnapshot!.Players.Single().Gold;
                sequence = main.ObservedLastQueuedSequence;
                GetViewport().GuiReleaseFocus();
                await ClickWorld(main, drop.PositionX, drop.PositionY);
                await AcceptedAndSettled(main, sequence, "Pick up a native Catacombs chest drop");
                var after = main.ObservedSnapshot!;
                var gold = drop.State.ItemType == 11;
                var value = NativeCatacombsChestStage.VerifyPickup(main.ObservedCommandResult!, after, drop,
                    levelId, goldBefore, CountNativeItemOccurrences(main, after.Players.Single(), drop.State.StableItemId, drop.ItemSeed));
                var result = WithNativeCatacombsDoor(new(0, 0, levelId, chest.EntityId, drop.EntityId,
                    drop.ItemSeed, drop.State.StableItemId, gold, value, drop.State.ItemType == 0));
                VerifyNativeCatacombsObjects(after, result);
                return result;
            }
        }
        return null;
    }

    [return: NotNullIfNotNull(nameof(checkpoint))]
    private NativeCatacombsObjectCheckpoint? WithNativeCatacombsDoor(NativeCatacombsObjectCheckpoint? checkpoint)
    {
        var firing = nativeCatacombsRequiredDoor is null ? nativeCatacombsTrappedDoor : nativeCatacombsRequiredDoorFiring;
        if (checkpoint is null) {
            if (firing is null) return null;
            // A seeded door run can retain its firing evidence even when no chest was available.
            checkpoint = new(DoorLevel: 0, DoorEntity: 0, ChestLevel: 0, ChestEntity: 0, DropEntity: 0,
                ItemSeed: 0, StableItemId: 0, Gold: false, PickupValue: 0, Consumable: false);
        }
        return checkpoint with { DoorLevel = firing?.Level ?? 0, DoorEntity = firing?.DoorEntity ?? 0,
            DoorTrapSource = firing?.TrapEntity ?? 0, DoorTrapTick = firing?.Tick ?? 0 };
    }

    private void RecordNativeCatacombsDoorFiring(NativeCatacombsDoorTrapObservation.Firing fired)
    {
        if (nativeCatacombsTrappedDoor is null)
            GD.Print($"L{fired.Level} door {fired.DoorEntity} fired wall trap {fired.TrapEntity} at tick {fired.Tick}.");
        nativeCatacombsTrappedDoor ??= fired;
        if (nativeCatacombsRequiredDoor is { } target && fired.Level == target.Level
            && fired.DoorEntity == nativeCatacombsRequiredDoorEntity)
            nativeCatacombsRequiredDoorFiring = fired;
    }

    /** Only the launcher's one seeded door is an extra destination. Other doors must lie on its route. */
    private async Task RequireNativeCatacombsDoorAsync(Main main, NativeCatacombsRequiredDoor target)
    {
        var currentDoor = main.ObservedSnapshot!.Objects.SingleOrDefault(target.Matches)
            ?? throw new InvalidOperationException($"Required native door is absent at L{target.Level} ({target.X},{target.Y}).");
        nativeCatacombsRequiredDoorEntity = currentDoor.EntityId;
        if (nativeCatacombsRequiredDoorFiring?.DoorEntity == currentDoor.EntityId && currentDoor.MissilePassable) return;
        nativeCatacombsRequiredDoorFiring = null;
        Require(!currentDoor.MissilePassable,
            "The required door was already open without firing evidence from a fresh observed opening.");
        using var timed = NativeWarriorStep("objects", $"Required door L{target.Level}@{target.X},{target.Y}");
        using var stage = NativeObjectStage(nativeRequiredDoorBudget);
        var door = currentDoor;
        await MoveAdjacentToNativePosition(main,
            current => current.Objects.Single(value => value.EntityId == door.EntityId),
            NativeObjectObstruction(main, door));
        var retainedDoor = main.ObservedSnapshot!.Objects.Single(value => value.EntityId == door.EntityId);
        if (retainedDoor.MissilePassable) {
            Require(nativeCatacombsRequiredDoorFiring?.DoorEntity == door.EntityId,
                "The required door opened during its approach without fresh firing evidence.");
            return;
        }
        var opening = await OperateNativeWarriorCatacombsDoorAsync(main, retainedDoor);
        await Until(() => {
            if (opening.Find(main.ObservedSnapshot!) is not { } firing) return false;
            RecordNativeCatacombsDoorFiring(firing);
            return true;
        }, $"Required L{target.Level} door ({target.X},{target.Y}) lacks firing evidence for this opening before its deadline.", 5);
    }

    private Func<bool, Task<bool>> NativeObjectObstruction(Main main, ObjectSnapshot target,
        bool allowClosedDoors = true, bool pickup = false)
    {
        var operations = 0;
        return async blocked => {
            if (!blocked) return false;
            if (++operations > 16)
                throw new NativeObjectApproachUnavailableException($"The route to native object {target.EntityId} stayed blocked after 16 attempts.");
            await UseNativeHealingPotionIfNeeded(main);
            await ClearNativeWarriorThreatsAsync(main, target.LevelId);
            var snapshot = main.ObservedSnapshot!;
            Require(snapshot.Players.Single().LevelId == target.LevelId,
                $"The required-object approach unexpectedly left L{target.LevelId}.");
            var geometry = DepthGeometryFromSnapshot(snapshot, target.LevelId);
            var route = NativeObjectApproachPath.Find(snapshot, geometry, target, pickup, allowClosedDoors, ignoreActors: true);
            if (route is null)
                throw new NativeObjectApproachUnavailableException($"No native floor route reaches object {target.EntityId}.");
            var neededDoor = NativeObjectApproachPath.FirstClosedDoorOnRoute(snapshot, target.LevelId, route!);
            if (neededDoor is not null) {
                await MoveAdjacentToNativePosition(main,
                    current => current.Objects.Single(value => value.EntityId == neededDoor.EntityId),
                    async actorBlocked => {
                        if (!actorBlocked) return false;
                        await ClearNativeWarriorThreatsAsync(main, target.LevelId);
                        await WaitTicksWhile(main, () => !NativeWalkBoundarySettled(main), 1);
                        return true;
                    });
                await OperateNativeWarriorCatacombsDoorAsync(main,
                    main.ObservedSnapshot!.Objects.Single(value => value.EntityId == neededDoor.EntityId));
                return true;
            }
            // Actors can step across the route after it was planned; re-observe
            // instead of visiting another room or declaring the target unreachable.
            await WaitTicksWhile(main, () => true, 0.5);
            return true;
        };
    }

    private void RecordUnreachableNativeCatacombsObject(ObjectSnapshot value, Exception exception)
    {
        nativeCatacombsUnreachableObjects.Add(new(value.LevelId, value.EntityId, value.NativeObjectId, exception.Message));
        GD.Print($"L{value.LevelId} object {value.EntityId} (native {value.NativeObjectId}) remained unreachable: {exception.Message}");
    }

    private static void VerifyNativeCatacombsObjects(Snapshot snapshot, NativeCatacombsObjectCheckpoint expected)
    {
        if (expected.DoorEntity != 0) {
            var door = snapshot.Objects.Single(value => value.EntityId == expected.DoorEntity);
            Require(door.LevelId == expected.DoorLevel && door.NativeObjectId is 42 or 43
                && door.MissilePassable && !door.TrapArmed,
                "The Catacombs trapped door closed or rearmed after its native one-shot firing.");
            Require(expected.DoorTrapSource != 0 && expected.DoorTrapTick != 0
                && snapshot.Objects.Any(value => value.EntityId == expected.DoorTrapSource
                    && value.LevelId == expected.DoorLevel && value.NativeObjectId is 53 or 54),
                "The Catacombs door checkpoint has no observed linked wall-missile firing.");
        }
        // A seeded run may retain only its independently required door firing.
        if (expected.ChestEntity == 0) {
            Require(expected.DoorEntity != 0, "The native object checkpoint contains neither chest nor trapped-door evidence.");
            return;
        }
        var chest = snapshot.Objects.Single(value => value.EntityId == expected.ChestEntity);
        Require(chest.LevelId == expected.ChestLevel && chest.Activated && !chest.NativeChestInteractable,
            "The Catacombs chest lost its one-use opened state on restore.");
        Require(!NativePickedUpItemIdentity.IsOnFloor(snapshot, expected.ItemSeed, expected.StableItemId),
            "The picked-up Catacombs drop reappeared on the floor.");
        if (!expected.Gold && !expected.Consumable) {
            var player = snapshot.Players.Single();
            Require(player.Inventory.Count(value => value.ItemSeed == expected.ItemSeed && value.State.StableItemId == expected.StableItemId)
                + player.Equipment.Count(value => value.ItemSeed == expected.ItemSeed && value.State.StableItemId == expected.StableItemId)
                + player.Belt.Count(value => value.ItemSeed == expected.ItemSeed && value.State.StableItemId == expected.StableItemId)
                + player.Stash.Count(value => value.Item is not null && value.Item.ItemSeed == expected.ItemSeed
                    && value.Item.State.StableItemId == expected.StableItemId)
                + (player.HeldItem is { } held && held.ItemSeed == expected.ItemSeed
                    && held.State.StableItemId == expected.StableItemId ? 1 : 0) == 1,
                "The picked-up Catacombs item was lost or duplicated on restore.");
        }
    }
}
