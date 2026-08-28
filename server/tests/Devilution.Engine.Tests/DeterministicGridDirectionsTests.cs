using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicGridDirectionsTests
{
    [Fact]
    public void FromAdjacentStepUsesNativeDirectionOrder()
    {
        var expected = new[] {
            DeterministicGridDirection.South,
            DeterministicGridDirection.SouthWest,
            DeterministicGridDirection.West,
            DeterministicGridDirection.NorthWest,
            DeterministicGridDirection.North,
            DeterministicGridDirection.NorthEast,
            DeterministicGridDirection.East,
            DeterministicGridDirection.SouthEast,
        };
        var displacements = new[] {
            (1, 1),
            (0, 1),
            (-1, 1),
            (-1, 0),
            (-1, -1),
            (0, -1),
            (1, -1),
            (1, 0),
        };

        for (var index = 0; index < expected.Length; index++)
            Assert.Equal(expected[index], DeterministicGridDirections.FromAdjacentStep(
                displacements[index].Item1,
                displacements[index].Item2));
    }

    [Fact]
    public void FromAdjacentStepRejectsNonAdjacentDisplacements()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DeterministicGridDirections.FromAdjacentStep(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DeterministicGridDirections.FromAdjacentStep(2, 1));
    }

    [Fact]
    public void TowardUsesNativeSectorBoundaries()
    {
        Assert.Equal(
            DeterministicGridDirection.SouthEast,
            DeterministicGridDirections.Toward(0, 0, 10, 4));
        Assert.Equal(
            DeterministicGridDirection.SouthWest,
            DeterministicGridDirections.Toward(0, 0, 4, 10));
        Assert.Equal(
            DeterministicGridDirection.NorthWest,
            DeterministicGridDirections.Toward(0, 0, -10, -4));
    }

    [Fact]
    public void TowardDoesNotOverflowAtCoordinateBoundaries()
    {
        Assert.Equal(
            DeterministicGridDirection.North,
            DeterministicGridDirections.Toward(
                int.MaxValue,
                int.MaxValue,
                int.MinValue,
                int.MinValue));
    }

    [Fact]
    public void RotateLeftWrapsAcrossTheNativeDirectionRing()
    {
        Assert.Equal(
            DeterministicGridDirection.South,
            DeterministicGridDirections.RotateLeft(DeterministicGridDirection.South, 8));
        Assert.Equal(
            DeterministicGridDirection.NorthEast,
            DeterministicGridDirections.RotateLeft(DeterministicGridDirection.South, 3));
    }

    [Fact]
    public void GetDisplacementRejectsUnknownDirections()
    {
        Assert.Equal(
            (1, -1),
            DeterministicGridDirections.GetDisplacement(DeterministicGridDirection.East));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DeterministicGridDirections.GetDisplacement((DeterministicGridDirection)99));
    }
}
