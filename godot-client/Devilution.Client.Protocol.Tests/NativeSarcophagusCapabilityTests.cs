using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeSarcophagusCapabilityTests
{
    [Fact]
    public void SelectsOnlyTheExplicitClosedNativeSarcophagusCapability()
    {
        var sarcophagus = Sarcophagus();
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 42, PositionX = 3, PositionY = 3 } },
            Objects = { sarcophagus },
        };

        Assert.Equal(10U, AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4)?.EntityId);
        sarcophagus.NativeSarcophagusInteractable = false;
        Assert.Null(AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4));
        sarcophagus.NativeSarcophagusInteractable = true;
        sarcophagus.Activated = true;
        Assert.Null(AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4));
    }

    [Fact]
    public void NeverSelectsConflictingNativeCapabilities()
    {
        var sarcophagus = Sarcophagus();
        sarcophagus.NativeChestInteractable = true;
        Assert.False(AuthoritativeObjectSelector.HasAuthoritativeInteraction(sarcophagus));

        sarcophagus.NativeObjectId = 5;
        Assert.False(AuthoritativeObjectSelector.HasAuthoritativeInteraction(sarcophagus));
        sarcophagus.NativeSarcophagusInteractable = false;
        Assert.True(AuthoritativeObjectSelector.HasAuthoritativeInteraction(sarcophagus));
    }

    [Fact]
    public void ClientModelAcceptsLegacyDefaultAndValidNativeSarcophagus()
    {
        var legacy = Sarcophagus();
        legacy.NativeSarcophagusInteractable = false;
        Assert.False(Assert.Single(Apply(legacy).Objects).NativeSarcophagusInteractable);
        Assert.True(Assert.Single(Apply(Sarcophagus()).Objects).NativeSarcophagusInteractable);
    }

    [Fact]
    public void ClientModelRejectsForgedOrInconsistentNativeSarcophagus()
    {
        var corruptions = new Action<ObjectSnapshot>[] {
            value => value.LevelId = 0,
            value => value.NativeObjectId = 47,
            value => value.NativeChestInteractable = true,
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
            var value = Sarcophagus();
            corrupt(value);
            Assert.Throws<InvalidDataException>(() => Apply(value));
        }
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

    private static ObjectSnapshot Sarcophagus()
        => new() {
            EntityId = 10,
            ObjectId = 48,
            LevelId = 42,
            PositionX = 3,
            PositionY = 4,
            NativeObjectId = 48,
            NativeSarcophagusInteractable = true,
        };
}
