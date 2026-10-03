using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralDoorTests
{
    [Fact]
    public void DiscoveryUsesNativeYMajorXMajorOrderAndImmediateClosedFields()
    {
        var level = CreateLevel([
            (AuthoritativeCathedralDoorKind.Right, 2, 1, (ushort)45, (ushort)42),
            (AuthoritativeCathedralDoorKind.Left, 4, 2, (ushort)50, (ushort)44),
            (AuthoritativeCathedralDoorKind.Left, 1, 4, (ushort)213, (ushort)53),
        ]);

        var discovered = AuthoritativeCathedralDoors.Discover(level);

        Assert.Equal(
            [(AuthoritativeCathedralDoorKind.Right, 2, 1),
             (AuthoritativeCathedralDoorKind.Left, 4, 2),
             (AuthoritativeCathedralDoorKind.Left, 1, 4)],
            discovered.Doors.Select(door => (door.Kind, door.PositionX, door.PositionY)));
        Assert.All(discovered.Doors, door => {
            Assert.Equal(AuthoritativeCathedralDoorStatus.Closed, door.Status);
            Assert.Equal(3, door.SelectionRegion);
            Assert.False(door.PreFlag);
            Assert.False(door.MissFlag);
            Assert.False(door.SolidFlag);
            Assert.Equal(door.Var1, level.NativePieceLayer[door.PositionY * level.Width + door.PositionX] + 1);
        });
        Assert.Equal(1, discovered.Doors[1].AnimationFrame);
        Assert.Equal(2, discovered.Doors[0].AnimationFrame);
    }

    [Theory]
    [InlineData(42, 391, 391)]
    [InlineData(44, 393, 393)]
    [InlineData(49, 410, 411)]
    [InlineData(53, 396, 396)]
    [InlineData(54, 397, 397)]
    [InlineData(60, 398, 398)]
    [InlineData(66, 399, 399)]
    [InlineData(67, 400, 400)]
    [InlineData(68, 402, 402)]
    [InlineData(69, 403, 403)]
    [InlineData(71, 405, 405)]
    [InlineData(211, 406, 406)]
    [InlineData(353, 408, 408)]
    [InlineData(354, 409, 409)]
    [InlineData(410, 395, 395)]
    [InlineData(411, 395, 395)]
    public void OpenUsesCurrentNeighborDoorSetMapping(
        int sourceNeighbor,
        int expectedLeft,
        int expectedRight)
    {
        var leftLevel = CreateLevel([
            (AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)sourceNeighbor),
        ]);
        var rightLevel = CreateLevel([
            (AuthoritativeCathedralDoorKind.Right, 4, 3, (ushort)45, (ushort)sourceNeighbor),
        ]);

        var left = AuthoritativeCathedralDoors.Discover(leftLevel);
        var right = AuthoritativeCathedralDoors.Discover(rightLevel);
        var leftOpen = AuthoritativeCathedralDoors.DeltaOpen(left.Level, Assert.Single(left.Doors));
        var rightOpen = AuthoritativeCathedralDoors.DeltaOpen(right.Level, Assert.Single(right.Doors));

        Assert.Equal((ushort)expectedLeft, leftOpen.Level.NativePieceLayer[2 * left.Level.Width + 3]);
        Assert.Equal((ushort)expectedRight, rightOpen.Level.NativePieceLayer[3 * right.Level.Width + 3]);
        Assert.Equal((ushort)7, leftOpen.Level.NativeSpecialLayer[3 * left.Level.Width + 3]);
        Assert.Equal((ushort)8, rightOpen.Level.NativeSpecialLayer[3 * right.Level.Width + 4]);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Open, leftOpen.Door.Status);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Open, rightOpen.Door.Status);
        Assert.Equal(2, leftOpen.Door.SelectionRegion);
        Assert.Equal(2, rightOpen.Door.SelectionRegion);
    }

    [Fact]
    public void OpenMainPieceUsesBloodPoolOnlyForVar1TwoHundredFourteen()
    {
        var ordinary = AuthoritativeCathedralDoors.Discover(CreateLevel([
            (AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)42),
        ]));
        var blood = AuthoritativeCathedralDoors.Discover(CreateLevel([
            (AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)213, (ushort)42),
        ]));

        var ordinaryOpen = AuthoritativeCathedralDoors.DeltaOpen(
            ordinary.Level, Assert.Single(ordinary.Doors));
        var bloodOpen = AuthoritativeCathedralDoors.DeltaOpen(
            blood.Level, Assert.Single(blood.Doors));

        Assert.Equal((ushort)392, ordinaryOpen.Level.NativePieceLayer[27]);
        Assert.Equal((ushort)407, bloodOpen.Level.NativePieceLayer[27]);
    }

    [Fact]
    public void SharedNeighborCloseUsesNativeNonRestorationException()
    {
        var level = CreateLevel([
            (AuthoritativeCathedralDoorKind.Right, 3, 1, (ushort)45, (ushort)49),
            (AuthoritativeCathedralDoorKind.Left, 2, 2, (ushort)43, (ushort)49),
        ]);
        var discovered = AuthoritativeCathedralDoors.Discover(level);
        var right = Assert.Single(discovered.Doors,
            door => door.Kind == AuthoritativeCathedralDoorKind.Right);
        var left = Assert.Single(discovered.Doors,
            door => door.Kind == AuthoritativeCathedralDoorKind.Left);

        var rightOpen = AuthoritativeCathedralDoors.DeltaOpen(discovered.Level, right);
        var leftOpen = AuthoritativeCathedralDoors.DeltaOpen(rightOpen.Level, left);
        var rightClosed = AuthoritativeCathedralDoors.Operate(leftOpen.Level, rightOpen.Door, false);

        // Right Var2 is 50 and the second door's open mapping leaves piece 395.
        // Native SetDoorStateClosed intentionally restores 410, not original 49.
        Assert.Equal((ushort)410, rightClosed.Level.NativePieceLayer[1 * level.Width + 2]);
        Assert.NotEqual((ushort)49, rightClosed.Level.NativePieceLayer[1 * level.Width + 2]);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Closed, rightClosed.Door.Status);
    }

    [Fact]
    public void OccupiedCloseBecomesBlockedWithoutGeometryOrAnimationMutation()
    {
        var discovered = AuthoritativeCathedralDoors.Discover(CreateLevel([
            (AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)42),
        ]));
        var open = AuthoritativeCathedralDoors.DeltaOpen(
            discovered.Level, Assert.Single(discovered.Doors));

        var blocked = AuthoritativeCathedralDoors.Operate(open.Level, open.Door, true);

        Assert.Same(open.Level, blocked.Level);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Blocked, blocked.Door.Status);
        Assert.Equal(open.Door.AnimationFrame, blocked.Door.AnimationFrame);
        Assert.Equal(open.Door.SelectionRegion, blocked.Door.SelectionRegion);
        Assert.Equal(open.Door.PreFlag, blocked.Door.PreFlag);
        Assert.Equal(open.Door.MissFlag, blocked.Door.MissFlag);
    }

    [Fact]
    public void DeltaOpenAdvancesAlreadyOpenAndBlockedDoorsAndUsesCurrentNeighbor()
    {
        var discovered = AuthoritativeCathedralDoors.Discover(CreateLevel([
            (AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)49),
        ]));
        var closed = Assert.Single(discovered.Doors);
        var first = AuthoritativeCathedralDoors.DeltaOpen(discovered.Level, closed);
        var second = AuthoritativeCathedralDoors.DeltaOpen(first.Level, first.Door);
        var blocked = AuthoritativeCathedralDoors.DeltaOpen(
            first.Level,
            first.Door with { Status = AuthoritativeCathedralDoorStatus.Blocked });

        Assert.Equal(first.Door.AnimationFrame + 2, second.Door.AnimationFrame);
        Assert.Equal(second.Door.AnimationFrame, blocked.Door.AnimationFrame);
        Assert.Equal((ushort)395, second.Level.NativePieceLayer[2 * first.Level.Width + 3]);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Open, second.Door.Status);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Open, blocked.Door.Status);
    }

    [Fact]
    public void OccupancyProjectionDoesNotRewriteClosedDoor()
    {
        var discovered = AuthoritativeCathedralDoors.Discover(CreateLevel([
            (AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)42),
        ]));
        var closed = Assert.Single(discovered.Doors);
        var open = AuthoritativeCathedralDoors.UpdateOccupancy(closed, true);

        Assert.Equal(AuthoritativeCathedralDoorStatus.Closed, open.Status);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Blocked,
            AuthoritativeCathedralDoors.UpdateOccupancy(
                closed with {
                    Status = AuthoritativeCathedralDoorStatus.Open,
                    SelectionRegion = 2,
                    PreFlag = true,
                    MissFlag = true,
                }, true).Status);
        Assert.Equal(AuthoritativeCathedralDoorStatus.Open,
            AuthoritativeCathedralDoors.UpdateOccupancy(
                open with {
                    Status = AuthoritativeCathedralDoorStatus.Blocked,
                    SelectionRegion = 2,
                    PreFlag = true,
                    MissFlag = true,
                }, false).Status);
    }

    [Fact]
    public void SynchronizeNormalizesBlockedWithoutAnimationDelta()
    {
        var discovered = AuthoritativeCathedralDoors.Discover(CreateLevel([
            (AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)42),
        ]));
        var open = AuthoritativeCathedralDoors.DeltaOpen(
            discovered.Level, Assert.Single(discovered.Doors));
        var blocked = AuthoritativeCathedralDoors.Operate(open.Level, open.Door, true);
        var synced = AuthoritativeCathedralDoors.Synchronize(blocked.Level, blocked.Door);

        Assert.Equal(AuthoritativeCathedralDoorStatus.Open, synced.Door.Status);
        Assert.Equal(blocked.Door.AnimationFrame, synced.Door.AnimationFrame);
        Assert.Equal(2, synced.Door.SelectionRegion);
        Assert.True(synced.Door.PreFlag);
        Assert.True(synced.Door.MissFlag);
        Assert.Equal((ushort)392, synced.Level.NativePieceLayer[27]);
    }

    [Fact]
    public void DiscoveryAndOperationsDoNotMutateInputLayersOrReturnedLayers()
    {
        var source = CreateLevel([
            (AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)42),
        ]);
        var sourcePieces = source.NativePieceLayer.ToArray();
        var sourceSpecial = source.NativeSpecialLayer.ToArray();
        var discovered = AuthoritativeCathedralDoors.Discover(source);
        var opened = AuthoritativeCathedralDoors.DeltaOpen(
            discovered.Level, Assert.Single(discovered.Doors));

        Assert.Equal(sourcePieces, source.NativePieceLayer);
        Assert.Equal(sourceSpecial, source.NativeSpecialLayer);
        Assert.NotSame(source.NativePieceLayer, discovered.Level.NativePieceLayer);
        Assert.NotSame(discovered.Level.NativePieceLayer, opened.Level.NativePieceLayer);
        Assert.Equal((ushort)43, discovered.Level.NativePieceLayer[27]);
        Assert.Equal((ushort)392, opened.Level.NativePieceLayer[27]);
    }

    [Fact]
    public void PreservesAuthoredBlockersWhileRebuildingOwnedCollisionCells()
    {
        const int main = 27;
        const int neighbor = 19;
        const int authored = 0;
        var level = CreateLevel(
            [(AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)42)],
            blockedCells: [main, neighbor, authored],
            missileBlockedCells: [main, neighbor, authored],
            nativePieceCollisionCells: [main, neighbor],
            nativePieceMissileBlockedCells: [main, neighbor],
            nativeVisionBlockedCells: [main, neighbor, authored],
            solidDoorPieces: true);
        var discovered = AuthoritativeCathedralDoors.Discover(level);
        var opened = AuthoritativeCathedralDoors.DeltaOpen(
            discovered.Level, Assert.Single(discovered.Doors));

        Assert.Equal([authored], opened.Level.BlockedCells);
        Assert.Equal([authored], opened.Level.MissileBlockedCells);
        Assert.Contains(authored, opened.Level.NativeVisionBlockedCells);
        Assert.DoesNotContain(main, opened.Level.NativePieceCollisionCells);
        Assert.DoesNotContain(neighbor, opened.Level.NativePieceMissileBlockedCells);
    }

    [Fact]
    public void RejectsOutOfBoundsDoorNeighborAndMissingChangedPieceProperties()
    {
        var outOfBounds = CreateLevel(
            [(AuthoritativeCathedralDoorKind.Left, 3, 0, (ushort)43, (ushort)42)]);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralDoors.Discover(outOfBounds));

        var missingOpenPiece = CreateLevel(
            [(AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)42)],
            omitProperties: [392]);
        var discovered = AuthoritativeCathedralDoors.Discover(missingOpenPiece);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralDoors.DeltaOpen(
            discovered.Level, Assert.Single(discovered.Doors)));
    }

    [Fact]
    public void RejectsMalformedStateBeforeChangingGeometry()
    {
        var discovered = AuthoritativeCathedralDoors.Discover(CreateLevel([
            (AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)42),
        ]));
        var door = Assert.Single(discovered.Doors);
        var invalidVar = door with { Var1 = 0 };
        var invalidFlags = door with { SelectionRegion = 2 };

        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralDoors.Operate(
            discovered.Level, invalidVar, false));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralDoors.Operate(
            discovered.Level, invalidFlags, false));
        Assert.Equal((ushort)43, discovered.Level.NativePieceLayer[27]);
    }

    [Fact]
    public void InitialSpecialLayerUsesNativeDungeonPieceInitializationWhenAbsent()
    {
        var level = CreateLevel(
            [(AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)42)],
            nativeSpecialLayer: null,
            extraPieces: [(0, 0, (ushort)11), (1, 0, (ushort)10)]);

        var discovered = AuthoritativeCathedralDoors.Discover(level);

        Assert.Equal((ushort)1, discovered.Level.NativeSpecialLayer[0]);
        Assert.Equal((ushort)2, discovered.Level.NativeSpecialLayer[1]);
        Assert.Equal(level.NativeSpecialLayer, Array.Empty<ushort>());
    }

    [Fact]
    public void LightsAndPreexistingObjectsConsumeTheNativeDoorPool()
    {
        var level = CreateLevel([
            (AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)42),
        ], extraPieces: [(0, 0, (ushort)269)]);
        Assert.Single(AuthoritativeCathedralDoors.Discover(level, 125).Doors);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralDoors.Discover(level, 126));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralDoors.Discover(level, -1));
    }

    [Fact]
    public void RepeatedDeltaOverflowRejectsBeforeChangingGeometry()
    {
        var discovered = AuthoritativeCathedralDoors.Discover(CreateLevel([
            (AuthoritativeCathedralDoorKind.Left, 3, 3, (ushort)43, (ushort)42),
        ]));
        var opened = AuthoritativeCathedralDoors.DeltaOpen(discovered.Level, Assert.Single(discovered.Doors));
        var maximum = opened.Door with { AnimationFrame = byte.MaxValue };
        var before = opened.Level.NativePieceLayer.ToArray();
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralDoors.DeltaOpen(opened.Level, maximum));
        Assert.Equal(before, opened.Level.NativePieceLayer);
        Assert.Equal(253, AuthoritativeCathedralDoors.Operate(opened.Level, maximum, false).Door.AnimationFrame);
    }

    private static AuthoritativeLevel CreateLevel(
        IReadOnlyList<(AuthoritativeCathedralDoorKind Kind, int X, int Y, ushort Main, ushort Neighbor)> doors,
        IReadOnlyList<int>? blockedCells = null,
        IReadOnlyList<int>? missileBlockedCells = null,
        IReadOnlyList<int>? nativePieceCollisionCells = null,
        IReadOnlyList<int>? nativePieceMissileBlockedCells = null,
        IReadOnlyList<int>? nativeVisionBlockedCells = null,
        bool solidDoorPieces = false,
        IReadOnlyList<ushort>? nativeSpecialLayer = null,
        IReadOnlyList<(int X, int Y, ushort Piece)>? extraPieces = null,
        IReadOnlyList<ushort>? omitProperties = null)
    {
        const int width = 8;
        const int height = 8;
        var pieces = Enumerable.Repeat((ushort)0, width * height).ToArray();
        foreach (var (kind, x, y, main, neighbor) in doors) {
            pieces[y * width + x] = main;
            var neighborX = kind == AuthoritativeCathedralDoorKind.Left ? x : x - 1;
            var neighborY = kind == AuthoritativeCathedralDoorKind.Left ? y - 1 : y;
            if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
                pieces[neighborY * width + neighborX] = neighbor;
        }
        foreach (var (x, y, piece) in extraPieces ?? [])
            pieces[y * width + x] = piece;

        var required = new HashSet<ushort>(pieces);
        foreach (var (_, _, _, main, neighbor) in doors) {
            required.Add(main);
            required.Add(neighbor);
        }
        foreach (var piece in new ushort[] {
                     42, 43, 44, 45, 49, 50, 53, 54, 55, 60, 66, 67, 68, 69, 71,
                     211, 213, 353, 354, 391, 392, 393, 394, 395, 396, 397, 398,
                     399, 400, 402, 403, 405, 406, 407, 408, 409, 410, 411,
                 })
            required.Add(piece);
        foreach (var piece in omitProperties ?? [])
            required.Remove(piece);
        var properties = required.ToDictionary(
            piece => piece,
            piece => solidDoorPieces && piece is 43 or 45 or 50 or 55 or 213
                ? AuthoritativeDunTileFlags.Solid
                    | AuthoritativeDunTileFlags.BlockLight
                    | AuthoritativeDunTileFlags.BlockMissile
                : AuthoritativeDunTileFlags.None);
        return new AuthoritativeLevel(
            1,
            width,
            height,
            blockedCells,
            missileBlockedCells,
            nativePieceLayer: pieces,
            nativePieceProperties: properties,
            nativePieceCollisionCells: nativePieceCollisionCells,
            nativePieceMissileBlockedCells: nativePieceMissileBlockedCells,
            nativeVisionBlockedCells: nativeVisionBlockedCells,
            nativeSpecialLayer: nativeSpecialLayer);
    }
}
