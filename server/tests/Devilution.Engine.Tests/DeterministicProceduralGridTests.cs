using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicProceduralGridTests
{
    [Fact]
    public void GenerationIsStableAndNativeLayersContainMovementWalls()
    {
        var first = DeterministicProceduralGrid.Generate(25, 21, 1234, 65, 70);
        var second = DeterministicProceduralGrid.Generate(25, 21, 1234, 65, 70);

        Assert.Equal(25, first.Width);
        Assert.Equal(21, first.Height);
        Assert.Equal(first.BlockedCells, second.BlockedCells);
        Assert.Equal(first.MissileBlockedCells, second.MissileBlockedCells);
        Assert.Equal(first.NativeVisionBlockedCells, second.NativeVisionBlockedCells);
        Assert.Subset(
            second.MissileBlockedCells.ToHashSet(),
            second.BlockedCells.ToHashSet());
        Assert.Subset(
            second.NativeVisionBlockedCells.ToHashSet(),
            second.BlockedCells.ToHashSet());
    }

    [Fact]
    public void SamplingRemainsOnTheConnectedPocketLattice()
    {
        var layout = DeterministicProceduralGrid.Generate(40, 40, 0xA5A5A5A5, 80, 80);

        Assert.NotEmpty(layout.BlockedCells);
        Assert.All(layout.BlockedCells, cell => {
            var x = cell % layout.Width;
            var y = cell / layout.Width;
            Assert.Equal(2, x % 4);
            Assert.Equal(2, y % 4);
        });
    }

    [Fact]
    public void RequiredWalkableCellsOverrideEveryGeneratedLayer()
    {
        var layout = DeterministicProceduralGrid.Generate(
            40,
            40,
            0xA5A5A5A5,
            80,
            80,
            [2 * 40 + 2, 6 * 40 + 6]);

        Assert.DoesNotContain(2 * 40 + 2, layout.BlockedCells);
        Assert.DoesNotContain(2 * 40 + 2, layout.MissileBlockedCells);
        Assert.DoesNotContain(6 * 40 + 6, layout.BlockedCells);
        Assert.DoesNotContain(6 * 40 + 6, layout.MissileBlockedCells);
        Assert.DoesNotContain(2 * 40 + 2, layout.NativeVisionBlockedCells);
        Assert.DoesNotContain(6 * 40 + 6, layout.NativeVisionBlockedCells);
    }

    [Fact]
    public void VisionDensityAddsIndependentNativeBlockersAndKeepsMovementWalls()
    {
        var layout = DeterministicProceduralGrid.Generate(
            40,
            40,
            0xA5A5A5A5,
            0,
            0,
            visionBlockedPercent: 80);

        Assert.Empty(layout.BlockedCells);
        Assert.Empty(layout.MissileBlockedCells);
        Assert.NotEmpty(layout.NativeVisionBlockedCells);
        Assert.All(layout.NativeVisionBlockedCells, cell =>
            Assert.DoesNotContain(cell, layout.BlockedCells));
    }

    [Theory]
    [InlineData(2, 10, 0, 0, 0)]
    [InlineData(10, 2, 0, 0, 0)]
    [InlineData(10, 10, 0, 81, 0)]
    [InlineData(10, 10, 0, 0, 81)]
    public void RejectsUnsupportedDimensionsAndDensities(
        int width,
        int height,
        int seed,
        int blockedPercent,
        int missileBlockedPercent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeterministicProceduralGrid.Generate(
                width,
                height,
                unchecked((uint)seed),
                blockedPercent,
                missileBlockedPercent));
    }

    [Fact]
    public void RejectsRequiredWalkableCellsOutsideTheGeneratedGrid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeterministicProceduralGrid.Generate(
                10,
                10,
                0,
                10,
                10,
                [100]));
    }

    [Fact]
    public void RejectsUnsupportedVisionDensity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeterministicProceduralGrid.Generate(10, 10, 0, 10, 10, visionBlockedPercent: 81));
    }
}
