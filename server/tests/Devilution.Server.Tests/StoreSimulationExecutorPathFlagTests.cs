using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class StoreSimulationExecutorPathFlagTests
{
    [Theory]
    [InlineData(false, CommandRejectReason.Malformed)]
    [InlineData(true, CommandRejectReason.NotAllowed)]
    public void FollowPathRefusalsDoNotAdvanceOrMoveTheParticipant(
        bool targetCell,
        CommandRejectReason expectedReason)
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, Array.Empty<StoreItem>());
        var executor = new StoreSimulationExecutor(catalog, startingGold: 0);
        executor.InitializeParticipant("player", new AuthoritativeParticipantSelection(null));
        var before = executor.GetPlayerState("player");
        var tickBefore = executor.GetSessionStateTick("player");

        var result = executor.Execute("player", new Command {
            MoveRequested = new MoveRequested {
                TargetCell = targetCell,
                TargetX = before.PositionX + 5,
                TargetY = before.PositionY,
                DirectionX = 1,
                FollowPath = true,
            },
        }, appliedTick: checked(tickBefore + 1));

        var after = executor.GetPlayerState("player");
        Assert.False(result.Succeeded);
        Assert.Equal(expectedReason, result.RejectReason);
        Assert.Equal(tickBefore, executor.GetSessionStateTick("player"));
        Assert.Equal(before.PositionX, after.PositionX);
        Assert.Equal(before.PositionY, after.PositionY);
    }
}
