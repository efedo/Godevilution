using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using Google.Protobuf;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;
using Xunit.Sdk;

namespace Devilution.Server.Tests;

/** Verifies that one clock-gated cohort snapshot poll has one durable boundary. */
public sealed partial class ClockGatedCohortSnapshotPollTests
{
    private const ulong FixedTick = 111;
    private static readonly string ContentHash = new('a', 64);
    private static readonly string RulesetHash = new('b', 64);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CohortSnapshotPollPublishesOnceAfterDrainingEventsAndMaterializingSnapshot(
        bool emitEvent)
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter { EmitNextEvent = emitEvent };
        var clock = new FixedClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            var capturesBefore = adapter.CaptureCount;
            var snapshotsBefore = adapter.SnapshotCount;
            await SendSnapshotRequestAsync(stream, timingRequestId: 17, timeout.Token);

            Envelope? eventEnvelope = null;
            if (emitEvent) {
                eventEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
                Assert.Equal(Envelope.PayloadOneofCase.EventBatch, eventEnvelope.PayloadCase);
            }
            var snapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshotEnvelope.PayloadCase);
            var timingEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.TimingReply, timingEnvelope.PayloadCase);
            Assert.Equal(17UL, timingEnvelope.TimingReply.RequestId);
            Assert.False(timingEnvelope.TimingReply.HasServerSchedulingClockTick);

            var snapshot = snapshotEnvelope.Snapshot;
            var after = store.Load()!;
            Assert.Equal(FixedTick, snapshot.Tick);
            Assert.Equal(before.Generation + 1, after.Generation);
            Assert.Equal(capturesBefore + 1, adapter.CaptureCount);
            Assert.Equal(snapshot.Tick, after.Tick);
            Assert.Equal(adapter.SerializeCurrentState(), after.SerializedModuleState);
            Assert.Equal(adapter.LastCapturedSerializedState, after.SerializedModuleState);
            Assert.Equal(1, adapter.EventDrainCount);
            Assert.Equal(snapshotsBefore + 1, adapter.SnapshotCount);
            if (emitEvent) {
                Assert.Equal(snapshot.Tick, eventEnvelope!.EventBatch.Tick);
                Assert.Single(eventEnvelope.EventBatch.Events);
            }
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CohortSnapshotPollPublishesOnceEvenWithoutProjectionProviders()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new FixedClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store, includeProjectionProviders: false);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: false, timeout.Token);

            var before = store.Load()!;
            var capturesBefore = adapter.CaptureCount;
            await SendSnapshotRequestAsync(stream, timingRequestId: 23, timeout.Token);
            var response = await ReadRequiredEnvelopeAsync(stream, timeout.Token);

            Assert.Equal(Envelope.PayloadOneofCase.TimingReply, response.PayloadCase);
            Assert.Equal(23UL, response.TimingReply.RequestId);
            Assert.False(response.TimingReply.HasServerSchedulingClockTick);
            var after = store.Load()!;
            Assert.Equal(before.Generation + 1, after.Generation);
            Assert.Equal(capturesBefore + 1, adapter.CaptureCount);
            Assert.Equal(FixedTick, after.Tick);
            Assert.Equal(adapter.SerializeCurrentState(), after.SerializedModuleState);
            Assert.Equal(0, adapter.EventDrainCount);
            Assert.Equal(0, adapter.SnapshotCount);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ClockOnlyProbeReturnsSchedulingClockSampleWithoutAdvancingOrPublishing()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new FixedClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store);
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            var stateBefore = adapter.SerializeCurrentState();
            var capturesBefore = adapter.CaptureCount;
            var eventsBefore = adapter.EventDrainCount;
            var snapshotsBefore = adapter.SnapshotCount;
            await SendClockProbeAsync(stream, requestId: 37, timeout.Token);
            var response = await ReadRequiredEnvelopeAsync(stream, timeout.Token);

            Assert.Equal(Envelope.PayloadOneofCase.TimingReply, response.PayloadCase);
            Assert.Equal(37UL, response.TimingReply.RequestId);
            Assert.True(response.TimingReply.HasServerSchedulingClockTick);
            Assert.Equal(FixedTick, response.TimingReply.ServerSchedulingClockTick);

            var after = store.Load()!;
            Assert.Equal(before.Generation, after.Generation);
            Assert.Equal(before.Tick, after.Tick);
            Assert.Equal(before.SerializedModuleState, after.SerializedModuleState);
            Assert.Equal(stateBefore, adapter.SerializeCurrentState());
            Assert.Equal(capturesBefore, adapter.CaptureCount);
            Assert.Equal(eventsBefore, adapter.EventDrainCount);
            Assert.Equal(snapshotsBefore, adapter.SnapshotCount);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CohortCommandResponsePublishesOneCapturedBoundaryAfterAckCheckpoint(
        bool commitBoundarySnapshotsEnabled)
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter {
            EmitNextEvent = true,
            CommitBoundarySnapshotsEnabled = commitBoundarySnapshotsEnabled,
        };
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var responseReady = new TaskCompletionSource<(string SessionId, ulong ClientSequence)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var server = CreateServer(
            adapter,
            clock,
            store,
            commitBoundaryResponseReadyObserverForTests: (sessionId, clientSequence) =>
                responseReady.TrySetResult((sessionId, clientSequence)));
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            var capturesBefore = adapter.CaptureCount;
            var eventsBefore = adapter.EventDrainCount;
            var snapshotsBefore = adapter.SnapshotCount;
            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            if (commitBoundarySnapshotsEnabled) {
                var ready = await responseReady.Task.WaitAsync(timeout.Token);
                Assert.Equal(1UL, ready.ClientSequence);
            } else {
                await WaitUntilAsync(() => server.PendingCommandCount == 1, timeout.Token);
            }
            clock.Set(FixedTick + 1);

            var acknowledgementEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, acknowledgementEnvelope.PayloadCase);
            var acknowledgement = Assert.Single(acknowledgementEnvelope.CommandAck.Results);
            Assert.Equal(1UL, acknowledgement.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            Assert.Equal(FixedTick + 1, acknowledgement.AppliedTick);

            var eventEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.EventBatch, eventEnvelope.PayloadCase);
            var snapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshotEnvelope.PayloadCase);

            // The simulation loop persists the same clock advance after the
            // scheduler drains the command. Wait for both independent writes
            // before asserting the command-boundary versus legacy deltas.
            var expectedCommandBoundaryPublications = commitBoundarySnapshotsEnabled ? 1UL : 2UL;
            var expectedTotalPublications = expectedCommandBoundaryPublications + 1;
            await WaitUntilAsync(
                () => store.Load()?.Generation >= before.Generation + expectedTotalPublications
                    && adapter.CaptureCount >= capturesBefore + (int)expectedTotalPublications,
                timeout.Token);

            var after = store.Load()!;
            Assert.Equal(before.Generation + expectedTotalPublications, after.Generation);
            Assert.Equal(capturesBefore + (int)expectedTotalPublications, adapter.CaptureCount);
            Assert.Equal(eventsBefore + 1, adapter.EventDrainCount);
            Assert.Equal(snapshotsBefore + 1, adapter.SnapshotCount);
            Assert.Equal(FixedTick + 1, eventEnvelope.EventBatch.Tick);
            Assert.Single(eventEnvelope.EventBatch.Events);
            Assert.Equal(FixedTick + 1, snapshotEnvelope.Snapshot.Tick);
            Assert.Equal(snapshotEnvelope.Snapshot.Tick, after.Tick);
            Assert.Equal(adapter.SerializeCurrentState(), after.SerializedModuleState);
            Assert.Equal(adapter.LastCapturedSerializedState, after.SerializedModuleState);
            var durableReceipt = Assert.Single(Assert.Single(after.Participants).CommandReceipts);
            Assert.Equal(1UL, durableReceipt.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, durableReceipt.Status);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task EarlierSnapshotRequestKeepsItsCapturedBoundaryBeforeACommitBoundaryCommand()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter { CommitBoundarySnapshotsEnabled = true };
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var blockNextWrite = 0;
        var writeClaimed = 0;
        var blockedWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = CreateServer(
            adapter,
            clock,
            store,
            clockGatedTransportDecorator: stream => new SignalStream(
                stream,
                () => false,
                () => { },
                shouldBlockWrite: () => Volatile.Read(ref blockNextWrite) != 0
                    && Interlocked.CompareExchange(ref writeClaimed, 1, 0) == 0,
                onWriteBlocked: () => blockedWrite.TrySetResult(true),
                releaseWrite: releaseWrite.Task));
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            var capturesBefore = adapter.CaptureCount;
            Volatile.Write(ref blockNextWrite, 1);
            await SendSnapshotRequestAsync(stream, timingRequestId: 51, timeout.Token);
            await blockedWrite.Task.WaitAsync(timeout.Token);

            // The earlier poll has already captured and durably published tick N;
            // its socket response is held while a newer command commits at N+9.
            await WaitUntilAsync(
                () => adapter.CaptureCount >= capturesBefore + 1
                    && store.Load()?.Generation >= before.Generation + 1,
                timeout.Token);
            adapter.EmitNextEvent = true;
            clock.Set(FixedTick + 9);
            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 9, timeout.Token);
            await WaitUntilAsync(
                () => adapter.CaptureCount >= capturesBefore + 3
                    && store.Load()?.Generation >= before.Generation + 3
                    && store.Load()?.Participants.SingleOrDefault()?.CommandReceipts
                        .Any(receipt => receipt.ClientSequence == 1
                            && receipt.Status == CommandStatus.Accepted
                            && receipt.AppliedTick == FixedTick + 9) == true,
                timeout.Token);
            var commandCommitted = store.Load()!;
            Assert.Equal(before.Generation + 3, commandCommitted.Generation);
            Assert.Equal(FixedTick + 9, commandCommitted.Tick);

            releaseWrite.TrySetResult(true);
            var earlierSnapshot = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, earlierSnapshot.PayloadCase);
            Assert.Equal(FixedTick, earlierSnapshot.Snapshot.Tick);
            var earlierTiming = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.TimingReply, earlierTiming.PayloadCase);
            Assert.Equal(51UL, earlierTiming.TimingReply.RequestId);

            var acknowledgement = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, acknowledgement.PayloadCase);
            var result = Assert.Single(acknowledgement.CommandAck.Results);
            Assert.Equal(CommandStatus.Accepted, result.Status);
            Assert.Equal(FixedTick + 9, result.AppliedTick);
            var eventEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.EventBatch, eventEnvelope.PayloadCase);
            Assert.Equal(FixedTick + 9, eventEnvelope.EventBatch.Tick);
            var commandSnapshot = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, commandSnapshot.PayloadCase);
            Assert.Equal(FixedTick + 9, commandSnapshot.Snapshot.Tick);

            var after = store.Load()!;
            Assert.Equal(before.Generation + 4, after.Generation);
            Assert.Equal(capturesBefore + 4, adapter.CaptureCount);
            Assert.Equal(after.Tick, commandSnapshot.Snapshot.Tick);
            Assert.Equal(adapter.LastCapturedSerializedState, after.SerializedModuleState);
            Assert.Equal(CommandStatus.Accepted, Assert.Single(Assert.Single(after.Participants).CommandReceipts).Status);
        } finally {
            releaseWrite.TrySetResult(true);
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }
}
