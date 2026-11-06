using Devilution.Client.Acceptance;
using Devilution.Client.Protocol;
using WireObjectKind = Devilution.Protocol.V1.ObjectPresentationKind;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

/** Regression inputs use the mapper's actual Warrior/Rogue visibility policy. */
public sealed class NativeTrappedSarcophagusProjectionTests
{
    internal const uint Sarcophagus = 0x09032071;
    internal const uint WallTrap = 0x09032037;
    internal const uint ReservedSkeleton = 0x09031006;

    [Fact]
    public void RogueViewArmedSarcophagusUsesTrapPresentationAndRemainsSelectable()
    {
        var snapshot = Project(true);
        var target = snapshot.Objects[0];
        Assert.True(target.TrapArmed);
        Assert.True(target.NativeTrapInteractable);
        Assert.Equal(WireObjectKind.Trap, target.PresentationKind);
        var client = new AuthoritativeClientModel();
        client.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        Assert.Equal(target.EntityId, AuthoritativeObjectSelector.SelectAt(client.Snapshot!, 33, 62)?.EntityId);
    }

    [Fact]
    public void WarriorViewCanProveOpeningWithoutSeeingThePrivateArmedFlag()
    {
        var before = Project(false);
        var target = before.Objects[0];
        Assert.False(target.TrapArmed);
        Assert.False(target.NativeTrapInteractable);
        Assert.NotEqual(WireObjectKind.Trap, target.PresentationKind);
        Assert.True(target.NativeSarcophagusInteractable);
        Assert.True(NativeTrappedSarcophagusJourneyEvidence.CanOpen(target));
        var client = new AuthoritativeClientModel(); client.Apply(new AuthoritativeClientMessage(Snapshot: before));
        Assert.Equal(target.EntityId, AuthoritativeObjectSelector.SelectAt(client.Snapshot!, 33, 62)?.EntityId);
        var after = before.Clone(); after.Tick = 112;
        after.Objects[0].Activated = true; after.Objects[0].NativeSarcophagusInteractable = false;
        after.Objects[1].NativeTrapFiredTriggerEntityId = target.EntityId; after.Objects[1].NativeTrapFiredTick = 111;
        var observed = new NativeTrappedSarcophagusJourneyEvidence.Opening(before, target.EntityId).Find(after, 110, 210);
        Assert.Equal(new NativeTrappedSarcophagusJourneyEvidence.Firing(WallTrap, 111), observed);
        before.Objects[1].NativeTrapFiredTriggerEntityId = target.EntityId;
        before.Objects[1].NativeTrapFiredTick = 90;
        Assert.Null(new NativeTrappedSarcophagusJourneyEvidence.Opening(before, target.EntityId).Find(after, 110, 210));
    }

    private static Snapshot Project(bool rogueView)
    {
        var source = new DiabloSimulationSnapshot(100,
            new StorePlayerSnapshot(0, null, [], 0, 6400, 0, 0, PlayerAttributesState.Zero, [], [], []) {
                EntityId = 7, LevelId = 4, PositionX = 34, PositionY = 62,
                CharacterClassId = rogueView ? 1U : 0U,
                AbilityKind = rogueView ? DiabloPlayerAbilityKind.TrapDisarm : DiabloPlayerAbilityKind.None,
            }, [], [], [
                new DiabloObjectState(Sarcophagus, 48, 4, 33, 62, false, 0, AuthoritativeObjectEffectKind.None, 0) {
                    NativeObjectId = 48, TrapArmed = true, AnimationFrame = 1, HideWhenActivated = false,
                },
                new DiabloObjectState(WallTrap, 54, 4, 33, 44, false, 0, AuthoritativeObjectEffectKind.None, 0) { NativeObjectId = 54 },
            ], [], [], null) {
                NativeSarcophagusInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Create([Sarcophagus]),
                NativeTrapInteractableEntityIds = DiabloNativeChestInteractableEntitySet.Create([Sarcophagus]),
            };
        var wire = DiabloProtocolMapper.ToProtocolSnapshot(source, revealTraps: rogueView);
        return Snapshot.Parser.ParseFrom(wire.ToByteArray());
    }
}
