using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeProceduralLevelCatalogTests
{
    [Fact]
    public void GeneratesDeterministicConnectedGeometry()
    {
        var catalog = AuthoritativeProceduralLevelCatalog.LoadTsv(
            "procedural_levels.tsv",
            "level_id\tlevel_symbol\twidth\theight\tgeneration_seed\tblocked_percent\tmissile_blocked_percent\tdefault_start\n"
                + "7\tgenerated_test\t25\t21\t1234\t65\t70\t1\n");

        var first = catalog.GenerateWorld(0xA5A5A5A5).Levels.Single();
        var second = catalog.GenerateWorld(0xA5A5A5A5).Levels.Single();

        Assert.Equal(first.BlockedCells, second.BlockedCells);
        Assert.Equal(first.MissileBlockedCells, second.MissileBlockedCells);
        Assert.Equal(first.NativeVisionBlockedCells, second.NativeVisionBlockedCells);
        Assert.False(first.HasExplicitNativeVisionBlockedCells);
        Assert.Equal("generated_test", first.StableSymbol);
        Assert.True(first.IsDefaultStart);
        AssertConnected(first);
    }

    [Fact]
    public void PreservesConfiguredStartPositionAcrossDeterministicGeneration()
    {
        var catalog = new AuthoritativeProceduralLevelCatalog([
            new AuthoritativeProceduralLevelDefinition(
                7,
                25,
                21,
                1234,
                65,
                StartPositionX: 2,
                StartPositionY: 3),
        ]);

        var level = catalog.GenerateWorld(0xA5A5A5A5).Levels.Single();

        Assert.Equal((2, 3), (level.StartPositionX, level.StartPositionY));
        Assert.True(level.IsWalkable(level.StartPositionX, level.StartPositionY));
    }

    [Fact]
    public void ClearsConfiguredStartPositionFromMovementAndMissileBlockers()
    {
        var catalog = new AuthoritativeProceduralLevelCatalog([
            new AuthoritativeProceduralLevelDefinition(
                7,
                40,
                40,
                0xA5A5A5A5,
                80,
                MissileBlockedPercent: 80,
                StartPositionX: 2,
                StartPositionY: 2),
        ]);

        var level = catalog.GenerateWorld().Levels.Single();
        var startCell = level.StartPositionY * level.Width + level.StartPositionX;

        Assert.DoesNotContain(startCell, level.BlockedCells);
        Assert.DoesNotContain(startCell, level.MissileBlockedCells);
        Assert.True(level.IsWalkable(level.StartPositionX, level.StartPositionY));
    }

    [Fact]
    public void OptionalMissileDensityAddsIndependentOcclusion()
    {
        var catalog = new AuthoritativeProceduralLevelCatalog([
            new AuthoritativeProceduralLevelDefinition(
                7,
                25,
                21,
                1234,
                0,
                MissileBlockedPercent: 70),
        ]);

        var level = catalog.GenerateWorld(0).Levels.Single();

        Assert.Empty(level.BlockedCells);
        Assert.NotEmpty(level.MissileBlockedCells);
        Assert.All(level.MissileBlockedCells, cell =>
            Assert.True(level.IsWalkable(cell % level.Width, cell / level.Width)));
    }

    [Fact]
    public void OptionalVisionDensityAddsIndependentNativeBlockers()
    {
        var catalog = AuthoritativeProceduralLevelCatalog.LoadTsv(
            "procedural_levels.tsv",
            "level_id\twidth\theight\tgeneration_seed\tblocked_percent\tmissile_blocked_percent\tvision_blocked_percent\n"
                + "7\t25\t21\t1234\t0\t0\t70\n");

        var level = catalog.GenerateWorld().Levels.Single();

        Assert.Empty(level.BlockedCells);
        Assert.Empty(level.MissileBlockedCells);
        Assert.NotEmpty(level.NativeVisionBlockedCells);
        Assert.True(level.HasExplicitNativeVisionBlockedCells);
        Assert.All(level.NativeVisionBlockedCells, cell =>
            Assert.DoesNotContain(cell, level.BlockedCells));
    }

    [Fact]
    public void WorldSeedChangesGeneratedLayoutWithoutChangingTheContract()
    {
        var catalog = new AuthoritativeProceduralLevelCatalog([
            new AuthoritativeProceduralLevelDefinition(7, 25, 21, 1234, 50),
        ]);

        var first = catalog.GenerateWorld(1).Levels.Single();
        var second = catalog.GenerateWorld(2).Levels.Single();

        Assert.NotEqual(first.BlockedCells, second.BlockedCells);
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);
    }

    [Fact]
    public void RejectsInvalidDensityDimensionsAndDuplicateLevels()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeProceduralLevelDefinition(1, 2, 10, 0, 10).Validate());
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeProceduralLevelDefinition(1, 113, 10, 0, 10).Validate());
        Assert.Equal(
            AuthoritativeLevel.NativeMaximumWidth,
            new AuthoritativeProceduralLevelDefinition(
                1,
                AuthoritativeLevel.NativeMaximumWidth,
                AuthoritativeLevel.NativeMaximumHeight,
                0,
                10).Validate().Width);
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeProceduralLevelDefinition(1, 10, 10, 0, 81).Validate());
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeProceduralLevelDefinition(
                1,
                10,
                10,
                0,
                10,
                StartPositionX: 10).Validate());
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeProceduralLevelDefinition(
                1,
                10,
                10,
                0,
                10,
                MissileBlockedPercent: 81).Validate());
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeProceduralLevelDefinition(
                1,
                10,
                10,
                0,
                10,
                VisionBlockedPercent: 81).Validate());
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeProceduralLevelCatalog([
                new AuthoritativeProceduralLevelDefinition(1, 10, 10, 0, 10),
                new AuthoritativeProceduralLevelDefinition(1, 12, 12, 1, 20),
            ]));
    }

    [Fact]
    public void RejectsNonBooleanFlagsInExternalRows()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeProceduralLevelCatalog.LoadTsv(
                "procedural_levels.tsv",
                "level_id\twidth\theight\tdefault_start\n"
                    + "7\t12\t12\t2\n"));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeProceduralLevelCatalog.LoadTsv(
                "procedural_levels.tsv",
                "level_id\twidth\theight\tsuppress_multiplayer_death_drops\n"
                    + "7\t12\t12\t-1\n"));
    }

    private static void AssertConnected(AuthoritativeLevel level)
    {
        var walkable = Enumerable.Range(0, level.Width * level.Height)
            .Where(cell => !level.BlockedCells.Contains(cell))
            .ToHashSet();
        var start = walkable.Min();
        var visited = new HashSet<int> { start };
        var pending = new Queue<int>([start]);
        while (pending.TryDequeue(out var cell)) {
            var x = cell % level.Width;
            var y = cell / level.Width;
            foreach (var neighbor in new[] {
                (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1),
            }) {
                if (!level.ContainsPosition(neighbor.Item1, neighbor.Item2))
                    continue;
                var candidate = neighbor.Item2 * level.Width + neighbor.Item1;
                if (walkable.Contains(candidate) && visited.Add(candidate))
                    pending.Enqueue(candidate);
            }
        }
        Assert.Equal(walkable, visited);
    }
}
