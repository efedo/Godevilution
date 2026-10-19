using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeNativeObjectLayerStyleTests
{
    [Fact]
    public void EmptyObjectCellStaysInvisible()
    {
        var style = AuthoritativeNativeObjectLayerStyle.Resolve(0);

        Assert.False(style.IsVisible);
        Assert.Equal((byte)0, style.Alpha);
    }

    [Fact]
    public void ObjectIdTintIsStableAndDistinguishesIds()
    {
        var first = AuthoritativeNativeObjectLayerStyle.Resolve(17);
        var repeat = AuthoritativeNativeObjectLayerStyle.Resolve(17);
        var other = AuthoritativeNativeObjectLayerStyle.Resolve(18);

        Assert.Equal(first, repeat);
        Assert.NotEqual(first, other);
        Assert.True(first.IsVisible);
    }
}
