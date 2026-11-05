using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeObjectAcceptanceControlTests
{
    [Fact]
    public async Task AlreadyStoppedTravelDoesNotRevealOrClickTheDisabledControl()
    {
        var prepared = false; var clicked = false;
        Assert.False(await AcceptanceControlClick.WhenEnabled(() => false,
            () => { prepared = true; return Task.CompletedTask; },
            () => { clicked = true; return Task.CompletedTask; }));
        Assert.False(prepared);
        Assert.False(clicked);
    }

    [Fact]
    public async Task TravelStoppingDuringRevealOrHoverDoesNotClickTheDisabledControl()
    {
        var enabled = true; var clicked = false;
        var preparation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = AcceptanceControlClick.WhenEnabled(() => enabled, () => preparation.Task,
            () => { clicked = true; return Task.CompletedTask; });
        Assert.False(operation.IsCompleted);
        enabled = false;
        preparation.SetResult();
        Assert.False(await operation);
        Assert.False(clicked);
    }

    [Fact]
    public async Task ActiveTravelGetsOneRealPressAndRelease()
    {
        var enabled = true; var clicks = 0;
        Assert.True(await AcceptanceControlClick.WhenEnabled(() => enabled, () => Task.CompletedTask,
            () => { clicks++; enabled = false; return Task.CompletedTask; }));
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void TheChestBudgetAccumulatesAcrossFloorsWithoutChargingOrdinaryTravel()
    {
        var now = 0.0;
        var budget = new AcceptanceStageBudget("Catacombs chest and pickup", 60, () => now);
        using (budget.Enter()) { now = 20; budget.Check(); }
        now = 1000; // Ordinary travel is outside the object scope.
        using (budget.Enter()) { now = 1039; budget.Check(); }
        Assert.Equal(59, budget.ElapsedSeconds);
        using (budget.Enter()) {
            now++;
            var exception = Assert.Throws<TimeoutException>(budget.Check);
            Assert.Contains("Catacombs chest and pickup", exception.Message);
            Assert.Contains("60s", exception.Message);
        }
        Assert.Throws<TimeoutException>(() => budget.Enter());
    }

    [Fact]
    public void BudgetExpirationEscapesOrdinaryUnreachableObjectRecoveryAndDisposesOnce()
    {
        var now = 0.0; var stopped = 0;
        var budget = new AcceptanceStageBudget("Required Catacombs door", 180, () => now);
        var scope = budget.Enter(() => stopped++);
        Assert.Throws<InvalidOperationException>(() => budget.Enter());
        now = 180;
        Assert.Throws<TimeoutException>(budget.Check);
        scope.Dispose(); scope.Dispose();
        Assert.Equal(1, stopped);
        Assert.Equal(180, budget.ElapsedSeconds);
    }

    [Fact]
    public void NormalJourneyHasNoDoorDestinationEvenWhenSeededCoordinatesExist()
    {
        Assert.Null(NativeCatacombsRequiredDoor.ForRun(false, null, null, null));
        Assert.Null(NativeCatacombsRequiredDoor.ForRun(false, "7", "68", "74"));
        Assert.Equal(new NativeCatacombsRequiredDoor(7, 68, 74),
            NativeCatacombsRequiredDoor.ForRun(true, "7", "68", "74"));
    }

    [Fact]
    public void RequiredDoorTargetsExactlyTheLaunchersFloorAndTile()
    {
        var target = NativeCatacombsRequiredDoor.Parse("7", "68", "74");
        var door = new ObjectSnapshot { LevelId = 7, NativeObjectId = 42, PositionX = 68, PositionY = 74 };
        Assert.True(target.Matches(door));
        door.NativeObjectId = 43;
        Assert.True(target.Matches(door));
        door.LevelId = 6;
        Assert.False(target.Matches(door));
        door.LevelId = 7; door.PositionX++;
        Assert.False(target.Matches(door));
        door.PositionX--; door.NativeObjectId = 5;
        Assert.False(target.Matches(door));
    }

    [Theory]
    [InlineData(null, "68", "74")]
    [InlineData("4", "68", "74")]
    [InlineData("7", "-1", "74")]
    [InlineData("7", "68", "112")]
    public void RequiredDoorRefusesMissingOrInvalidLauncherCoordinates(string? level, string? x, string? y)
        => Assert.Throws<InvalidDataException>(() => NativeCatacombsRequiredDoor.Parse(level, x, y));
}
