using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Timed checkpoints: the world advances in memory and acknowledgements never wait for a write. */
public sealed partial class ClockGatedCohortSnapshotPollTests
{
    private static readonly TimeSpan CheckpointInterval = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task PollInsideTheCheckpointIntervalShowsTheLiveTickWithoutWriting()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new MutableClock(FixedTick);
        var time = new ManualClockTimeProvider();
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store, checkpointInterval: CheckpointInterval, checkpointTime: time);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);
            var before = store.Load()!;

            clock.Set(FixedTick + 5);
            var live = await PollSnapshotAsync(stream, timeout.Token);
            Assert.Equal(FixedTick + 5, live.Tick);
            Assert.Equal(before.Generation, store.Load()!.Generation);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CheckpointIsWrittenOnceTheIntervalElapses()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new MutableClock(FixedTick);
        var time = new ManualClockTimeProvider();
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store, checkpointInterval: CheckpointInterval, checkpointTime: time);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);
            var before = store.Load()!;

            clock.Set(FixedTick + 3);
            time.AdvanceMilliseconds((long)CheckpointInterval.TotalMilliseconds);
            clock.Set(FixedTick + 4);
            await WaitUntilAsync(() => store.Load()!.Generation > before.Generation, timeout.Token);
            Assert.True(store.Load()!.Tick >= FixedTick + 3);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task AcknowledgementDoesNotWaitForACheckpointAndTheLastDisconnectWritesIt()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new MutableClock(FixedTick);
        var time = new ManualClockTimeProvider();
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store, checkpointInterval: CheckpointInterval, checkpointTime: time);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);
            var before = store.Load()!;

            await SendCommandAtTickAsync(stream, FixedTick + 2, timeout.Token);
            await WaitUntilAsync(() => server.PendingCommandCount == 1, timeout.Token);
            clock.Set(FixedTick + 2);
            var acknowledgementEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            while (acknowledgementEnvelope.PayloadCase != Envelope.PayloadOneofCase.CommandAck)
                acknowledgementEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            var acknowledgement = Assert.Single(acknowledgementEnvelope.CommandAck.Results);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            Assert.Equal(FixedTick + 2, acknowledgement.AppliedTick);
            Assert.Equal(before.Generation, store.Load()!.Generation);
            Assert.Empty(Assert.Single(store.Load()!.Participants).CommandReceipts);

            await stream.DisposeAsync();
            client.Dispose();
            await WaitUntilAsync(
                () => Assert.Single(store.Load()!.Participants).CommandReceipts.Count == 1,
                timeout.Token);
            var receipt = Assert.Single(Assert.Single(store.Load()!.Participants).CommandReceipts);
            Assert.Equal(acknowledgement.ClientSequence, receipt.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, receipt.Status);
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
            new FixedClock(FixedTick)) { CohortCheckpointInterval = TimeSpan.Zero });

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
