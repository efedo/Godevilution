using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralNativeVisionTests
{
    [Fact]
    public void ZeroRadiusMarksOnlyTheObserverWithoutChangingTransparency()
    {
        var seen = new List<AuthoritativeCathedralPopulationPoint>();
        var transparent = new List<AuthoritativeCathedralPopulationPoint>();
        AuthoritativeCathedralNativeVision.Cast(new(50, 50), 0, seen.Add,
            transparent.Add, _ => throw new InvalidOperationException("Zero-radius vision must not query walls."));
        Assert.Equal(new[] { new AuthoritativeCathedralPopulationPoint(50, 50) }, seen);
        Assert.Empty(transparent);
    }

    [Fact]
    public void BlockedDiagonalCannotLeakPastEitherAdjacentWall()
    {
        var seen = new HashSet<AuthoritativeCathedralPopulationPoint>();
        var transparent = new HashSet<AuthoritativeCathedralPopulationPoint>();
        var east = new AuthoritativeCathedralPopulationPoint(51, 50);
        var south = new AuthoritativeCathedralPopulationPoint(50, 51);
        AuthoritativeCathedralNativeVision.Cast(new(50, 50), 15, point => seen.Add(point),
            point => transparent.Add(point), point => point != east && point != south);
        Assert.Contains(east, seen);
        Assert.Contains(south, seen);
        Assert.DoesNotContain(east, transparent);
        Assert.DoesNotContain(south, transparent);
        Assert.DoesNotContain(new AuthoritativeCathedralPopulationPoint(51, 51), seen);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(111, 0)]
    [InlineData(0, 111)]
    [InlineData(111, 111)]
    public void FullRadiusClipsEveryCallbackAtNativeBounds(int x, int y)
    {
        static void RequireBounds(AuthoritativeCathedralPopulationPoint point)
        {
            Assert.InRange(point.X, 0, 111);
            Assert.InRange(point.Y, 0, 111);
        }
        var seen = new HashSet<AuthoritativeCathedralPopulationPoint>();
        AuthoritativeCathedralNativeVision.Cast(new(x, y), 15,
            point => { RequireBounds(point); seen.Add(point); }, RequireBounds,
            point => { RequireBounds(point); return true; });
        Assert.Contains(new AuthoritativeCathedralPopulationPoint(x, y), seen);
        Assert.True(seen.Count > 1);
    }

    [Fact]
    public void UnsafeOriginsAndRadiiRefuseBeforeAnyCallback()
    {
        static void Unexpected(AuthoritativeCathedralPopulationPoint _)
            => throw new InvalidOperationException("Invalid input reached a callback.");
        foreach (var origin in new[] { new AuthoritativeCathedralPopulationPoint(-1, 50), new(112, 50), new(50, -1), new(50, 112) })
            Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralNativeVision.Cast(
                origin, 1, Unexpected, Unexpected, _ => true));
        foreach (var radius in new[] { -1, 16, int.MaxValue })
            Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralNativeVision.Cast(
                new(50, 50), radius, Unexpected, Unexpected, _ => true));
    }
}
