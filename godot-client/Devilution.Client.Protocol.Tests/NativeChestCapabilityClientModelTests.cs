using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeChestCapabilityClientModelTests
{
    [Fact]
    public void AcceptsLegacyDefaultAndValidNativeChestCapability()
    {
        var legacy = Apply(new ObjectSnapshot {
            EntityId = 10,
            ObjectId = 5,
            LevelId = 1,
            NativeObjectId = 5,
        });
        Assert.False(Assert.Single(legacy.Objects).NativeChestInteractable);

        var valid = Apply(ValidChest());
        Assert.True(Assert.Single(valid.Objects).NativeChestInteractable);
    }

    [Theory]
    [InlineData(5U, false)]
    [InlineData(6U, false)]
    [InlineData(7U, false)]
    [InlineData(68U, false)]
    [InlineData(69U, false)]
    [InlineData(70U, false)]
    [InlineData(5U, true)]
    [InlineData(6U, true)]
    [InlineData(7U, true)]
    [InlineData(68U, true)]
    [InlineData(69U, true)]
    [InlineData(70U, true)]
    public void CatacombsChestSnapshotsAdvanceTheClockWithOrWithoutTrapSense(uint type, bool revealTrap)
    {
        var chest = ValidChest();
        chest.NativeObjectId = type;
        chest.LevelId = 5;
        chest.TrapArmed = revealTrap;
        chest.NativeTrapInteractable = revealTrap;
        chest.PresentationKind = revealTrap ? Devilution.Protocol.V1.ObjectPresentationKind.Trap
            : Devilution.Protocol.V1.ObjectPresentationKind.Generic;
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Tick = 1751,
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 5, VisitedLevelIds = { 0, 4, 5 } } },
            Objects = { chest },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        var later = snapshot.Clone();
        later.Tick++;
        model.Apply(new AuthoritativeClientMessage(Snapshot: later));
        Assert.Equal(1752UL, model.Snapshot!.Tick);
        Assert.True(AuthoritativeObjectSelector.HasAuthoritativeInteraction(chest));
        Assert.Equal(revealTrap, AuthoritativeObjectSelector.IsValidNativeTrapInteraction(chest));

        chest.Activated = true;
        Assert.Throws<InvalidDataException>(() => Apply(chest));
    }

    [Fact]
    public void RejectsForgedOrInconsistentNativeChestCapability()
    {
        var malformed = new Action<ObjectSnapshot>[] {
            value => value.LevelId = 0,
            value => value.NativeObjectId = 4,
            value => value.Activated = true,
            value => value.Destroyed = true,
            value => value.TrapArmed = true,
            value => value.Breakable = true,
            value => value.EffectKind = 1,
            value => value.QuestId = 1,
            value => value.PresentationKind = Devilution.Protocol.V1.ObjectPresentationKind.Trap,
        };

        foreach (var corrupt in malformed) {
            var model = new AuthoritativeClientModel();
            var invalidObject = Mutate(ValidChest(), corrupt);
            var snapshot = new Snapshot {
                Players = { new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 4,
                    VisitedLevelIds = { 0, 4 },
                } },
                Objects = { invalidObject },
            };
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
        }
    }

    private static Snapshot Apply(ObjectSnapshot value)
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = { new PlayerSnapshot {
                EntityId = 7,
                LevelId = 4,
                VisitedLevelIds = { 0, 4 },
            } },
            Objects = { value },
        }));
        return model.Snapshot!;
    }

    private static ObjectSnapshot ValidChest()
        => new() {
            EntityId = 10,
            ObjectId = 5,
            LevelId = 42,
            NativeObjectId = 5,
            NativeChestInteractable = true,
        };

    private static ObjectSnapshot Mutate(ObjectSnapshot value, Action<ObjectSnapshot> mutation)
    {
        mutation(value);
        return value;
    }
}
