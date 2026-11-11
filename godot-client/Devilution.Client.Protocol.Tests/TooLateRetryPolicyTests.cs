using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class TooLateRetryPolicyTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public void OnlyUnchangedLiveIntentsMayBeRepeated(int kind)
    {
        var snapshot = World(); var command = Intent(kind);
        var context = AuthoritativeCommandRetryPolicy.Capture(command, snapshot);
        Assert.NotNull(context);
        Assert.True(AuthoritativeCommandRetryPolicy.StillValid(command, context!, snapshot));
        foreach (var change in new Action<Snapshot>[] {
            s => s.Players[0].Life = 0, s => s.Players[0].EntityId++, s => s.Players[0].LevelId++,
            s => s.ContentManifestHash = "different", s => s.RulesetIdentityHash = "different",
        }) {
            var stale = snapshot.Clone(); change(stale);
            Assert.False(AuthoritativeCommandRetryPolicy.StillValid(command, context!, stale));
        }
    }

    [Fact]
    public void DoorOperationCannotReplayAfterAnotherOpeningOrCapabilityChange()
    {
        var snapshot = World(); var command = Intent(5);
        var context = AuthoritativeCommandRetryPolicy.Capture(command, snapshot)!;
        foreach (var change in new Action<ObjectSnapshot>[] {
            obj => obj.Activated = true, obj => obj.Destroyed = true, obj => obj.NativeThemeObjectInteractable = true,
            obj => obj.NativeObjectId++, obj => obj.PositionX++, obj => obj.TrapArmed = true,
            obj => obj.RewardItemLevel++,
        }) {
            var stale = snapshot.Clone(); change(stale.Objects[0]);
            Assert.False(AuthoritativeCommandRetryPolicy.StillValid(command, context, stale));
        }
        var cosmetic = snapshot.Clone(); cosmetic.Objects[0].AnimationFrame++;
        Assert.True(AuthoritativeCommandRetryPolicy.StillValid(command, context, cosmetic));
    }

    [Fact]
    public void PickupAndConsumableRetriesKeepThePhysicalItemIdentity()
    {
        var snapshot = World();
        foreach (var kind in new[] { 6, 7 }) {
            var command = Intent(kind); var context = AuthoritativeCommandRetryPolicy.Capture(command, snapshot)!;
            var changed = snapshot.Clone();
            if (kind == 6) changed.WorldItems[0].ItemSeed++; else changed.Players[0].Inventory[0].ItemSeed++;
            Assert.False(AuthoritativeCommandRetryPolicy.StillValid(command, context, changed));
            if (kind == 6) changed.WorldItems.Clear(); else changed.Players[0].Inventory.Clear();
            Assert.False(AuthoritativeCommandRetryPolicy.StillValid(command, context, changed));
        }
        var use = Intent(7); var original = AuthoritativeCommandRetryPolicy.Capture(use, snapshot)!;
        var transformed = snapshot.Clone(); transformed.Players[0].Inventory[0].State.ItemActionKind = ItemActionKind.RestoreFullMana;
        Assert.False(AuthoritativeCommandRetryPolicy.StillValid(use, original, transformed));
    }

    [Fact]
    public void ARelativeStepCannotBecomeAnotherStepFromANewOrigin()
    {
        var snapshot = World(); var command = new Command { MoveRequested = new() { DirectionX = 1 } };
        var context = AuthoritativeCommandRetryPolicy.Capture(command, snapshot)!;
        snapshot.Players[0].PositionX++;
        Assert.False(AuthoritativeCommandRetryPolicy.StillValid(command, context, snapshot));
    }

    [Fact]
    public void AttacksSpellsPurchasesAndUnidentifiedItemSlotsDoNotQualify()
    {
        foreach (var command in new Command[] {
            new() { AttackRequested = new() }, new() { CastRequested = new() }, new() { PurchaseRequested = new() },
            new() { SellItemRequested = new() }, new() { AllocateAttributeRequested = new() },
        }) Assert.Null(AuthoritativeCommandRetryPolicy.Capture(command, World()));
        var snapshot = World(); snapshot.Players[0].Inventory[0].State.StableItemId = 0;
        var use = Intent(7); var context = AuthoritativeCommandRetryPolicy.Capture(use, snapshot)!;
        Assert.False(AuthoritativeCommandRetryPolicy.StillValid(use, context, snapshot));
    }

    internal static Command Intent(int kind) => kind switch {
        0 => new() { OpenStoreRequested = new() { StoreId = 1 } },
        1 => new() { CloseStoreRequested = new() },
        2 => new() { OpenStashRequested = new() },
        3 => new() { CloseStashRequested = new() },
        4 => new() { MoveRequested = new() { TargetCell = true, TargetX = 2, TargetY = 2 } },
        5 => new() { OperateObjectRequested = new() { ObjectEntityId = 2 } },
        6 => new() { PickupWorldItemRequested = new() { ItemEntityId = 3 } },
        7 => new() { UseConsumableRequested = new() { Item = new() { StableItemId = 10 } } },
        8 => new() { TalkToTownerRequested = new() { TownerId = 0 } },
        9 => new() { TalkToQuestRequested = new() { QuestId = 1, TargetEntityId = 2 } },
        10 => new() { ReadBookRequested = new() { InventoryIndex = 0 } },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    internal static Snapshot World() => new() { Tick = 100, ContentManifestHash = "content", RulesetIdentityHash = "rules",
        Players = { new PlayerSnapshot { EntityId = 1, Life = 64, LifeMaximum = 64, Inventory = {
            new ItemSnapshot { ItemSeed = 123, State = new() { StableItemId = 10, ItemActionKind = ItemActionKind.RestorePartialLife } } } } },
        Objects = { new ObjectSnapshot { EntityId = 2, ObjectId = 1, PositionX = 1 } },
        WorldItems = { new WorldItemSnapshot { EntityId = 3, ItemSeed = 456, State = new() { StableItemId = 10 }, PositionX = 1 } },
        NativeTownResidents = { new NativeTownResidentSnapshot { PositionX = 1 } },
        Quests = { new QuestSnapshot { QuestId = 1 } },
        QuestTownerInteractions = { new QuestTownerInteractionSnapshot { QuestId = 1, PositionX = 1 } },
        LevelGeometry = { new LevelGeometrySnapshot { Width = 4, Height = 4 } },
    };
}
