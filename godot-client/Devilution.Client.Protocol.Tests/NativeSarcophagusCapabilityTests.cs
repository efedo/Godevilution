using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
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

    [Theory]
    [InlineData(1U, false)] [InlineData(1U, true)]
    [InlineData(2U, false)] [InlineData(2U, true)]
    [InlineData(3U, false)] [InlineData(3U, true)]
    [InlineData(4U, false)] [InlineData(4U, true)]
    public void ClosedSarcophagiAreSelectableAndAdmittedWithOrWithoutTheirNativeTrap(uint level, bool armed)
    {
        var value = Sarcophagus(); value.LevelId = level; value.TrapArmed = armed;
        value.NativeTrapInteractable = armed;
        value.PresentationKind = armed ? Devilution.Protocol.V1.ObjectPresentationKind.Trap : Devilution.Protocol.V1.ObjectPresentationKind.Unspecified;
        var wire = ObjectSnapshot.Parser.ParseFrom(value.ToByteArray());
        Assert.True(AuthoritativeObjectSelector.IsValidNativeSarcophagusInteraction(wire));
        var snapshot = Apply(wire, level);
        Assert.Equal(armed, Assert.Single(snapshot.Objects).TrapArmed);
        Assert.Equal(value.EntityId, AuthoritativeObjectSelector.SelectAt(snapshot, 3, 4)?.EntityId);
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

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ClientModelRejectsForgedOrInconsistentNativeSarcophagus(bool armed)
    {
        var corruptions = new Action<ObjectSnapshot>[] {
            value => value.LevelId = 0,
            value => value.NativeObjectId = 47,
            value => value.NativeChestInteractable = true,
            value => value.Activated = true,
            value => value.Destroyed = true,
            value => value.NativeTrapInteractable = !armed,
            value => value.NativeBarrelMeleeBreakable = true,
            value => value.NativeObjectId = 91,
            value => value.Breakable = true,
            value => value.IsShrine = true,
            value => value.EffectKind = 1,
            value => value.QuestId = 1,
            value => value.PresentationKind = Devilution.Protocol.V1.ObjectPresentationKind.Quest,
        };
        foreach (var corrupt in corruptions) {
            var value = Sarcophagus(); value.TrapArmed = armed; value.NativeTrapInteractable = armed;
            corrupt(value);
            Assert.False(AuthoritativeObjectSelector.IsValidNativeSarcophagusInteraction(value));
            Assert.Throws<InvalidDataException>(() => Apply(value));
        }
    }

    private static Snapshot Apply(ObjectSnapshot value, uint level = 4)
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = level, VisitedLevelIds = { 0, level } } },
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
