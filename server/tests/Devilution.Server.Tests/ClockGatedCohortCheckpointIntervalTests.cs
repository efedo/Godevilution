using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** A checkpoint interval defers invisible progress only; nothing is visible before it is durable. */
public sealed partial class ClockGatedCohortSnapshotPollTests
{
    private const uint CheckpointInterval = 5;

    [Fact]
    public async Task PollInsideTheCheckpointIntervalServesTheDurableBoundaryWithoutPublishing()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store, checkpointIntervalTicks: CheckpointInterval);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);
            var before = store.Load()!;
            var capturesBefore = adapter.CaptureCount;

            // Inside the interval the periodic loop neither advances nor publishes,
            // so the poll shows exactly the durable tick.
            clock.Set(FixedTick + CheckpointInterval - 1);
            var inside = await PollSnapshotAsync(stream, timeout.Token);
            Assert.Equal(before.Tick, inside.Tick);
            Assert.Equal(before.Generation, store.Load()!.Generation);
            Assert.Equal(capturesBefore, adapter.CaptureCount);

            // Once the interval elapses the periodic loop advances and publishes together.
            clock.Set(FixedTick + CheckpointInterval);
            await WaitUntilAsync(() => store.Load()?.Tick == FixedTick + CheckpointInterval, timeout.Token);
            var published = store.Load()!;
            var boundary = await PollSnapshotAsync(stream, timeout.Token);
            Assert.Equal(published.Tick, boundary.Tick);
            Assert.Equal(published.Generation, store.Load()!.Generation);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task PollAfterTheCheckpointIntervalShowsOnlyAPublishedTick()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store, checkpointIntervalTicks: CheckpointInterval);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            // Polls never advance: a poll racing the periodic checkpoint shows the old or the new durable tick.
            var target = FixedTick + CheckpointInterval + 2;
            clock.Set(target);
            var racing = await PollSnapshotAsync(stream, timeout.Token);
            Assert.Contains(racing.Tick, new[] { FixedTick, target });

            await WaitUntilAsync(() => store.Load()?.Tick == target, timeout.Token);
            var snapshot = await PollSnapshotAsync(stream, timeout.Token);
            var after = store.Load()!;
            Assert.Equal(target, snapshot.Tick);
            Assert.Equal(snapshot.Tick, after.Tick);
            Assert.Equal(adapter.LastCapturedSerializedState, after.SerializedModuleState);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task DueCommandIsAppliedAndDurableBeforeAckInsideTheCheckpointInterval()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store, checkpointIntervalTicks: CheckpointInterval);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            await SendCommandAtTickAsync(stream, FixedTick + 2, timeout.Token);
            await WaitUntilAsync(() => server.PendingCommandCount == 1, timeout.Token);
            clock.Set(FixedTick + 2);

            var acknowledgementEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, acknowledgementEnvelope.PayloadCase);
            var acknowledgement = Assert.Single(acknowledgementEnvelope.CommandAck.Results);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            Assert.Equal(FixedTick + 2, acknowledgement.AppliedTick);
            var durable = store.Load()!;
            Assert.True(durable.Tick >= FixedTick + 2);
            var receipt = Assert.Single(Assert.Single(durable.Participants).CommandReceipts);
            Assert.Equal(acknowledgement.ClientSequence, receipt.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, receipt.Status);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    /** A client anchored on the durable snapshot tick is not late while the cohort waits at that boundary. */
    [Fact]
    public async Task CommandBehindTheClockButAfterTheDurableTickIsAccepted()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store, checkpointIntervalTicks: CheckpointInterval);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            clock.Set(FixedTick + 3);
            Assert.Equal(FixedTick, (await PollSnapshotAsync(stream, timeout.Token)).Tick);
            await SendCommandAtTickAsync(stream, FixedTick + 1, timeout.Token);

            var acknowledgementEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            while (acknowledgementEnvelope.PayloadCase != Envelope.PayloadOneofCase.CommandAck)
                acknowledgementEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            var acknowledgement = Assert.Single(acknowledgementEnvelope.CommandAck.Results);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            Assert.Equal(FixedTick + 1, acknowledgement.AppliedTick);
            Assert.True(store.Load()!.Tick >= FixedTick + 1);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public void CheckpointIntervalRejectsZero()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new AuthoritativeTcpServer(
            new AuthoritativeCommandServer(new SnapshotPollAdapter()),
            new ProtocolHandshake(new ProtocolServerIdentity("interval", "0.1.0", ContentHash, 20)),
            new FixedClock(FixedTick)) { CohortCheckpointIntervalTicks = 0 });

    private static async Task<Snapshot> PollSnapshotAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        await SendSnapshotRequestAsync(stream, timingRequestId: 0, cancellationToken);
        while (true) {
            var envelope = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
            if (envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot)
                return envelope.Snapshot;
            Assert.Equal(Envelope.PayloadOneofCase.EventBatch, envelope.PayloadCase);
        }
    }
}
