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
    public async Task DueCheckpointWritesOffTheLockAndSkipsWhileOneIsRunning()
    {
        var root = NewRoot();
        var clock = new MutableClock(FixedTick);
        var time = new ManualClockTimeProvider();
        var store = new AuthoritativeCohortStore(root);
        var performance = new HostPerformanceRecorder();
        performance.Start();
        var server = CreateServer(new SnapshotPollAdapter(), clock, store,
            checkpointInterval: CheckpointInterval, checkpointTime: time, performanceRecorder: performance);
        using var write = new ParkedCheckpointWrite();
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);
            var before = store.Load()!;
            store.PublicationObserver = write.Observe;

            clock.Set(FixedTick + 3);
            time.AdvanceMilliseconds((long)CheckpointInterval.TotalMilliseconds);
            clock.Set(FixedTick + 4);
            await WaitUntilAsync(() => write.Parked, timeout.Token);

            // The world moves on and a further due checkpoint finds the writer busy.
            time.AdvanceMilliseconds((long)CheckpointInterval.TotalMilliseconds);
            clock.Set(FixedTick + 5);
            Assert.Equal(FixedTick + 5, (await PollSnapshotAsync(stream, timeout.Token)).Tick);
            clock.Set(FixedTick + 6);
            Assert.Equal(FixedTick + 6, (await PollSnapshotAsync(stream, timeout.Token)).Tick);
            Assert.Equal(1, write.Writes);

            // Publish the next live tick before releasing the writer: the background
            // scheduler may settle and retry immediately after that release.
            clock.Set(FixedTick + 7);
            write.Release();
            await WaitUntilAsync(() => store.Load()!.Generation > before.Generation, timeout.Token);
            Assert.True(store.Load()!.Tick >= FixedTick + 3);

            // The skipped checkpoint continues the generation chain.
            await WaitUntilAsync(() => store.Load()!.Tick >= FixedTick + 7, timeout.Token);
            Assert.Null(server.LastClientFailure);
            var metrics = performance.Stop().Metrics;
            Assert.True(metrics[nameof(HostPerformanceMetric.CohortCheckpointCaptureMilliseconds)].Samples >= 2);
            Assert.True(metrics[nameof(HostPerformanceMetric.CohortCheckpointWriteMilliseconds)].Samples >= 2);
        } finally {
            write.Release();
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedTimedWriteKeepsTheHostRunningAndRetriesNextInterval(bool afterRename)
    {
        var root = NewRoot();
        var clock = new MutableClock(FixedTick);
        var time = new ManualClockTimeProvider();
        var store = new AuthoritativeCohortStore(root);
        var failures = 0;
        var server = CreateServer(new SnapshotPollAdapter(), clock, store,
            checkpointInterval: CheckpointInterval, checkpointTime: time,
            checkpointFailureObserver: _ => Interlocked.Increment(ref failures));
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);
            var before = store.Load()!;
            var armed = 1;
            var failingPhase = afterRename
                ? AuthoritativePublicationPhase.AfterCanonicalRename
                : AuthoritativePublicationPhase.AfterFlushBeforeRename;
            store.PublicationObserver = (phase, _) => {
                if (phase == failingPhase && Interlocked.Exchange(ref armed, 0) == 1)
                    throw new IOException("Injected checkpoint write failure.");
            };

            clock.Set(FixedTick + 3);
            time.AdvanceMilliseconds((long)CheckpointInterval.TotalMilliseconds);
            clock.Set(FixedTick + 4);
            await WaitUntilAsync(() => Volatile.Read(ref failures) == 1, timeout.Token);

            // An interrupted write leaves the prior checkpoint; a late failure leaves its own.
            var published = before.Generation + (afterRename ? 1UL : 0UL);
            Assert.Equal(published, store.Load()!.Generation);
            clock.Set(FixedTick + 5);
            Assert.Equal(FixedTick + 5, (await PollSnapshotAsync(stream, timeout.Token)).Tick);
            Assert.Equal(published, store.Load()!.Generation);

            time.AdvanceMilliseconds((long)CheckpointInterval.TotalMilliseconds);
            clock.Set(FixedTick + 6);
            await WaitUntilAsync(() => store.Load()!.Generation == published + 1, timeout.Token);
            Assert.True(store.Load()!.Tick >= FixedTick + 5);
            Assert.Equal(1, Volatile.Read(ref failures));
            Assert.Null(server.LastClientFailure);
            Assert.False(server.Completion.IsCompleted);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ForcedBoundariesWaitForTheRunningWriteThenPublish()
    {
        var root = NewRoot();
        var clock = new MutableClock(FixedTick);
        var time = new ManualClockTimeProvider();
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(new SnapshotPollAdapter(), clock, store,
            checkpointInterval: CheckpointInterval, checkpointTime: time);
        using var write = new ParkedCheckpointWrite();
        try {
            server.Start();
            using var timeout = CreateTimeout();
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);
            var before = store.Load()!;
            store.PublicationObserver = write.Observe;

            clock.Set(FixedTick + 3);
            time.AdvanceMilliseconds((long)CheckpointInterval.TotalMilliseconds);
            clock.Set(FixedTick + 4);
            await WaitUntilAsync(() => write.Parked, timeout.Token);

            // Disconnect and shutdown both publish; neither may overtake the parked write.
            await stream.DisposeAsync();
            client.Dispose();
            // Shutdown can queue behind the disconnect's publication, which waits on the write.
            var disposal = Task.Run(() => server.DisposeAsync().AsTask(), timeout.Token);
            await Task.Delay(100, timeout.Token);
            Assert.False(disposal.IsCompleted);
            write.Release();
            await disposal;

            using var reopened = new AuthoritativeCohortStore(root);
            var final = reopened.Load()!;
            Assert.True(final.Generation >= before.Generation + 2);
            Assert.True(final.Tick >= FixedTick + 4);
        } finally {
            write.Release();
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

    /** Holds each write just before its rename. Never call store.Load while parked. */
    private sealed class ParkedCheckpointWrite : IDisposable
    {
        private readonly ManualResetEventSlim parked = new();
        private readonly ManualResetEventSlim released = new();
        private int writes;

        public bool Parked => parked.IsSet;
        public int Writes => Volatile.Read(ref writes);

        public void Observe(AuthoritativePublicationPhase phase, string path)
        {
            if (phase != AuthoritativePublicationPhase.AfterFlushBeforeRename)
                return;
            Interlocked.Increment(ref writes);
            parked.Set();
            released.Wait(TimeSpan.FromSeconds(30));
        }

        public void Release() => released.Set();

        public void Dispose()
        {
            parked.Dispose();
            released.Dispose();
        }
    }

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
