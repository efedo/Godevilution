using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class LiveSnapshotReferenceTests
{
    [Fact]
    [Trait("Category", "Slow")]
    public async Task ScopedConnectionResumesTheUnchangedCohortOnAFreshHost()
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var firstHost = await StartedServer.StartAsync(cancellation, startingLevelOverride: 0,
            gameMode: "multiplayer", controlledClock: true, persistenceMode: "cohort", deleteSaveRootOnDispose: false);
        var options = CreateOptions(firstHost, "scoped-checkpoint", requestedClassId: 0) with {
            UseSnapshotReferences = true, UseLevelScopedSnapshots = true, UseLevelScopedIdleReferences = true,
            SnapshotPollInterval = TimeSpan.Zero,
        };
        string token, hash;
        Snapshot expected;
        await using (var client = new AuthoritativeClient(options)) {
            await client.ConnectAsync(cancellation);
            var initial = await NextSnapshotAsync(client, cancellation);
            var model = new AuthoritativeClientModel(); model.Apply(initial);
            expected = model.Snapshot!.Clone(); hash = expected.StateSha256; token = client.SessionToken;
        }
        firstHost.Dispose();
        using var freshHost = await StartedServer.StartAsync(cancellation, startingLevelOverride: 0,
            gameMode: "multiplayer", controlledClock: true, persistenceMode: "cohort", saveRootOverride: firstHost.SaveRoot);
        await using var resumed = new AuthoritativeClient(options with { Port = freshHost.Port, ResumeToken = token });
        await resumed.ConnectAsync(cancellation);
        var restored = await NextSnapshotAsync(resumed, cancellation);
        Assert.True(restored.LevelScopedSnapshots);
        Assert.False(restored.Snapshot!.GeometryUnchanged); Assert.False(restored.Snapshot.StateUnchanged);
        var resumedModel = new AuthoritativeClientModel(); resumedModel.Apply(restored);
        Assert.Equal(hash, resumedModel.Snapshot!.StateSha256);
        var actual = resumedModel.Snapshot.Clone(); actual.Tick = expected.Tick;
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false, CommandSchedulingMode.Immediate, "legacy")]
    [InlineData(true, CommandSchedulingMode.Immediate, "legacy")]
    [InlineData(false, CommandSchedulingMode.ClockGated, "legacy")]
    [InlineData(true, CommandSchedulingMode.ClockGated, "legacy")]
    [InlineData(false, CommandSchedulingMode.Immediate, "cohort")]
    [InlineData(true, CommandSchedulingMode.Immediate, "cohort")]
    [InlineData(false, CommandSchedulingMode.ClockGated, "cohort")]
    [InlineData(true, CommandSchedulingMode.ClockGated, "cohort")]
    [Trait("Category", "Slow")]
    public async Task RealHostScopesOnlyNegotiatedConnectionsAndReconnectStartsWithACompleteView(
        bool references, CommandSchedulingMode scheduling, string persistence)
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var server = await StartedServer.StartAsync(cancellation, startingLevelOverride: 0,
            controlledClock: true, gameMode: "multiplayer", commandSchedulingMode: scheduling, persistenceMode: persistence);
        var options = CreateOptions(server, "level-view-test", requestedClassId: 0) with {
            UseSnapshotReferences = references, UseLevelScopedIdleReferences = true,
            UseLevelScopedSnapshots = true, SnapshotPollInterval = TimeSpan.Zero,
        };
        await using var client = new AuthoritativeClient(options);
        await client.ConnectAsync(cancellation);
        var first = await NextSnapshotAsync(client, cancellation);
        Assert.True(first.LevelScopedSnapshots);
        Assert.NotEmpty(first.Snapshot!.PlayerRoster);
        var model = new AuthoritativeClientModel(); model.Apply(first);
        await using var oldPeer = new AuthoritativeClient(options with {
            UseLevelScopedSnapshots = false, UseSnapshotReferences = false,
        });
        await oldPeer.ConnectAsync(cancellation);
        var full = await NextSnapshotAsync(oldPeer, cancellation);
        Assert.False(full.LevelScopedSnapshots); Assert.Empty(full.Snapshot!.PlayerRoster);
        await client.PollAsync(cancellation);
        var updated = await NextSnapshotAsync(client, cancellation); model.Apply(updated);
        var level = model.Snapshot!.Players[0].LevelId;
        Assert.Equal(full.Snapshot.ParticipantCount, model.Snapshot.ParticipantCount);
        Assert.Equal(full.Snapshot.Monsters.Where(value => value.LevelId == level).Select(value => value.EntityId),
            model.Snapshot.Monsters.Select(value => value.EntityId));
        Assert.Equal(full.Snapshot.Objects.Where(value => value.LevelId == level).Select(value => value.EntityId),
            model.Snapshot.Objects.Select(value => value.EntityId));
        Assert.Equal(full.Snapshot.StateSha256, model.Snapshot.StateSha256);
        await client.PollAsync(cancellation);
        var idle = await NextSnapshotAsync(client, cancellation);
        Assert.Equal(references, idle.Snapshot!.StateUnchanged); model.Apply(idle);
        model.ResetTransientPresentation();
        await client.ReconnectAsync(cancellation);
        var fresh = await NextSnapshotAsync(client, cancellation);
        Assert.True(fresh.LevelScopedSnapshots);
        Assert.False(fresh.Snapshot!.GeometryUnchanged); Assert.False(fresh.Snapshot.StateUnchanged);
        Assert.NotEmpty(fresh.Snapshot.PlayerRoster); model.Apply(fresh);
        Assert.Equal(full.Snapshot.ParticipantCount, model.Snapshot!.ParticipantCount);
    }
}
