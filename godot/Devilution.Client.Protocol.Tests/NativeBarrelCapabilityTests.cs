using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeBarrelCapabilityTests
{
    [Fact]
    public void DedicatedSelectorAcceptsOnlyExplicitNativeBarrelCapability()
    {
        var barrel = Barrel(57);
        var snapshot = Snapshot(barrel);

        Assert.Null(AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4));
        Assert.Equal(10U,
            AuthoritativeObjectSelector.SelectNativeMeleeBarrelAt(snapshot, 3, 4)?.EntityId);
        Assert.True(AuthoritativeObjectSelector.IsValidNativeBarrelMeleeBreakable(barrel));

        barrel.NativeBarrelMeleeBreakable = false;
        Assert.Null(AuthoritativeObjectSelector.SelectNativeMeleeBarrelAt(snapshot, 3, 4));
        Assert.False(AuthoritativeObjectSelector.HasAuthoritativeInteraction(barrel));
    }

    [Fact]
    public void CombatSelectorKeepsActorPriorityAndUsesRequestedLocalPlayerForBarrels()
    {
        var barrel = Barrel(57);
        var snapshot = Snapshot(barrel);
        snapshot.Players.Insert(0, new PlayerSnapshot {
            EntityId = 99, LevelId = 4, PositionX = 3, PositionY = 3, Life = 64,
        });
        Assert.Equal(10U, AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 3, 4));
        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 99, 3, 4));
        snapshot.Players.Add(new PlayerSnapshot {
            EntityId = 98, LevelId = 42, PositionX = 3, PositionY = 4, Life = 64,
        });
        Assert.Equal(98U, AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 3, 4));
        snapshot.Players.RemoveAt(snapshot.Players.Count - 1);
        barrel.NativeBarrelMeleeBreakable = false;
        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 3, 4));
    }

    [Theory]
    [InlineData(57, false)]
    [InlineData(57, true)]
    [InlineData(58, false)]
    [InlineData(58, true)]
    public void ClientModelAcceptsExactNativeBarrelAcrossRawMissileFlags(int nativeObjectId, bool missilePassable)
    {
        var barrel = Barrel((uint)nativeObjectId);
        barrel.MissilePassable = missilePassable;
        Assert.True(Assert.Single(Apply(barrel).Objects).NativeBarrelMeleeBreakable);
    }

    [Fact]
    public void ClientModelRejectsForgedOrInconsistentNativeBarrelCapability()
    {
        var corruptions = new Action<ObjectSnapshot>[] {
            value => value.LevelId = 0,
            value => value.NativeObjectId = 56,
            value => value.NativeChestInteractable = true,
            value => value.NativeSarcophagusInteractable = true,
            value => value.Activated = true,
            value => value.Destroyed = true,
            value => value.TrapArmed = true,
            value => value.Breakable = true,
            value => value.IsShrine = true,
            value => value.EffectKind = 1,
            value => value.QuestId = 1,
            value => value.PresentationKind = Devilution.Protocol.V1.ObjectPresentationKind.Trap,
        };

        foreach (var corrupt in corruptions) {
            var value = Barrel(57);
            corrupt(value);
            Assert.Throws<InvalidDataException>(() => Apply(value));
        }
    }

    [Fact]
    public void NativeBarrelCapabilityIsMutuallyExclusiveWithOtherNativeCapabilities()
    {
        var chest = Barrel(57);
        chest.NativeChestInteractable = true;
        Assert.False(AuthoritativeObjectSelector.IsValidNativeBarrelMeleeBreakable(chest));

        var sarcophagus = Barrel(57);
        sarcophagus.NativeSarcophagusInteractable = true;
        Assert.False(AuthoritativeObjectSelector.IsValidNativeBarrelMeleeBreakable(sarcophagus));
    }

    private static Snapshot Apply(ObjectSnapshot value)
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 4, VisitedLevelIds = { 0, 4 } } },
            Objects = { value },
        }));
        return model.Snapshot!;
    }

    private static Snapshot Snapshot(ObjectSnapshot value)
        => new() {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 42, PositionX = 3, PositionY = 3 } },
            Objects = { value },
        };

    private static ObjectSnapshot Barrel(uint nativeObjectId)
        => new() {
            EntityId = 10,
            ObjectId = nativeObjectId,
            LevelId = 42,
            PositionX = 3,
            PositionY = 4,
            NativeObjectId = nativeObjectId,
            MissilePassable = true,
            NativeBarrelMeleeBreakable = true,
        };
}
