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
