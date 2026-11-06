using Devilution.Diablo;
using Devilution.Server.Protocol;
using Devilution.Tests;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class NativeShrineHostPublicationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task LegacyNativeShrineFlagPublishesTheFirstSnapshot(int depth)
    {
        var fixture = new NativeShrineTestFixture(depth, 13);
        // Native population builders have always retained false in the legacy authored-shrine field.
        Assert.False(fixture.Shrine.IsShrine);
        var hero = NativeShrineStartupTests.CreateWarriorL8();
        var captured = fixture.Simulation.CapturePresentationSnapshot(hero, 32);
        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(captured, spells: fixture.Spells);
        Assert.True(snapshot.Objects.Single(value => value.EntityId == NativeShrineTestFixture.ShrineEntityId).IsShrine);
        AuthoritativeSnapshotValidator.Validate(snapshot);
        await using var host = new NativeTrapHostPublicationTests.SnapshotHost(32,
            tick => DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(hero, tick), spells: fixture.Spells),
            _ => { });
        await host.ConnectAsync();
        Assert.Equal(32UL, host.Model.Snapshot!.Tick);
        Assert.Equal(18U, host.Model.Snapshot.Players[0].CharacterLevel);
        Assert.Equal(0U, host.Model.Snapshot.Players[0].LevelId);
        Assert.False(fixture.Shrine.IsShrine);
        Assert.Null(host.Server.LastClientFailure);
    }

    [Fact]
    public async Task RejectedFirstSnapshotReportsItsFenceAndClosesTheConnection()
    {
        var fixture = new NativeShrineTestFixture(1, 1);
        var hero = Player(fixture, 1);
        var observed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new NativeTrapHostPublicationTests.SnapshotHost(32, tick => {
            var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(hero, tick), spells: fixture.Spells);
            // Reproduce the invalid initial projection even after the mapper is repaired.
            snapshot.Objects.Single(value => value.EntityId == NativeShrineTestFixture.ShrineEntityId).IsShrine = false;
            return snapshot;
        }, _ => { }, failure => observed.TrySetResult(failure));
        try {
            var connection = host.ConnectAsync();
            var primary = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await host.Server.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Contains("invalid object effect metadata", primary.Message);
            var disconnected = await Assert.ThrowsAnyAsync<Exception>(async () =>
                await connection.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.IsNotType<TimeoutException>(disconnected);
            Assert.IsNotAssignableFrom<OperationCanceledException>(disconnected);
            Assert.Same(primary, await observed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        } finally {
            try { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
            catch (InvalidDataException) { }
        }
    }
}
