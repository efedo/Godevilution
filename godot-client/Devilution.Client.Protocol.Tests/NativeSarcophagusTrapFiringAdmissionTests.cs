using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeSarcophagusTrapFiringAdmissionTests
{
    [Theory]
    [InlineData(1U, 53U, false)] [InlineData(1U, 53U, true)]
    [InlineData(1U, 54U, false)] [InlineData(1U, 54U, true)]
    [InlineData(4U, 53U, false)] [InlineData(4U, 53U, true)]
    [InlineData(4U, 54U, false)] [InlineData(4U, 54U, true)]
    public void OpenedSarcophagusFiredLinkEntersTheClientAndKeepsLegacyTickAbsence(uint level, uint sourceType, bool timed)
    {
        var snapshot = Fired();
        snapshot.Players[0].LevelId = level;
        snapshot.Players[0].VisitedLevelIds.Clear(); snapshot.Players[0].VisitedLevelIds.Add([0, level]);
        foreach (var value in snapshot.Objects) value.LevelId = level;
        snapshot.Objects[1].NativeObjectId = sourceType;
        if (!timed) snapshot.Objects[1].ClearNativeTrapFiredTick();
        var decoded = Snapshot.Parser.ParseFrom(snapshot.ToByteArray());
        AuthoritativeNativeTrapFiringState.Validate(decoded.Objects, decoded.Tick);
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: decoded));
        var source = model.Snapshot!.Objects.Single(value => value.EntityId == 21);
        Assert.Equal(20U, source.NativeTrapFiredTriggerEntityId);
        Assert.Equal(timed, source.HasNativeTrapFiredTick);
        Assert.False(Assert.Single(model.Snapshot.Objects, value => value.EntityId == 20).TrapArmed);
    }

    [Theory]
    [InlineData(1U)] [InlineData(2U)] [InlineData(5U)] [InlineData(6U)]
    [InlineData(7U)] [InlineData(42U)] [InlineData(43U)] [InlineData(48U)]
    public void ExistingDoorAndChestTriggerKindsRemainAdmittedAlongsideSarcophagi(uint type)
    {
        var snapshot = Fired(); snapshot.Objects[0].NativeObjectId = type;
        AuthoritativeNativeTrapFiringState.Validate(snapshot.Objects, snapshot.Tick);
    }

    [Theory]
    [InlineData(0U)] [InlineData(3U)] [InlineData(47U)] [InlineData(49U)]
    [InlineData(57U)] [InlineData(59U)] [InlineData(68U)] [InlineData(91U)]
    public void UnsupportedTriggerKindsStillFailProtocolAndClientAdmission(uint type)
    {
        var snapshot = Fired(); snapshot.Objects[0].NativeObjectId = type;
        Reject(snapshot);
    }

    [Fact]
    public void SarcophagusUsesTheSameSourceFloorDisarmedAndTickRulesAsAChest()
    {
        foreach (var type in new uint[] {5, 48}) {
            foreach (var corrupt in new Action<Snapshot>[] {
                value => value.Objects[1].NativeObjectId = 55,
                value => value.Objects[1].Destroyed = true,
                value => { value.Objects[0].LevelId = 0; value.Objects[1].LevelId = 0; },
                value => value.Objects[1].LevelId++,
                value => value.Objects.RemoveAt(0),
                value => value.Objects[0].Destroyed = true,
                value => value.Objects[0].TrapArmed = true,
                value => value.Objects[1].NativeTrapFiredTick = value.Tick + 1,
                value => value.Objects[1].NativeTrapFiredTriggerEntityId = 0,
            }) {
                var snapshot = Fired(); snapshot.Objects[0].NativeObjectId = type; corrupt(snapshot);
                Reject(snapshot);
            }
        }
    }

    [Fact]
    public void UnfiredLegacyObjectsRemainAdmissibleWithoutAddingWireFields()
    {
        var snapshot = Fired();
        snapshot.Objects[1].NativeTrapFiredTriggerEntityId = 0;
        snapshot.Objects[1].ClearNativeTrapFiredTick();
        AuthoritativeNativeTrapFiringState.Validate(snapshot.Objects, snapshot.Tick);
        var decoded = Snapshot.Parser.ParseFrom(snapshot.ToByteArray());
        Assert.False(decoded.Objects[1].HasNativeTrapFiredTick);
        Assert.Equal(0U, decoded.Objects[1].NativeTrapFiredTriggerEntityId);
        var model = new AuthoritativeClientModel(); model.Apply(new AuthoritativeClientMessage(Snapshot: decoded));
        Assert.Equal(snapshot.ToByteArray(), model.Snapshot!.ToByteArray());
    }

    private static void Reject(Snapshot snapshot)
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeTrapFiringState.Validate(snapshot.Objects, snapshot.Tick));
        var decoded = Snapshot.Parser.ParseFrom(snapshot.ToByteArray());
        Assert.Throws<InvalidDataException>(() => new AuthoritativeClientModel().Apply(new AuthoritativeClientMessage(Snapshot: decoded)));
    }

    private static Snapshot Fired() => new() {Tick = 100,
        Players = {new PlayerSnapshot {EntityId = 7, LevelId = 4, VisitedLevelIds = {0, 4}}},
        Objects = {
            new ObjectSnapshot {EntityId = 20, ObjectId = 48, NativeObjectId = 48, LevelId = 4, PositionX = 3, PositionY = 4,
                Activated = true, KeepVisibleAfterActivation = true, AnimationFrame = 8},
            new ObjectSnapshot {EntityId = 21, ObjectId = 54, NativeObjectId = 54, LevelId = 4, PositionX = 3, PositionY = 2,
                NativeTrapFiredTriggerEntityId = 20, NativeTrapFiredTick = 99},
        },
    };
}
