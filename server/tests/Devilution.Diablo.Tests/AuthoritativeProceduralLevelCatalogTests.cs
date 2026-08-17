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
            "level_id\tlevel_symbol\twidth\theight\tgeneration_seed\tblocked_percent\tdefault_start\n"
                + "7\tgenerated_test\t25\t21\t1234\t65\t1\n");

        var first = catalog.GenerateWorld(0xA5A5A5A5).Levels.Single();
        var second = catalog.GenerateWorld(0xA5A5A5A5).Levels.Single();

        Assert.Equal(first.BlockedCells, second.BlockedCells);
        Assert.Equal(first.MissileBlockedCells, second.MissileBlockedCells);
        Assert.Equal("generated_test", first.StableSymbol);
        Assert.True(first.IsDefaultStart);
        AssertConnected(first);
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
            new AuthoritativeProceduralLevelDefinition(1, 10, 10, 0, 81).Validate());
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeProceduralLevelCatalog([
                new AuthoritativeProceduralLevelDefinition(1, 10, 10, 0, 10),
                new AuthoritativeProceduralLevelDefinition(1, 12, 12, 1, 20),
            ]));
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
