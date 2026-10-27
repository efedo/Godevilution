using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ScreenDirectionMovementTests
{
    [Theory]
    [InlineData(true, false, false, false, -1, -1)]
    [InlineData(false, true, false, false, 1, 1)]
    [InlineData(false, false, true, false, -1, 1)]
    [InlineData(false, false, false, true, 1, -1)]
    [InlineData(true, false, false, true, 0, -1)]
    [InlineData(true, false, true, false, -1, 0)]
    [InlineData(false, true, false, true, 1, 0)]
    [InlineData(false, true, true, false, 0, 1)]
    public void ScreenDirectionsBecomeNativeGridSteps(bool up, bool down, bool left, bool right, int x, int y)
        => Assert.Equal((x, y), ScreenDirectionMovement.Resolve(up, down, left, right));

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, true, true)]
    public void OpposedOrReleasedKeysDoNotMove(bool up, bool down, bool left, bool right)
        => Assert.Equal((0, 0), ScreenDirectionMovement.Resolve(up, down, left, right));
}
