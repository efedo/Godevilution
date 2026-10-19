using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeNativeGeometryStyleTests
{
    [Fact]
    public void EmptyNativeCellStaysInvisible()
    {
        var style = AuthoritativeNativeGeometryStyle.Resolve(
            pieceId: 0,
            transparency: 0,
            blocked: false,
            missileBlocked: false);

        Assert.False(style.IsVisible);
        Assert.Equal((byte)0, style.Alpha);
    }

    [Fact]
    public void OrdinaryPieceTintIsStableAndDistinguishesIds()
    {
        var first = AuthoritativeNativeGeometryStyle.Resolve(17, 0, false, false);
        var repeat = AuthoritativeNativeGeometryStyle.Resolve(17, 0, false, false);
        var other = AuthoritativeNativeGeometryStyle.Resolve(18, 0, false, false);

        Assert.Equal(first, repeat);
        Assert.NotEqual(first, other);
        Assert.True(first.IsVisible);
    }

    [Fact]
    public void CollisionAndTransparencyStylesRemainDeterministic()
    {
        var blocked = AuthoritativeNativeGeometryStyle.Resolve(17, 3, true, true);
        var missileBlocked = AuthoritativeNativeGeometryStyle.Resolve(17, 3, false, true);
        var transparent = AuthoritativeNativeGeometryStyle.Resolve(17, 3, false, false);

        Assert.Equal((112, 54, 76, 92), (blocked.Red, blocked.Green, blocked.Blue, blocked.Alpha));
        Assert.Equal((142, 70, 122, 78), (missileBlocked.Red, missileBlocked.Green, missileBlocked.Blue, missileBlocked.Alpha));
        Assert.Equal((80, 142, 156, 66), (transparent.Red, transparent.Green, transparent.Blue, transparent.Alpha));
    }
}
