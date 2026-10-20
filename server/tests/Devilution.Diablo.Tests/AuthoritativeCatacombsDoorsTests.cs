using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCatacombsDoorsTests
{
    [Fact]
    public void CatalogBindsOnlyCanonicalL2ObjectRowsAndDepthsFiveThroughEight()
    {
        var metadata = LoadMetadata();
        var catalog = AuthoritativeCatacombsDoorCatalog.Load(metadata);

        Assert.Equal("original-diablo.catacombs-l2-doors", AuthoritativeCatacombsDoorCatalog.ProfileId);
        Assert.Equal(new uint[] { 5, 6, 7, 8 }, catalog.Definitions.Select(value => value.Depth));
        Assert.All(catalog.Definitions, value => {
            Assert.Equal(42u, value.LeftNativeObjectId);
            Assert.Equal(43u, value.RightNativeObjectId);
        });
        Assert.True(catalog.Supports(metadata));
        Assert.Throws<InvalidDataException>(() => catalog.ForDepth(4));
        Assert.Throws<InvalidDataException>(() => catalog.ForDepth(9));
    }

    [Theory]
    [InlineData(42, 1, 537, 12, 5)]
    [InlineData(43, 2, 539, 16, 6)]
    public void L2DoorOpenBlockedCloseAndRecloseUpdateOnlyNativeDoorGeometry(
        int nativeType, int initialFrame, int closedPiece, int openPiece, int openSpecial)
    {
        var metadata = LoadMetadata();
        var catalog = AuthoritativeCatacombsDoorCatalog.Load(metadata);
        var kind = nativeType == 42 ? AuthoritativeCatacombsDoorKind.Left : AuthoritativeCatacombsDoorKind.Right;
        var (level, raw, value) = CreateBoundDoor(
            catalog, nativeType, initialFrame, (ushort)closedPiece, initialStatus: AuthoritativeCatacombsDoorStatus.Closed);
        var state = AuthoritativeCatacombsDoors.Bind(level, 5, catalog, raw, value);

        var opened = AuthoritativeCatacombsDoors.Operate(level, 5, catalog, state, occupied: false);
        Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, opened.Door.Status);
        Assert.Equal(initialFrame + 2, opened.Door.AnimationFrame);
        Assert.Equal(2, opened.Door.SelectionRegion);
        Assert.True(opened.Door.PreFlag);
        Assert.True(opened.Door.MissFlag);
        Assert.Equal((ushort)openPiece, opened.Level.NativePieceLayer[CenterCell(opened.Level)]);
        Assert.Equal((ushort)openSpecial, opened.Level.NativeSpecialLayer[CenterCell(opened.Level)]);
        Assert.Equal(level.NativeTransparency, opened.Level.NativeTransparency);
        Assert.DoesNotContain(CenterCell(opened.Level), opened.Level.NativePieceCollisionCells);
        Assert.DoesNotContain(CenterCell(opened.Level), opened.Level.NativePieceMissileBlockedCells);
        Assert.Contains(1, opened.Level.BlockedCells);
        Assert.Contains(1, opened.Level.MissileBlockedCells);
        Assert.Contains(1, opened.Level.NativeVisionBlockedCells);
        Assert.Equal(kind, opened.Door.Kind);

        var blocked = AuthoritativeCatacombsDoors.Operate(opened.Level, 5, catalog, opened.Door, occupied: true);
        Assert.Same(opened.Level, blocked.Level);
        Assert.Equal(AuthoritativeCatacombsDoorStatus.Blocked, blocked.Door.Status);
        Assert.Equal(opened.Door.AnimationFrame, blocked.Door.AnimationFrame);
        Assert.Equal(opened.Level.NativePieceLayer, blocked.Level.NativePieceLayer);
        Assert.Equal(opened.Level.NativeSpecialLayer, blocked.Level.NativeSpecialLayer);
        Assert.True(blocked.Door.PreFlag);
        Assert.True(blocked.Door.MissFlag);

        var reclosed = AuthoritativeCatacombsDoors.Operate(
            blocked.Level, 5, catalog, blocked.Door, occupied: false);
        Assert.Equal(AuthoritativeCatacombsDoorStatus.Closed, reclosed.Door.Status);
        Assert.Equal(initialFrame, reclosed.Door.AnimationFrame);
        Assert.Equal(3, reclosed.Door.SelectionRegion);
        Assert.False(reclosed.Door.PreFlag);
        Assert.False(reclosed.Door.MissFlag);
        Assert.Equal((ushort)closedPiece, reclosed.Level.NativePieceLayer[CenterCell(reclosed.Level)]);
        Assert.Equal((ushort)0, reclosed.Level.NativeSpecialLayer[CenterCell(reclosed.Level)]);
        Assert.Contains(CenterCell(reclosed.Level), reclosed.Level.NativePieceCollisionCells);
        Assert.Contains(CenterCell(reclosed.Level), reclosed.Level.NativePieceMissileBlockedCells);
        Assert.Contains(CenterCell(reclosed.Level), reclosed.Level.BlockedCells);
        Assert.Contains(CenterCell(reclosed.Level), reclosed.Level.MissileBlockedCells);
    }

    [Fact]
    public void BindingRequiresTheExplicitActiveNativeDoorEntityAndReadyGeometry()
    {
        var catalog = AuthoritativeCatacombsDoorCatalog.Load(LoadMetadata());
        var (level, raw, value) = CreateBoundDoor(
            catalog, 42, 1, AuthoritativeCatacombsDoors.ClosedLeftPiece,
            initialStatus: AuthoritativeCatacombsDoorStatus.Closed);

        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsDoors.Bind(
            level, 4, catalog, raw, value));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsDoors.Bind(
            level, 5, catalog, raw with { Type = 5 }, value));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsDoors.Bind(
            level, 5, catalog, raw, new AuthoritativeWorldObject(
                value.EntityId, value.ObjectId, 6, value.PositionX, value.PositionY,
                nativeObjectId: value.NativeObjectId, animationFrame: value.AnimationFrame)));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsDoors.Bind(
            level, 5, catalog, raw with { DoorFlag = false }, value));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsDoors.Bind(
            level, 5, catalog, raw with { AnimDelay = 3 }, value));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsDoors.Bind(
            CreateLevel(5, AuthoritativeCatacombsDoors.OpenLeftPiece), 5, catalog, raw, value));
    }

    [Fact]
    public void DoorBindingKeepsPopulationDepthSeparateFromStableWorldLevelId()
    {
        var catalog = AuthoritativeCatacombsDoorCatalog.Load(LoadMetadata());
        var (level, raw, value) = CreateBoundDoor(
            catalog, 42, 1, AuthoritativeCatacombsDoors.ClosedLeftPiece,
            initialStatus: AuthoritativeCatacombsDoorStatus.Closed,
            levelId: 700);

        var door = AuthoritativeCatacombsDoors.Bind(level, 5, catalog, raw, value);

        Assert.Equal(700u, door.LevelId);
        var opened = AuthoritativeCatacombsDoors.Operate(level, 5, catalog, door, occupied: false);
        Assert.Equal(700u, opened.Level.LevelId);
        Assert.Equal(700u, opened.Door.LevelId);
        Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, opened.Door.Status);
    }

    [Theory]
    [InlineData(42, 1)]
    [InlineData(43, 2)]
    public void StaticDoorSetupDelayIsIndependentOfCatalogAnimationDelay(int nativeType, int catalogDelay)
    {
        var metadata = LoadMetadata();
        Assert.Equal(catalogDelay, metadata.Population.Objects[nativeType].AnimDelay);
        var catalog = AuthoritativeCatacombsDoorCatalog.Load(metadata);
        var closedPiece = nativeType == 42
            ? AuthoritativeCatacombsDoors.ClosedLeftPiece
            : AuthoritativeCatacombsDoors.ClosedRightPiece;
        var (level, raw, value) = CreateBoundDoor(
            catalog, nativeType, nativeType == 42 ? 1 : 2, closedPiece,
            initialStatus: AuthoritativeCatacombsDoorStatus.Closed);

        Assert.Equal(1000, raw.AnimDelay);
        Assert.NotEqual(catalogDelay, raw.AnimDelay);
        Assert.Equal(nativeType == 42 ? AuthoritativeCatacombsDoorKind.Left : AuthoritativeCatacombsDoorKind.Right,
            AuthoritativeCatacombsDoors.Bind(level, 5, catalog, raw, value).Kind);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsDoors.Bind(
            level, 5, catalog, raw with { AnimDelay = catalogDelay }, value));
    }

    [Fact]
    public void OpeningDoorPreservesStaticObjectBlockerAtTheSameCell()
    {
        var catalog = AuthoritativeCatacombsDoorCatalog.Load(LoadMetadata());
        var (level, raw, value) = CreateBoundDoor(
            catalog, 42, 1, AuthoritativeCatacombsDoors.ClosedLeftPiece,
            initialStatus: AuthoritativeCatacombsDoorStatus.Closed,
            includeStaticBlockerAtDoor: true);
        var door = AuthoritativeCatacombsDoors.Bind(level, 5, catalog, raw, value);

        var opened = AuthoritativeCatacombsDoors.Operate(level, 5, catalog, door, occupied: false);

        Assert.Contains(CenterCell(opened.Level), opened.Level.StaticObjectBlockedCells);
        Assert.Contains(CenterCell(opened.Level), opened.Level.BlockedCells);
        Assert.DoesNotContain(CenterCell(opened.Level), opened.Level.NativePieceCollisionCells);
    }

    [Fact]
    public void DeltaOpenAndSynchronizeFollowNativeNoAnimationSyncSemantics()
    {
        var catalog = AuthoritativeCatacombsDoorCatalog.Load(LoadMetadata());
        var (level, raw, value) = CreateBoundDoor(
            catalog, 42, 1, AuthoritativeCatacombsDoors.ClosedLeftPiece,
            initialStatus: AuthoritativeCatacombsDoorStatus.Closed);
        var door = AuthoritativeCatacombsDoors.Bind(level, 5, catalog, raw, value);
        var firstOpen = AuthoritativeCatacombsDoors.DeltaOpen(level, 5, catalog, door);
        var repeatedOpen = AuthoritativeCatacombsDoors.DeltaOpen(firstOpen.Level, 5, catalog, firstOpen.Door);
        Assert.Equal(5, repeatedOpen.Door.AnimationFrame);
        var synchronized = AuthoritativeCatacombsDoors.Synchronize(
            repeatedOpen.Level, 5, catalog, repeatedOpen.Door);
        Assert.Equal(5, synchronized.Door.AnimationFrame);
        Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, synchronized.Door.Status);
        Assert.Equal((ushort)AuthoritativeCatacombsDoors.OpenLeftPiece,
            synchronized.Level.NativePieceLayer[CenterCell(synchronized.Level)]);
    }

    private static AuthoritativeCatacombsPopulationMetadata LoadMetadata()
    {
        var tables = AuthoritativeCatacombsPopulationMetadataTests.CreateCompleteTables()
            .Select(table => table.SourcePath == AuthoritativeCathedralPopulationCatalog.ObjectTableName
                ? CanonicalizeDoorDefinitions(table)
                : table)
            .ToArray();
        return AuthoritativeCatacombsPopulationMetadataTests.Load(tables);
    }

    private static Devilution.Data.TsvTable CanonicalizeDoorDefinitions(Devilution.Data.TsvTable table)
    {
        var columnIndexes = table.Columns
            .Select((column, index) => (column, index))
            .ToDictionary(value => value.column, value => value.index, StringComparer.Ordinal);
        var rows = table.Rows.Select(row => {
            var cells = table.Columns.Select(column => row[column]).ToArray();
            var nativeId = row.RequiredInt32("native_object_id");
            if (nativeId is 42 or 43) {
                cells[columnIndexes["flags"]] = "24";
                cells[columnIndexes["anim_delay"]] = nativeId == 42 ? "1" : "2";
                cells[columnIndexes["anim_length"]] = "0";
                cells[columnIndexes["anim_width"]] = "64";
                cells[columnIndexes["selection_region"]] = "3";
            }
            return string.Join('\t', cells);
        });
        var content = string.Join("\n", new[] { string.Join('\t', table.Columns) }.Concat(rows)) + "\n";
        return Devilution.Data.TsvTable.Parse(table.SourcePath, content);
    }

    private static (AuthoritativeLevel Level, AuthoritativeCathedralPopulationObjectState Raw,
        AuthoritativeWorldObject Value) CreateBoundDoor(
        AuthoritativeCatacombsDoorCatalog catalog,
        int nativeType,
        int animationFrame,
        ushort piece,
        AuthoritativeCatacombsDoorStatus initialStatus,
        uint levelId = 5,
        bool includeStaticBlockerAtDoor = false)
    {
        const int x = 3;
        const int y = 3;
        var level = CreateLevel(levelId, piece, includeStaticBlockerAtDoor);
        var raw = new AuthoritativeCathedralPopulationObjectState {
            Slot = 0,
            Type = nativeType,
            Position = new(x, y),
            AnimDelay = 1000,
            AnimFrame = (uint)animationFrame,
            AnimWidth = 64,
            SelectionRegion = 3,
            DoorFlag = true,
            Var4 = (int)initialStatus,
        };
        var value = new AuthoritativeWorldObject(
            entityId: 0x0A000001,
            objectId: 1,
            levelId: levelId,
            positionX: x,
            positionY: y,
            nativeObjectId: (uint)nativeType,
            animationFrame: animationFrame);
        return (level, raw, value);
    }

    private static AuthoritativeLevel CreateLevel(
        uint levelId, ushort centerPiece, bool includeStaticBlockerAtDoor = false)
    {
        const int width = 5;
        const int height = 5;
        const int cell = 3 * width + 3;
        var pieces = new ushort[width * height];
        pieces[cell] = centerPiece;
        var special = new ushort[pieces.Length];
        var properties = Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount)
            .ToDictionary(index => checked((ushort)index), _ => AuthoritativeDunTileFlags.None);
        properties[AuthoritativeCatacombsDoors.ClosedLeftPiece] =
            AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockLight
            | AuthoritativeDunTileFlags.BlockMissile;
        properties[AuthoritativeCatacombsDoors.ClosedRightPiece] =
            AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockLight
            | AuthoritativeDunTileFlags.BlockMissile;
        properties[AuthoritativeCatacombsDoors.OpenLeftPiece] = AuthoritativeDunTileFlags.None;
        properties[AuthoritativeCatacombsDoors.OpenRightPiece] = AuthoritativeDunTileFlags.None;
        return new AuthoritativeLevel(
            levelId, width, height,
            blockedCells: [cell, 1], missileBlockedCells: [cell, 1],
            nativeTransparency: new ushort[pieces.Length],
            nativePieceLayer: pieces,
            nativePieceProperties: properties,
            nativePieceCollisionCells: [cell],
            nativePieceMissileBlockedCells: [cell],
            nativeVisionBlockedCells: [cell, 1],
            nativeSpecialLayer: special,
            staticObjectBlockedCells: includeStaticBlockerAtDoor ? [cell] : []);
    }

    private static int CenterCell(AuthoritativeLevel level) => 3 * level.Width + 3;
}
