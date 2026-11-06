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

    [Fact]
    public async Task ExactRetryDuringOriginalSnapshotWriteReusesOnlyTheInflightResponse()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter {
            CommitBoundarySnapshotsEnabled = true,
            EmitNextEvent = true,
            VarySnapshotStateHash = true,
        };
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var timingRecorder = new HostCommandTimingRecorder();
        var responseReady = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshotWriteBlocked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSnapshotWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedSnapshotClaimed = 0;
        var server = CreateServer(
            adapter,
            clock,
            store,
            commandTimingRecorder: timingRecorder,
            commitBoundaryResponseReadyObserverForTests: (_, sequence) => responseReady.TrySetResult(sequence),
            clockGatedTransportDecorator: stream => new SignalStream(
                stream,
                () => false,
                () => { },
                onWriteBlocked: () => snapshotWriteBlocked.TrySetResult(true),
                releaseWrite: releaseSnapshotWrite.Task,
                shouldBlockEnvelope: envelope => envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot
                    && Interlocked.CompareExchange(ref blockedSnapshotClaimed, 1, 0) == 0));
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            adapter.EmitNextEvent = true;
            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            Assert.Equal(1UL, await responseReady.Task.WaitAsync(timeout.Token));
            clock.Set(FixedTick + 1);
            await snapshotWriteBlocked.Task.WaitAsync(timeout.Token);

            var originalAckEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, originalAckEnvelope.PayloadCase);
            var originalAck = Assert.Single(originalAckEnvelope.CommandAck.Results);
            Assert.Equal(CommandStatus.Accepted, originalAck.Status);
            Assert.Equal(FixedTick + 1, originalAck.AppliedTick);
            var originalEventEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.EventBatch, originalEventEnvelope.PayloadCase);
            Assert.Single(originalEventEnvelope.EventBatch.Events);

            // Settle the one periodic publication for N+1 before measuring the
            // retry. With the physical clock held at N+1 it cannot race the reuse.
            await WaitUntilAsync(
                () => store.Load()?.Generation >= before.Generation + 2,
                timeout.Token);
            var generationAtLink = store.Load()!.Generation;
            var capturesAtLink = adapter.CaptureCount;
            var drainsAtLink = adapter.EventDrainCount;
            var snapshotsAtLink = adapter.SnapshotCount;
            var advancesAtLink = adapter.AdvanceCount;
            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            await WaitForCommandAdmissionRecordAsync(
                timingRecorder,
                clientSequence: 1,
                timeout.Token);

            // The response writer is still held at the original Snapshot frame;
            // the reader has now registered the exact retry against that work.
            Assert.Equal(generationAtLink, store.Load()!.Generation);
            Assert.Equal(capturesAtLink, adapter.CaptureCount);
            Assert.Equal(drainsAtLink, adapter.EventDrainCount);
            Assert.Equal(snapshotsAtLink, adapter.SnapshotCount);
            Assert.Equal(advancesAtLink, adapter.AdvanceCount);

            releaseSnapshotWrite.TrySetResult(true);
            var originalSnapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, originalSnapshotEnvelope.PayloadCase);
            var originalSnapshot = originalSnapshotEnvelope.Snapshot;

            var duplicateAckEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, duplicateAckEnvelope.PayloadCase);
            var duplicateAck = Assert.Single(duplicateAckEnvelope.CommandAck.Results);
            Assert.Equal(CommandStatus.Duplicate, duplicateAck.Status);
            Assert.Equal(originalAck.ServerReceiptSequence, duplicateAck.ServerReceiptSequence);
            Assert.Equal(originalAck.AppliedTick, duplicateAck.AppliedTick);
            var duplicateSnapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, duplicateSnapshotEnvelope.PayloadCase);
            Assert.Equal(originalSnapshot.ToByteArray(), duplicateSnapshotEnvelope.Snapshot.ToByteArray());

            Assert.Equal(generationAtLink, store.Load()!.Generation);
            Assert.Equal(capturesAtLink, adapter.CaptureCount);
            Assert.Equal(drainsAtLink, adapter.EventDrainCount);
            Assert.Equal(snapshotsAtLink, adapter.SnapshotCount);
            Assert.Equal(advancesAtLink, adapter.AdvanceCount);

            // The completed response is no longer an idle cache entry. A later
            // retry is still a Duplicate receipt, but follows the normal fresh poll.
            var capturesBeforeIdleRetry = adapter.CaptureCount;
            var drainsBeforeIdleRetry = adapter.EventDrainCount;
            var snapshotsBeforeIdleRetry = adapter.SnapshotCount;
            var advancesBeforeIdleRetry = adapter.AdvanceCount;
            var generationBeforeIdleRetry = store.Load()!.Generation;
            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            var idleRetryAckEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, idleRetryAckEnvelope.PayloadCase);
            var idleRetryAck = Assert.Single(idleRetryAckEnvelope.CommandAck.Results);
            Assert.Equal(CommandStatus.Duplicate, idleRetryAck.Status);
            Assert.Equal(originalAck.ServerReceiptSequence, idleRetryAck.ServerReceiptSequence);
            var idleRetrySnapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, idleRetrySnapshotEnvelope.PayloadCase);
            Assert.NotEqual(originalSnapshot.StateSha256, idleRetrySnapshotEnvelope.Snapshot.StateSha256);
            Assert.Equal(generationBeforeIdleRetry + 1, store.Load()!.Generation);
            Assert.Equal(capturesBeforeIdleRetry + 1, adapter.CaptureCount);
            Assert.Equal(drainsBeforeIdleRetry + 1, adapter.EventDrainCount);
            Assert.Equal(snapshotsBeforeIdleRetry + 1, adapter.SnapshotCount);
            Assert.Equal(advancesBeforeIdleRetry + 1, adapter.AdvanceCount);
        } finally {
            releaseSnapshotWrite.TrySetResult(true);
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ChangedRetryBytesDoNotLinkToInflightSnapshot()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter {
            CommitBoundarySnapshotsEnabled = true,
            VarySnapshotStateHash = true,
        };
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var timingRecorder = new HostCommandTimingRecorder();
        var responseReady = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshotWriteBlocked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSnapshotWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedSnapshotClaimed = 0;
        var server = CreateServer(
            adapter,
            clock,
            store,
            commandTimingRecorder: timingRecorder,
            commitBoundaryResponseReadyObserverForTests: (_, sequence) => responseReady.TrySetResult(sequence),
            clockGatedTransportDecorator: stream => new SignalStream(
                stream,
                () => false,
                () => { },
                onWriteBlocked: () => snapshotWriteBlocked.TrySetResult(true),
                releaseWrite: releaseSnapshotWrite.Task,
                shouldBlockEnvelope: envelope => envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot
                    && Interlocked.CompareExchange(ref blockedSnapshotClaimed, 1, 0) == 0));
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            Assert.Equal(1UL, await responseReady.Task.WaitAsync(timeout.Token));
            clock.Set(FixedTick + 1);
            await snapshotWriteBlocked.Task.WaitAsync(timeout.Token);
            var originalAckEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, originalAckEnvelope.PayloadCase);
            var originalAck = Assert.Single(originalAckEnvelope.CommandAck.Results);
            Assert.Equal(CommandStatus.Accepted, originalAck.Status);
            await WaitUntilAsync(
                () => store.Load()?.Generation >= before.Generation + 2,
                timeout.Token);

            var snapshotCountBeforeRetry = adapter.SnapshotCount;
            var eventDrainCountBeforeRetry = adapter.EventDrainCount;
            var captureCountBeforeRetry = adapter.CaptureCount;
            var generationBeforeRetry = store.Load()!.Generation;
            var advanceCountBeforeRetry = adapter.AdvanceCount;
            await SendCommandBatchAsync(
                stream,
                [CreateCommand(1, FixedTick + 1, targetEntityId: 2)],
                timeout.Token);
            await WaitForCommandAdmissionRecordAsync(
                timingRecorder,
                clientSequence: 1,
                timeout.Token);

            releaseSnapshotWrite.TrySetResult(true);
            var originalSnapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, originalSnapshotEnvelope.PayloadCase);
            var duplicateAckEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, duplicateAckEnvelope.PayloadCase);
            var duplicateAck = Assert.Single(duplicateAckEnvelope.CommandAck.Results);
            Assert.Equal(CommandStatus.Duplicate, duplicateAck.Status);
            Assert.Equal(originalAck.ServerReceiptSequence, duplicateAck.ServerReceiptSequence);
            var fallbackSnapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, fallbackSnapshotEnvelope.PayloadCase);

            Assert.NotEqual(originalSnapshotEnvelope.Snapshot.StateSha256, fallbackSnapshotEnvelope.Snapshot.StateSha256);
            Assert.Equal(generationBeforeRetry + 1, store.Load()!.Generation);
            Assert.Equal(captureCountBeforeRetry + 1, adapter.CaptureCount);
            Assert.Equal(snapshotCountBeforeRetry + 1, adapter.SnapshotCount);
            Assert.Equal(eventDrainCountBeforeRetry + 1, adapter.EventDrainCount);
            Assert.Equal(advanceCountBeforeRetry + 1, adapter.AdvanceCount);
        } finally {
            releaseSnapshotWrite.TrySetResult(true);
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SameTickInterveningSnapshotInvalidatesLinkedRetryAndUsesFreshFallback()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter {
            CommitBoundarySnapshotsEnabled = true,
            VarySnapshotStateHash = true,
        };
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var timingRecorder = new HostCommandTimingRecorder();
        var responseReady = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshotWriteBlocked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSnapshotWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedSnapshotClaimed = 0;
        var server = CreateServer(
            adapter,
            clock,
            store,
            commandTimingRecorder: timingRecorder,
            commitBoundaryResponseReadyObserverForTests: (_, sequence) => responseReady.TrySetResult(sequence),
            clockGatedTransportDecorator: stream => new SignalStream(
                stream,
                () => false,
                () => { },
                onWriteBlocked: () => snapshotWriteBlocked.TrySetResult(true),
                releaseWrite: releaseSnapshotWrite.Task,
                shouldBlockEnvelope: envelope => envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot
                    && Interlocked.CompareExchange(ref blockedSnapshotClaimed, 1, 0) == 0));
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            Assert.Equal(1UL, await responseReady.Task.WaitAsync(timeout.Token));
            clock.Set(FixedTick + 1);
            await snapshotWriteBlocked.Task.WaitAsync(timeout.Token);
            var originalAckEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, originalAckEnvelope.PayloadCase);
            var originalAck = Assert.Single(originalAckEnvelope.CommandAck.Results);
            Assert.Equal(CommandStatus.Accepted, originalAck.Status);
            await WaitUntilAsync(
                () => store.Load()?.Generation >= before.Generation + 2,
                timeout.Token);

            var snapshotsBeforeRetries = adapter.SnapshotCount;
            var eventDrainsBeforeRetries = adapter.EventDrainCount;
            var capturesBeforeRetries = adapter.CaptureCount;
            var generationBeforeRetries = store.Load()!.Generation;
            var advancesBeforeRetries = adapter.AdvanceCount;
            await SendSnapshotRequestAsync(stream, timingRequestId: 73, timeout.Token);
            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            await WaitForCommandAdmissionRecordAsync(
                timingRecorder,
                clientSequence: 1,
                timeout.Token);

            releaseSnapshotWrite.TrySetResult(true);
            var originalSnapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, originalSnapshotEnvelope.PayloadCase);
            var interveningSnapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, interveningSnapshotEnvelope.PayloadCase);
            Assert.Equal(originalSnapshotEnvelope.Snapshot.Tick, interveningSnapshotEnvelope.Snapshot.Tick);
            Assert.NotEqual(originalSnapshotEnvelope.Snapshot.StateSha256, interveningSnapshotEnvelope.Snapshot.StateSha256);
            var timingReply = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.TimingReply, timingReply.PayloadCase);
            Assert.Equal(73UL, timingReply.TimingReply.RequestId);

            var duplicateAckEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, duplicateAckEnvelope.PayloadCase);
            var duplicateAck = Assert.Single(duplicateAckEnvelope.CommandAck.Results);
            Assert.Equal(CommandStatus.Duplicate, duplicateAck.Status);
            Assert.Equal(originalAck.ServerReceiptSequence, duplicateAck.ServerReceiptSequence);
            var fallbackSnapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, fallbackSnapshotEnvelope.PayloadCase);
            Assert.Equal(originalSnapshotEnvelope.Snapshot.Tick, fallbackSnapshotEnvelope.Snapshot.Tick);
            Assert.NotEqual(interveningSnapshotEnvelope.Snapshot.StateSha256, fallbackSnapshotEnvelope.Snapshot.StateSha256);
            Assert.Equal(snapshotsBeforeRetries + 2, adapter.SnapshotCount);
            Assert.Equal(eventDrainsBeforeRetries + 2, adapter.EventDrainCount);
            Assert.Equal(capturesBeforeRetries + 2, adapter.CaptureCount);
            Assert.Equal(generationBeforeRetries + 2, store.Load()!.Generation);
            Assert.Equal(advancesBeforeRetries + 2, adapter.AdvanceCount);
        } finally {
            releaseSnapshotWrite.TrySetResult(true);
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ReadFailureCancelsLinkedRetryWriteAndConnectionCleanupCompletes()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter {
            CommitBoundarySnapshotsEnabled = true,
            EmitNextEvent = true,
        };
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var timingRecorder = new HostCommandTimingRecorder();
        var responseReady = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshotWriteBlocked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshotWriteCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var injectedReadFailure = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var injectReadFailure = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var injectedReadFailureException = new IOException("Injected pending participant read failure.");
        var decoratedStreamDisposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var neverReleaseSnapshotWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedSnapshotClaimed = 0;
        var outputEnvelopes = new List<Envelope.PayloadOneofCase>();
        var outputSynchronization = new object();
        TcpClient? client = null;
        var server = CreateServer(
            adapter,
            clock,
            store,
            commandTimingRecorder: timingRecorder,
            commitBoundaryResponseReadyObserverForTests: (_, sequence) => responseReady.TrySetResult(sequence),
            clockGatedTransportDecorator: stream => new SignalStream(
                stream,
                () => false,
                () => { },
                onWriteBlocked: () => snapshotWriteBlocked.TrySetResult(true),
                onWriteCanceled: () => snapshotWriteCanceled.TrySetResult(true),
                releaseWrite: neverReleaseSnapshotWrite.Task,
                injectedReadFailureSignal: injectReadFailure.Task,
                injectedReadFailureException: injectedReadFailureException,
                onInjectedReadFailure: () => injectedReadFailure.TrySetResult(true),
                onDisposed: () => decoratedStreamDisposed.TrySetResult(true),
                onEnvelopeWriteStarted: envelope => {
                    lock (outputSynchronization)
                        outputEnvelopes.Add(envelope.PayloadCase);
                },
                shouldBlockEnvelope: envelope => envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot
                    && Interlocked.CompareExchange(ref blockedSnapshotClaimed, 1, 0) == 0));
        try {
            server.Start();
            using var timeout = CreateTimeout();
            client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            adapter.EmitNextEvent = true;
            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            Assert.Equal(1UL, await responseReady.Task.WaitAsync(timeout.Token));
            clock.Set(FixedTick + 1);
            await snapshotWriteBlocked.Task.WaitAsync(timeout.Token);
            var originalAckEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, originalAckEnvelope.PayloadCase);
            var originalEventEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.EventBatch, originalEventEnvelope.PayloadCase);
            await WaitUntilAsync(
                () => store.Load()?.Generation >= before.Generation + 2,
                timeout.Token);
            var generationAtLink = store.Load()!.Generation;
            var capturesAtLink = adapter.CaptureCount;
            var drainsAtLink = adapter.EventDrainCount;
            var snapshotCountAtLink = adapter.SnapshotCount;
            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            await WaitForCommandAdmissionRecordAsync(
                timingRecorder,
                clientSequence: 1,
                timeout.Token);

            // A deterministic failed read cancels the blocked snapshot payload;
            // the original and linked duplicate work must both release their leases.
            injectReadFailure.TrySetResult(true);
            await injectedReadFailure.Task.WaitAsync(timeout.Token);
            await snapshotWriteCanceled.Task.WaitAsync(timeout.Token);
            await decoratedStreamDisposed.Task.WaitAsync(timeout.Token);
            await WaitUntilAsync(
                () => ReferenceEquals(server.LastClientFailure, injectedReadFailureException),
                timeout.Token);

            Envelope.PayloadOneofCase[] writes;
            lock (outputSynchronization)
                writes = outputEnvelopes.ToArray();
            Assert.Equal(
                new[] {
                    Envelope.PayloadOneofCase.CommandAck,
                    Envelope.PayloadOneofCase.EventBatch,
                    Envelope.PayloadOneofCase.Snapshot,
                },
                writes);
            Assert.Equal(snapshotCountAtLink, adapter.SnapshotCount);
            Assert.Equal(capturesAtLink, adapter.CaptureCount);
            Assert.Equal(drainsAtLink, adapter.EventDrainCount);
            Assert.Equal(generationAtLink, store.Load()!.Generation);
            var durableReceipt = Assert.Single(Assert.Single(store.Load()!.Participants).CommandReceipts);
            Assert.Equal(1UL, durableReceipt.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, durableReceipt.Status);
        } finally {
            neverReleaseSnapshotWrite.TrySetResult(true);
            client?.Dispose();
            await DisposeServerAsync(server, expectedFailure: server.LastClientFailure);
            DeleteRoot(root);
        }
    }
}
