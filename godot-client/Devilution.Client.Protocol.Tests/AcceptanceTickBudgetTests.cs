using Devilution.Client.Acceptance;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AcceptanceTickBudgetTests
{
    [Theory]
    [InlineData(0, 0UL)]
    [InlineData(0.01, 1UL)]
    [InlineData(2, 40UL)]
    [InlineData(15, 300UL)]
    public void BudgetsAreReferenceSecondsAtTwentyHertz(double seconds, ulong ticks)
        => Assert.Equal(ticks, AcceptanceTickBudget.Ticks(seconds));

    [Fact]
    public void NegativeBudgetIsRejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => AcceptanceTickBudget.Ticks(-1));

    [Theory]
    [InlineData(null, 20U)]
    [InlineData(0U, 20U)]
    [InlineData(60U, 60U)]
    public void MissingRateFallsBackToReference(uint? announced, uint effective)
        => Assert.Equal(effective, AcceptanceTickBudget.EffectiveRate(announced));

    [Fact]
    public void WallBackstopScalesWithTheServerRate()
    {
        Assert.Equal(40, AcceptanceTickBudget.WallBackstopSeconds(300, 20));
        Assert.Equal(20, AcceptanceTickBudget.WallBackstopSeconds(300, 60));
        Assert.Equal(610, AcceptanceTickBudget.WallBackstopSeconds(300, 0));
    }

    [Fact]
    public void BudgetExpiresOnlyAfterItsLastTick()
    {
        Assert.False(AcceptanceTickBudget.Expired(140, 100, 40));
        Assert.True(AcceptanceTickBudget.Expired(141, 100, 40));
        Assert.False(AcceptanceTickBudget.Expired(ulong.MaxValue - 1, ulong.MaxValue - 41, 40));
    }

    [Theory]
    [InlineData(9.9, false)]
    [InlineData(10, true)]
    public void ReferenceRateStagesKeepTheirWallTimeLimit(double elapsed, bool expired)
    {
        Assert.Equal(expired, AcceptanceTickBudget.StageExpired(10, elapsed, 20, 100, 100_000));
        Assert.Equal(expired, AcceptanceTickBudget.StageExpired(10, elapsed, null, 100, 100_000));
    }

    [Fact]
    public void StagesBeforeTheFirstSnapshotUseWallTime()
    {
        Assert.False(AcceptanceTickBudget.StageExpired(10, 9, 60, null, 5_000));
        Assert.True(AcceptanceTickBudget.StageExpired(10, 10, 60, 100, null));
    }

    [Fact]
    public void FasterServersSpendTheStageInGameTicks()
    {
        // 10 reference seconds are 200 ticks; at 60 Hz those take about 3.3 s.
        Assert.False(AcceptanceTickBudget.StageExpired(10, 3, 60, 1_000, 1_200));
        Assert.True(AcceptanceTickBudget.StageExpired(10, 3, 60, 1_000, 1_201));
        // A server that falls behind keeps the stage open until the 16.7 s backstop.
        Assert.False(AcceptanceTickBudget.StageExpired(10, 16, 60, 1_000, 1_050));
        Assert.True(AcceptanceTickBudget.StageExpired(10, 16.7, 60, 1_000, 1_050));
    }

    [Fact]
    public void AStallCannotSpendAFastStageBeforeTheWallFloor()
    {
        // 10 reference seconds are 200 ticks, 1 s at 200 Hz; a stall can deliver them in one snapshot.
        Assert.False(AcceptanceTickBudget.StageExpired(10, 0, 200, 12_076, 16_749));
        Assert.False(AcceptanceTickBudget.StageExpired(10, 2.9, 200, 1_000, 1_201));
        Assert.True(AcceptanceTickBudget.StageExpired(10, 3, 200, 1_000, 1_201));
        Assert.False(AcceptanceTickBudget.StageExpired(10, 11.9, 200, 1_000, 1_100));
        Assert.True(AcceptanceTickBudget.StageExpired(10, 12, 200, 1_000, 1_100));
    }

    [Theory]
    [InlineData(10, 2.9, false)]
    [InlineData(10, 3, true)]
    [InlineData(2, 1.9, false)]
    [InlineData(2, 2, true)]
    public void WaitsMayEndOnTicksOnlyPastTheWallFloor(double seconds, double elapsed, bool past)
        => Assert.Equal(past, AcceptanceTickBudget.PastWallFloor(seconds, elapsed));

    [Fact]
    public void TheWallFloorNeverExceedsAShortStage()
    {
        Assert.False(AcceptanceTickBudget.StageExpired(1, 0.9, 200, 1_000, 1_021));
        Assert.True(AcceptanceTickBudget.StageExpired(1, 1, 200, 1_000, 1_021));
        Assert.True(AcceptanceTickBudget.StageExpired(0, 0, 200, 1_000, 1_001));
    }

    [Fact]
    public void SlowerServersAlsoSpendTheStageInGameTicks()
    {
        Assert.False(AcceptanceTickBudget.StageExpired(10, 15, 10, 0, 150));
        Assert.True(AcceptanceTickBudget.StageExpired(10, 15, 10, 0, 201));
        Assert.True(AcceptanceTickBudget.StageExpired(10, 50, 10, 0, 150));
    }

    [Fact]
    public void ReactionSummaryReportsOrderStatistics()
    {
        Assert.Null(AcceptanceTickBudget.Summarize([]));
        Assert.Equal(new AcceptanceReactionSummary(1, 4, 4, 4, 4), AcceptanceTickBudget.Summarize([4]));
        Assert.Equal(new AcceptanceReactionSummary(10, 5.5, 6, 10, 10),
            AcceptanceTickBudget.Summarize([10, 1, 9, 2, 8, 3, 7, 4, 6, 5]));
    }
}
