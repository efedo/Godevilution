using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveSnapshotReferenceTests : LiveAuthoritativeTestBase
{
    [Theory]
    [InlineData(null, CommandSchedulingMode.Immediate, "legacy")] [InlineData(false, CommandSchedulingMode.Immediate, "legacy")]
    [InlineData(true, CommandSchedulingMode.Immediate, "legacy")] [InlineData(null, CommandSchedulingMode.ClockGated, "legacy")]
    [InlineData(false, CommandSchedulingMode.ClockGated, "legacy")] [InlineData(true, CommandSchedulingMode.ClockGated, "legacy")]
    [InlineData(true, CommandSchedulingMode.Immediate, "cohort")] [InlineData(true, CommandSchedulingMode.ClockGated, "cohort")]
    public async Task RealHostNegotiatesReferencesAndLegacyDeliveryKeepsCompleteSnapshots(bool? references, CommandSchedulingMode scheduling, string persistence)
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var server = await StartedServer.StartAsync(cancellation, startingLevelOverride: 0,
            controlledClock: true, commandSchedulingMode: scheduling, persistenceMode: persistence);
        var options = CreateOptions(server, "snapshot-reference-test", requestedClassId: 0) with {
            SnapshotPollInterval = TimeSpan.Zero, UseLevelScopedIdleReferences = true,
        };
        if (references is { } requested) options = options with { UseSnapshotReferences = requested };
        var expectedReferences = references ?? false;
        await using var client = new AuthoritativeClient(options) { AllocationMetrics = new ClientTransportAllocationMetrics() };
        await client.ConnectAsync(cancellation);
        var first = await NextSnapshotAsync(client, cancellation);
        Assert.Equal(expectedReferences, first.LevelScopedIdleReferences);
        var model = new AuthoritativeClientModel();
        model.Apply(first);
        await client.PollAsync(cancellation);
        var second = await NextSnapshotAsync(client, cancellation);
        Assert.Equal(expectedReferences, second.LevelScopedIdleReferences);
        Assert.Equal(expectedReferences, second.Snapshot!.GeometryUnchanged);
        Assert.Equal(expectedReferences, second.Snapshot.StateUnchanged);
        if (expectedReferences) {
            Assert.Empty(second.Snapshot.LevelGeometry);
            Assert.Empty(second.Snapshot.Players);
        } else {
            Assert.Equal(first.Snapshot!.LevelGeometry, second.Snapshot.LevelGeometry);
            Assert.NotEmpty(second.Snapshot.Players);
        }
        model.Apply(second);
        Assert.NotEmpty(model.Snapshot!.Players);
        Assert.Equal(2UL, model.AcceptedSnapshotRevision);
        var metrics = client.AllocationMetrics!.Capture();
        Assert.True(metrics.SnapshotCount >= 2);
        Assert.True(metrics.SnapshotWireBytes > 0);
        Assert.True(metrics.DecodeAllocatedBytes > 0);
        if (expectedReferences) {
            // Leave an old connection-local reference queued; reconnect must not expose it before the new baseline.
            var previousProcessing = client.AllocationMetrics.Capture().ProcessingAllocatedBytes;
            await client.PollAsync(cancellation);
            using var queuedDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            queuedDeadline.CancelAfter(TimeSpan.FromSeconds(10));
            while (client.AllocationMetrics.Capture().ProcessingAllocatedBytes <= previousProcessing)
                await Task.Delay(5, queuedDeadline.Token);
            model.ResetTransientPresentation();
            await client.ReconnectAsync(cancellation);
            var reconnected = await NextSnapshotAsync(client, cancellation);
            Assert.False(reconnected.Snapshot!.GeometryUnchanged);
            Assert.False(reconnected.Snapshot.StateUnchanged);
            model.Apply(reconnected);
            await client.PollAsync(cancellation);
            var referencedAgain = await NextSnapshotAsync(client, cancellation);
            Assert.True(referencedAgain.Snapshot!.StateUnchanged);
            model.Apply(referencedAgain);
            Assert.NotEmpty(model.Snapshot!.Players);
        }
    }

    [Fact]
    public async Task ReferenceAwareOlderPolicyRemainsNegotiable()
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var server = await StartedServer.StartAsync(cancellation, startingLevelOverride: 0,
            controlledClock: true, commandSchedulingMode: CommandSchedulingMode.ClockGated, persistenceMode: "cohort");
        var options = CreateOptions(server, "old-reference-policy", requestedClassId: 0) with {
            UseSnapshotReferences = true, UseLevelScopedIdleReferences = false, SnapshotPollInterval = TimeSpan.Zero,
        };
        await using var client = new AuthoritativeClient(options);
        await client.ConnectAsync(cancellation);
        var model = new AuthoritativeClientModel();
        var first = await NextSnapshotAsync(client, cancellation);
        Assert.False(first.LevelScopedIdleReferences);
        model.Apply(first);
        await client.PollAsync(cancellation);
        var next = await NextSnapshotAsync(client, cancellation);
        Assert.False(next.LevelScopedIdleReferences);
        Assert.True(next.Snapshot!.StateUnchanged);
        model.Apply(next);
        Assert.Equal(2UL, model.AcceptedSnapshotRevision);
    }

    private static async Task<AuthoritativeClientMessage> NextSnapshotAsync(AuthoritativeClient client, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (true) {
            var messages = client.DrainMessages();
            Assert.DoesNotContain(messages, message => message.Error is not null);
            if (messages.Any(message => message.Snapshot is not null))
                return Assert.Single(messages, message => message.Snapshot is not null);
            await Task.Delay(5, deadline.Token);
        }
    }

}
