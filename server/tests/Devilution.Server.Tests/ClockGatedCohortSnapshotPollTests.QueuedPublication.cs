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
    public async Task QueuedCohortAdmissionPrecedesNextPeriodicTurnAndIsDurableBeforeAck()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var timingRecorder = new HostCommandTimingRecorder();
        var server = CreateServer(adapter, clock, store, commandTimingRecorder: timingRecorder);
        var periodicPublicationEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextPeriodicPublication = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandCheckpointRenamed = new TaskCompletionSource<(bool Durable, bool AckPending, ulong? AppliedTick)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releasePeriodicPublication = new ManualResetEventSlim();
        using var releaseCommandCheckpoint = new ManualResetEventSlim();
        var publicationNumber = 0;
        Task<Envelope?>? acknowledgementRead = null;
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            store.PublicationObserver = (phase, _) => {
                if (phase == AuthoritativePublicationPhase.BeforeStageWrite) {
                    var currentPublication = Interlocked.Increment(ref publicationNumber);
                    if (currentPublication == 1) {
                        periodicPublicationEntered.TrySetResult(true);
                        if (!releasePeriodicPublication.Wait(TimeSpan.FromSeconds(5)))
                            throw new TimeoutException("The test did not release the held periodic publication.");
                    } else if (currentPublication == 2) {
                        nextPeriodicPublication.TrySetResult(server.PendingCommandCount);
                    }
                } else if (phase == AuthoritativePublicationPhase.AfterCanonicalRename
                    && Volatile.Read(ref publicationNumber) == 3) {
                    var receipt = store.Load()?.Participants
                        .SelectMany(participant => participant.CommandReceipts)
                        .SingleOrDefault(result => result.ClientSequence == 1);
                    commandCheckpointRenamed.TrySetResult((
                        receipt?.Status == CommandStatus.Accepted,
                        acknowledgementRead is { IsCompleted: false },
                        receipt?.AppliedTick));
                    if (!releaseCommandCheckpoint.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("The test did not release the command receipt checkpoint.");
                }
            };

            clock.Set(FixedTick + 1);
            await periodicPublicationEntered.Task.WaitAsync(timeout.Token);
            await SendCommandAtTickAsync(stream, FixedTick + 3, timeout.Token);
            await WaitUntilAsync(
                () => server.AuthorityTurnGateForTests.QueuedWaiterCount == 1,
                timeout.Token);

            // The ingress is now queued behind the held tick N+1 publication.
            // Its turn must sample N+2 before the timer can publish another tick.
            clock.Set(FixedTick + 2);
            releasePeriodicPublication.Set();
            Assert.Equal(1, await nextPeriodicPublication.Task.WaitAsync(timeout.Token));
            var afterAdmissionTurn = store.Load()!;
            Assert.Equal(FixedTick + 2, afterAdmissionTurn.Tick);
            Assert.Equal(1, server.PendingCommandCount);

            var acknowledgementTask = EnvelopeCodec.ReadAsync(stream, timeout.Token).AsTask();
            acknowledgementRead = acknowledgementTask;
            clock.Set(FixedTick + 3);
            var durableCheckpoint = await commandCheckpointRenamed.Task.WaitAsync(timeout.Token);
            Assert.True(durableCheckpoint.Durable);
            Assert.True(durableCheckpoint.AckPending);
            Assert.Equal(FixedTick + 3, durableCheckpoint.AppliedTick);
            releaseCommandCheckpoint.Set();

            var acknowledgementEnvelope = await acknowledgementTask.WaitAsync(timeout.Token);
            Assert.NotNull(acknowledgementEnvelope);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, acknowledgementEnvelope.PayloadCase);
            var acknowledgement = Assert.Single(acknowledgementEnvelope.CommandAck.Results);
            Assert.Equal(1UL, acknowledgement.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            Assert.Equal(FixedTick + 3, acknowledgement.AppliedTick);
            var admissionRecord = Assert.Single(timingRecorder.Drain().Records,
                record => record.Kind == "command-admission" && record.ClientSequence == 1);
            Assert.Equal(FixedTick + 2, admissionRecord.SampledClockCurrentTick);
            Assert.Equal(FixedTick + 1, admissionRecord.SampledCommandServerCurrentTick);
            Assert.Equal(FixedTick + 2, admissionRecord.ActualAdmissionTick);

            var durableAfterAck = store.Load()!;
            var persistedReceipt = Assert.Single(Assert.Single(durableAfterAck.Participants).CommandReceipts);
            Assert.Equal(1UL, persistedReceipt.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, persistedReceipt.Status);
            Assert.Equal(FixedTick + 3, persistedReceipt.AppliedTick);

            clock.Set(FixedTick + 4);
            await WaitUntilAsync(
                () => store.Load()?.Tick == FixedTick + 4,
                timeout.Token);
            Assert.Equal(FixedTick + 4, store.Load()!.Tick);
        } finally {
            releasePeriodicPublication.Set();
            releaseCommandCheckpoint.Set();
            await DisposeServerAsync(server, server.LastClientFailure);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task QueuedOrdinarySnapshotCapturePrecedesNextPeriodicPublication()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store);
        var periodicPublicationEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextPublication = new TaskCompletionSource<(int Snapshots, int EventDrains)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releasePeriodicPublication = new ManualResetEventSlim();
        var publicationNumber = 0;
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var snapshotsBefore = adapter.SnapshotCount;
            var eventDrainsBefore = adapter.EventDrainCount;
            store.PublicationObserver = (phase, _) => {
                if (phase != AuthoritativePublicationPhase.BeforeStageWrite)
                    return;
                var currentPublication = Interlocked.Increment(ref publicationNumber);
                if (currentPublication == 1) {
                    periodicPublicationEntered.TrySetResult(true);
                    if (!releasePeriodicPublication.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("The test did not release the held periodic publication.");
                } else if (currentPublication == 2) {
                    nextPublication.TrySetResult((adapter.SnapshotCount, adapter.EventDrainCount));
                }
            };

            clock.Set(FixedTick + 1);
            await periodicPublicationEntered.Task.WaitAsync(timeout.Token);
            await SendSnapshotRequestAsync(stream, timingRequestId: 81, timeout.Token);
            await WaitUntilAsync(
                () => server.AuthorityTurnGateForTests.QueuedWaiterCount == 1,
                timeout.Token);

            // The ordinary snapshot is already ahead of the next timer turn in the FIFO.
            clock.Set(FixedTick + 2);
            releasePeriodicPublication.Set();
            var capturedAtNextPublication = await nextPublication.Task.WaitAsync(timeout.Token);
            Assert.Equal(snapshotsBefore + 1, capturedAtNextPublication.Snapshots);
            Assert.Equal(eventDrainsBefore + 1, capturedAtNextPublication.EventDrains);

            var snapshot = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot.PayloadCase);
            Assert.Equal(FixedTick + 2, snapshot.Snapshot.Tick);
            var timing = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.TimingReply, timing.PayloadCase);
            Assert.Equal(81UL, timing.TimingReply.RequestId);
        } finally {
            releasePeriodicPublication.Set();
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task PreWireAuthorityTurnIsReleasedBeforeBlockedSocketWrite()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new FixedClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var slowWriteEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowWriteCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSlowWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstWriteClaimed = 0;
        var server = CreateServer(
            adapter,
            clock,
            store,
            clockGatedTransportDecorator: stream => new SignalStream(
                stream,
                () => false,
                () => { },
                () => Interlocked.CompareExchange(ref firstWriteClaimed, 1, 0) == 0,
                () => slowWriteEntered.TrySetResult(true),
                () => slowWriteCanceled.TrySetResult(true),
                releaseSlowWrite.Task));
        Task<Envelope?>? slowReplyRead = null;
        var serverDisposed = false;
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var slowClient = new TcpClient();
            await slowClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var slowStream = slowClient.GetStream();
            await ReadHandshakeAsync(slowStream, hasSnapshot: true, timeout.Token, "slow-writer");

            using var fastClient = new TcpClient();
            await fastClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var fastStream = fastClient.GetStream();
            await ReadHandshakeAsync(fastStream, hasSnapshot: true, timeout.Token, "fast-writer");

            await SendClockProbeAsync(slowStream, requestId: 91, timeout.Token);
            slowReplyRead = EnvelopeCodec.ReadAsync(slowStream, timeout.Token).AsTask();
            await slowWriteEntered.Task.WaitAsync(timeout.Token);

            // A second lane must pass the same pre-wire barrier while the first lane is
            // suspended inside Stream.WriteAsync, proving the turn is not held over I/O.
            await SendClockProbeAsync(fastStream, requestId: 92, timeout.Token);
            var fastReply = await ReadRequiredEnvelopeAsync(fastStream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.TimingReply, fastReply.PayloadCase);
            Assert.Equal(92UL, fastReply.TimingReply.RequestId);
            Assert.Equal(FixedTick, fastReply.TimingReply.ServerSchedulingClockTick);

            // A clean client EOF intentionally preserves queued responses. Stop the host
            // to exercise cancellation of the blocked write through its linked token.
            await DisposeServerAsync(server, expectedFailure: null);
            serverDisposed = true;
            await slowWriteCanceled.Task.WaitAsync(timeout.Token);
            _ = await Record.ExceptionAsync(async () =>
                await slowReplyRead!.WaitAsync(timeout.Token));
        } finally {
            releaseSlowWrite.TrySetResult(true);
            if (!serverDisposed)
                await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData(PollFailure.InvalidEventBatch)]
    [InlineData(PollFailure.InvalidSnapshot)]
    [InlineData(PollFailure.Publication)]
    public async Task CohortCommandCaptureFailureClosesBeforeAnyResponse(PollFailure failure)
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new FixedClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store);
        Exception? injectedPublicationFailure = null;
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
            switch (failure) {
            case PollFailure.InvalidEventBatch:
                adapter.ReturnInvalidEventBatch = true;
                break;
            case PollFailure.InvalidSnapshot:
                adapter.ReturnInvalidSnapshot = true;
                break;
            case PollFailure.Publication:
                var publicationFailure = new IOException("Injected clock-gated command capture publication failure.");
                injectedPublicationFailure = publicationFailure;
                var stageWriteCount = 0;
                store.PublicationObserver = (phase, _) => {
                    if (phase == AuthoritativePublicationPhase.BeforeStageWrite
                        && Interlocked.Increment(ref stageWriteCount) == 2)
                        throw publicationFailure;
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure));
            }

            await SendCommandAsync(stream, timeout.Token);
            await AssertConnectionClosesWithoutResponseAsync(stream, timeout.Token);
            var hostFailure = await Record.ExceptionAsync(async () =>
                await server.Completion.WaitAsync(timeout.Token));
            Assert.NotNull(hostFailure);
            Assert.Same(hostFailure, server.LastClientFailure);

            var after = store.Load()!;
            Assert.Equal(before.Generation + 1, after.Generation);
            Assert.Equal(capturesBefore + (failure == PollFailure.Publication ? 2 : 1), adapter.CaptureCount);
            Assert.Equal(eventsBefore + 1, adapter.EventDrainCount);
            Assert.Equal(snapshotsBefore + (failure == PollFailure.InvalidEventBatch ? 0 : 1), adapter.SnapshotCount);
            var durableReceipt = Assert.Single(Assert.Single(after.Participants).CommandReceipts);
            Assert.Equal(1UL, durableReceipt.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, durableReceipt.Status);
            if (injectedPublicationFailure is not null)
                Assert.Same(injectedPublicationFailure, server.LastClientFailure);
        } finally {
            await DisposeServerAsync(server, server.LastClientFailure);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ClockOnlyProbeWaitsForInFlightPublicationAndDoesNotReplyAfterFence()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new FixedClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var probeRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observeProbeRead = 0;
        var server = CreateServer(
            adapter,
            clock,
            store,
            clockGatedTransportDecorator: stream => new SignalStream(
                stream,
                () => Volatile.Read(ref observeProbeRead) != 0,
                () => probeRead.TrySetResult(true)));
        using var publicationEntered = new ManualResetEventSlim();
        using var releasePublication = new ManualResetEventSlim();
        var publicationFailure = new IOException("Injected clock-only probe barrier publication failure.");
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var publisherClient = new TcpClient();
            await publisherClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var publisherStream = publisherClient.GetStream();
            await ReadHandshakeAsync(publisherStream, true, timeout.Token, "clock-probe-publisher");

            using var probeClient = new TcpClient();
            await probeClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var probeStream = probeClient.GetStream();
            await ReadHandshakeAsync(probeStream, true, timeout.Token, "clock-probe-requester");

            var before = store.Load()!;
            store.PublicationObserver = (phase, _) => {
                if (phase != AuthoritativePublicationPhase.BeforeStageWrite)
                    return;
                publicationEntered.Set();
                if (!releasePublication.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("The test did not release the clock-only probe barrier.");
                throw publicationFailure;
            };

            // Publish from another participant so the clock-only response itself reaches
            // its authority-lock barrier while a real write is already in flight.
            await SendCommandAsync(publisherStream, timeout.Token);
            await Task.Run(() => publicationEntered.Wait(timeout.Token), timeout.Token);
            Volatile.Write(ref observeProbeRead, 1);
            await SendClockProbeAsync(probeStream, requestId: 42, timeout.Token);
            await probeRead.Task.WaitAsync(timeout.Token);
            var probeReplyTask = EnvelopeCodec.ReadAsync(probeStream, timeout.Token).AsTask();
            await Task.Delay(TimeSpan.FromMilliseconds(40), timeout.Token);
            Assert.False(probeReplyTask.IsCompleted);
            releasePublication.Set();

            try {
                var probeResponse = await probeReplyTask.WaitAsync(TimeSpan.FromSeconds(5), timeout.Token);
                Assert.Null(probeResponse);
            } catch (Exception exception) when (
                exception is IOException or SocketException or ObjectDisposedException) {
                // The host fences and closes the lane after authoritative failure.
            }
            var hostFailure = await Record.ExceptionAsync(async () =>
                await server.Completion.WaitAsync(timeout.Token));
            Assert.Same(publicationFailure, hostFailure);
            Assert.Same(publicationFailure, server.LastClientFailure);
            var after = store.Load()!;
            Assert.Equal(before.Generation, after.Generation);
            Assert.Equal(before.SerializedModuleState, after.SerializedModuleState);
        } finally {
            releasePublication.Set();
            await DisposeServerAsync(server, server.LastClientFailure);
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData(PollFailure.InvalidEventBatch)]
    [InlineData(PollFailure.InvalidSnapshot)]
    [InlineData(PollFailure.Publication)]
    public async Task CohortSnapshotPollFailureFencesWithoutPublishingAnyResponse(PollFailure failure)
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter();
        var clock = new FixedClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var server = CreateServer(adapter, clock, store);
        Exception? injectedPublicationFailure = null;
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            var capturesBefore = adapter.CaptureCount;
            switch (failure) {
            case PollFailure.InvalidEventBatch:
                adapter.ReturnInvalidEventBatch = true;
                break;
            case PollFailure.InvalidSnapshot:
                adapter.ReturnInvalidSnapshot = true;
                break;
            case PollFailure.Publication:
                var publicationFailure = new IOException("Injected cohort poll publication failure.");
                injectedPublicationFailure = publicationFailure;
                store.PublicationObserver = (phase, _) => {
                    if (phase == AuthoritativePublicationPhase.BeforeStageWrite)
                        throw publicationFailure;
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure));
            }

            await SendSnapshotRequestAsync(stream, timingRequestId: 31, timeout.Token);
            await AssertConnectionClosesWithoutResponseAsync(stream, timeout.Token);
            var hostFailure = await Record.ExceptionAsync(async () =>
                await server.Completion.WaitAsync(timeout.Token));
            Assert.NotNull(hostFailure);
            Assert.Same(hostFailure, server.LastClientFailure);

            var after = store.Load()!;
            Assert.Equal(before.Generation, after.Generation);
            Assert.Equal(before.SerializedModuleState, after.SerializedModuleState);
            Assert.Equal(capturesBefore + (failure == PollFailure.Publication ? 1 : 0), adapter.CaptureCount);
            if (injectedPublicationFailure is not null)
                Assert.Same(injectedPublicationFailure, server.LastClientFailure);
        } finally {
            await DisposeServerAsync(server, server.LastClientFailure);
            DeleteRoot(root);
        }
    }
}
