using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeSpecialLayerTests
{
    [Fact]
    public void LegacyClrConstructorSignaturesRemainAvailable()
    {
        var levelConstructor = typeof(AuthoritativeLevel).GetConstructor([
            typeof(uint), typeof(int), typeof(int), typeof(IEnumerable<int>), typeof(IEnumerable<int>),
            typeof(bool), typeof(bool), typeof(IEnumerable<ushort>), typeof(IEnumerable<ushort>),
            typeof(IEnumerable<ushort>), typeof(IEnumerable<ushort>),
            typeof(IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>), typeof(IEnumerable<int>),
            typeof(IEnumerable<int>), typeof(IEnumerable<ushort>), typeof(int), typeof(int), typeof(IEnumerable<int>),
        ]);
        Assert.NotNull(levelConstructor);
        var legacyLevel = Assert.IsType<AuthoritativeLevel>(levelConstructor.Invoke([
            1U, 2, 2, null, null, true, false, null, null, null, null, null,
            null, null, null, 1, 0, null,
        ]));
        var extendedLevel = new AuthoritativeLevel(1, 2, 2,
            null, null, true, false, null, null, null, null, null,
            null, null, null, 1, 0, null, null, null);
        Assert.Equal(extendedLevel.LevelId, legacyLevel.LevelId);
        Assert.Equal(extendedLevel.StartPositionX, legacyLevel.StartPositionX);
        Assert.Equal(extendedLevel.IsDefaultStart, legacyLevel.IsDefaultStart);
        Assert.Empty(legacyLevel.NativeSpecialLayer);
        Assert.Empty(legacyLevel.StaticObjectBlockedCells);
        var worldConstructor = typeof(DiabloWorldState).GetConstructor([
            typeof(AuthoritativeWorld), typeof(int), typeof(int), typeof(IEnumerable<int>),
            typeof(IReadOnlyList<AuthoritativeCombatTarget>), typeof(IReadOnlyList<AuthoritativePortal>),
            typeof(IReadOnlyList<AuthoritativeWorldItem>), typeof(IReadOnlyList<AuthoritativeWorldObject>),
            typeof(IReadOnlyList<AuthoritativeQuestState>), typeof(IReadOnlyList<AuthoritativeProjectile>),
            typeof(IEnumerable<int>), typeof(IReadOnlyList<AuthoritativeCorpse>),
            typeof(AuthoritativeWorldMutationCatalog), typeof(IEnumerable<uint>),
            typeof(AuthoritativeNativeDunObjectCatalog), typeof(IReadOnlyList<AuthoritativeWorldObject>),
            typeof(bool), typeof(bool),
        ]);
        Assert.NotNull(worldConstructor);
        var world = new AuthoritativeWorld();
        world.AddLevel(legacyLevel);
        var legacyState = Assert.IsType<DiabloWorldState>(worldConstructor.Invoke([
            world, 2, 2, null, null, null, null, null, null, null,
            null, null, null, null, null, null, false, false,
        ]));
        var extendedState = new DiabloWorldState(world, 2, 2,
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, false, false, null, null);
        Assert.Equal(Assert.Single(extendedState.LevelGeometry).LevelId,
            Assert.Single(legacyState.LevelGeometry).LevelId);
        Assert.Empty(Assert.Single(legacyState.LevelGeometry).NativeSpecialLayer);
    }

    [Fact]
    public void LegacyLevelsRetainAnAbsentSpecialLayer()
    {
        var level = new AuthoritativeLevel(1, 2, 2);
        Assert.Empty(level.NativeSpecialLayer);
    }

    [Fact]
    public void NativeSpecialBytesAreCopiedAndPublishedReadOnly()
    {
        var source = new ushort[] { 0, 7, 8, byte.MaxValue };
        var level = new AuthoritativeLevel(1, 2, 2, nativeSpecialLayer: source);
        source[1] = 0;
        Assert.Equal(new ushort[] { 0, 7, 8, byte.MaxValue }, level.NativeSpecialLayer);
        var mutable = Assert.IsAssignableFrom<IList<ushort>>(level.NativeSpecialLayer);
        Assert.Throws<NotSupportedException>(() => mutable[0] = 1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void PartialOrOversizedSpecialLayersAreRejected(int count)
        => Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(
            1, 2, 2, nativeSpecialLayer: new ushort[count]));

    [Fact]
    public void ValuesOutsideTheNativeByteRangeAreRejected()
        => Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(
            1, 2, 2, nativeSpecialLayer: new ushort[] { 0, 0, 0, 256 }));

    [Fact]
    public void StaticObjectCollisionIsCopiedBoundedAndDoesNotWidenVision()
    {
        var cells = new[] { 3, 1, 1 };
        var level = new AuthoritativeLevel(1, 2, 2, staticObjectBlockedCells: cells);
        cells[0] = 0;
        Assert.Equal(new[] { 1, 3 }, level.StaticObjectBlockedCells);
        Assert.Empty(level.BlockedCells);
        Assert.Empty(level.NativeVisionBlockedCells);
        Assert.Empty(level.MissileBlockedCells);
        Assert.Equal(level.StaticObjectBlockedCells, level.WithStartPosition(0, 0).StaticObjectBlockedCells);
        var world = new AuthoritativeWorld();
        world.AddLevel(level);
        Assert.Throws<InvalidDataException>(() => world.CaptureGeometryState());
        Assert.Throws<NotSupportedException>(() => ((IList<int>)level.StaticObjectBlockedCells)[0] = 0);
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(1, 2, 2, staticObjectBlockedCells: [4]));
    }

    [Fact]
    public void EntryPointAndEffectiveProjectionRetainSpecialBytes()
    {
        var source = new AuthoritativeLevel(1, 2, 2,
            nativeSpecialLayer: new ushort[] { 1, 7, 8, 2 });
        var movedStart = source.WithStartPosition(1, 1);
        Assert.Equal(source.NativeSpecialLayer, movedStart.NativeSpecialLayer);
        var world = new AuthoritativeWorld();
        world.AddLevel(movedStart);
        var state = new DiabloWorldState(world);
        Assert.Equal(source.NativeSpecialLayer,
            Assert.Single(state.LevelGeometry).NativeSpecialLayer);
        Assert.Throws<InvalidDataException>(() => state.CaptureCohortInfrastructure());
    }
}
