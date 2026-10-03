using Google.Protobuf;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class CathedralDoorProtocolProjectionTests
{
    [Fact]
    public void LegacyObjectWithoutTypedDoorPayloadKeepsItsWireBytes()
    {
        var legacy = new Snapshot {
            Tick = 4,
            Objects = {
                new ObjectSnapshot { EntityId = 2, ObjectId = 1, NativeObjectId = 1 },
            },
        };

        Assert.Equal(
            Convert.FromHexString("08043A0708021001880201"),
            legacy.ToByteArray());
        Assert.Null(legacy.Objects[0].CathedralDoor);
    }

    [Fact]
    public void RoundTripsExactTypedDoorFields()
    {
        var source = CreateSource(
            new DiabloCathedralDoorObjectState(
                2, 1, 4, 5, 1, 1, 43, 49, 3, 2, true, true, false));

        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source);
        var wire = Assert.Single(projected.Objects).CathedralDoor;
        Assert.NotNull(wire);
        Assert.Equal(43U, wire.CachedPiece);
        Assert.Equal(49U, wire.CachedNeighborPiece);
        Assert.True(wire.PreDraw);
        Assert.True(wire.MissilePassable);
        Assert.False(wire.Solid);

        var restored = DiabloProtocolMapper.FromProtocolSnapshot(projected);
        Assert.Equal(
            Assert.Single(source.CathedralDoorObjects),
            Assert.Single(restored.CathedralDoorObjects));
    }

    [Fact]
    public void RejectsTypedDoorWithInvalidNativeStateFlags()
    {
        var source = CreateSource(
            new DiabloCathedralDoorObjectState(
                2, 1, 4, 5, 1, 1, 43, 49, 3, 2, true, true, false));
        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source);
        projected.StateSha256 = string.Empty;
        projected.Objects[0].CathedralDoor!.SelectionRegion = 3;

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.FromProtocolSnapshot(projected));
    }

    [Fact]
    public void RejectsTypedDoorWithHybridInteractionMetadata()
    {
        var source = CreateSource(
            new DiabloCathedralDoorObjectState(
                2, 1, 4, 5, 1, 1, 43, 49, 3, 2, true, true, false));
        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source);
        projected.StateSha256 = string.Empty;
        projected.Objects[0].DialogueMessageId = 1;

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.FromProtocolSnapshot(projected));
    }

    [Fact]
    public void RejectsTypedDoorWithConflictingNativeSpecialLayer()
    {
        var source = CreateSource(
            new DiabloCathedralDoorObjectState(
                2, 1, 4, 5, 1, 1, 43, 49, 3, 2, true, true, false));
        var specialLayer = Enumerable.Repeat((ushort)0, 36).ToArray();
        specialLayer[5 * 6 + 4] = 8;
        var level = new AuthoritativeLevel(
            1,
            6,
            6,
            nativeSpecialLayer: specialLayer);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source, levelGeometry: [level]));
    }

    [Fact]
    public void AllowsTypedDoorWithUntypedNativeIdPeer()
    {
        var source = CreateSource(
            new DiabloCathedralDoorObjectState(
                2, 1, 4, 5, 1, 0, 43, 49, 1, 3, false, false, false)) with {
                    Objects = [
                        new DiabloObjectState(2, 1, 1, 4, 5, false, 0,
                            AuthoritativeObjectEffectKind.None, 0) {
                            NativeObjectId = 1,
                            AnimationFrame = 1,
                            HideWhenActivated = false,
                        },
                        new DiabloObjectState(3, 2, 1, 6, 5, false, 0,
                            AuthoritativeObjectEffectKind.None, 0) {
                            NativeObjectId = 2,
                            AnimationFrame = 2,
                        },
                    ],
                };

        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source);
        Assert.NotNull(projected.Objects.Single(value => value.EntityId == 2).CathedralDoor);
        Assert.Null(projected.Objects.Single(value => value.EntityId == 3).CathedralDoor);
    }

    private static DiabloSimulationSnapshot CreateSource(DiabloCathedralDoorObjectState door)
    {
        var player = new StorePlayerSnapshot(
            0, null, [], 0, 64, 0, 0, PlayerAttributesState.Zero, [], [], []) {
                EntityId = 1,
                LevelId = door.LevelId,
            };
        var @object = CreateSourceObject(
            door.EntityId,
            door.LevelId,
            door.PositionX,
            door.PositionY,
            door.AnimationFrame,
            door.MissilePassable);
        return new DiabloSimulationSnapshot(1, player, [], [], [@object], [], [], null) {
            CathedralDoorObjects = [door],
        };
    }

    private static DiabloObjectState CreateSourceObject(
        uint entityId,
        uint levelId,
        int positionX,
        int positionY,
        int animationFrame,
        bool missilePassable)
        => new(
            entityId, 1, levelId, positionX, positionY, false, 0,
            AuthoritativeObjectEffectKind.None, 0) {
                NativeObjectId = 1,
                AnimationFrame = animationFrame,
                MissilePassable = missilePassable,
                HideWhenActivated = false,
            };
}
