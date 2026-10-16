using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeVisionMutableGeometryTests
{
    [Fact]
    public void ChangedNativeBlockerMaskIsObservedOnTheSameGeometryInstance()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 1, Width = 33, Height = 33 };
        geometry.NativeTransparency.Add(0);
        geometry.NativeVisionBlockedCells.Add(0);
        bool Visible() => AuthoritativeNativeVisionVisibility.IsVisible(1, 12, 10, 1, geometry, 10, 10, 10);
        Assert.True(Visible());
        geometry.NativeVisionBlockedCells.Add(10 * 33 + 11);
        Assert.False(Visible());
        geometry.NativeVisionBlockedCells.Remove(10 * 33 + 11);
        Assert.True(Visible());
        geometry.NativeVisionBlockedCells.Clear();
        geometry.BlockedCells.Add(10 * 33 + 11);
        Assert.False(Visible());
        geometry.BlockedCells.Clear();
        Assert.True(Visible());
    }
}
