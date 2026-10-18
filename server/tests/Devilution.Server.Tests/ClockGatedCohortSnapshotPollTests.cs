using System.Net;
using System.Net.Sockets;
using System.Text.Json;
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
public sealed class ClockGatedCohortSnapshotPollTests
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

    [Fact]
    public async Task CohortCommandResponsePublishesOneCapturedBoundaryAfterAckCheckpoint()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter { EmitNextEvent = true };
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
            var eventsBefore = adapter.EventDrainCount;
            var snapshotsBefore = adapter.SnapshotCount;
            await SendCommandAsync(stream, timeout.Token);

            var acknowledgementEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, acknowledgementEnvelope.PayloadCase);
            var acknowledgement = Assert.Single(acknowledgementEnvelope.CommandAck.Results);
            Assert.Equal(1UL, acknowledgement.ClientSequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            Assert.Equal(FixedTick, acknowledgement.AppliedTick);

            var eventEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.EventBatch, eventEnvelope.PayloadCase);
            var snapshotEnvelope = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshotEnvelope.PayloadCase);

            var after = store.Load()!;
            Assert.Equal(before.Generation + 2, after.Generation);
            Assert.Equal(capturesBefore + 2, adapter.CaptureCount);
            Assert.Equal(eventsBefore + 1, adapter.EventDrainCount);
            Assert.Equal(snapshotsBefore + 1, adapter.SnapshotCount);
            Assert.Equal(FixedTick, eventEnvelope.EventBatch.Tick);
            Assert.Single(eventEnvelope.EventBatch.Events);
            Assert.Equal(FixedTick, snapshotEnvelope.Snapshot.Tick);
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

    private static AuthoritativeTcpServer CreateServer(
        SnapshotPollAdapter adapter,
        IAuthoritativeClock clock,
        AuthoritativeCohortStore store,
        bool includeProjectionProviders = true,
        Func<Stream, Stream>? clockGatedTransportDecorator = null,
        HostCommandTimingRecorder? commandTimingRecorder = null)
    {
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "cohort-poll-tests",
            "0.1.0",
            ContentHash,
            20) {
            RulesetIdentityHash = RulesetHash,
            GameMode = AuthoritativeGameMode.Multiplayer,
        });
        var commandServer = new AuthoritativeCommandServer(adapter);
        return includeProjectionProviders
            ? new AuthoritativeTcpServer(
                commandServer,
                handshake,
                clock,
                adapter,
                port: 0,
                address: IPAddress.Loopback) {
                CohortStore = store,
                UseClockGatedCommandScheduling = true,
                ClockGatedTransportDecorator = clockGatedTransportDecorator,
                CommandTimingRecorder = commandTimingRecorder,
            }
            : new AuthoritativeTcpServer(
                commandServer,
                handshake,
                clock,
                port: 0,
                address: IPAddress.Loopback,
                snapshotProvider: null,
                eventProvider: null,
                saveProvider: adapter,
                participantInitializer: adapter) {
                CohortStore = store,
                UseClockGatedCommandScheduling = true,
                ClockGatedTransportDecorator = clockGatedTransportDecorator,
                CommandTimingRecorder = commandTimingRecorder,
            };
    }

    private static async Task ReadHandshakeAsync(
        Stream stream,
        bool hasSnapshot,
        CancellationToken cancellationToken,
        string resumeToken = "cohort-poll-session")
    {
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "cohort-poll-client",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = ContentHash,
                RulesetIdentityHash = RulesetHash,
                ResumeToken = resumeToken,
                RequestedCharacterName = "PollTester",
                SupportsClockGatedCommands = true,
            },
        }, cancellationToken);
        var hello = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello.PayloadCase);
        Assert.True(hello.ServerHello.SupportsClockOnlyProbes);
        if (hasSnapshot) {
            var snapshot = await ReadRequiredEnvelopeAsync(stream, cancellationToken);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, snapshot.PayloadCase);
            Assert.Equal(FixedTick, snapshot.Snapshot.Tick);
        }
    }

    private static ValueTask SendSnapshotRequestAsync(
        Stream stream,
        ulong timingRequestId,
        CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, new Envelope {
            SnapshotRequest = new Envelope.Types.SnapshotRequest {
                TimingRequestId = timingRequestId,
            },
        }, cancellationToken);

    private static ValueTask SendClockProbeAsync(
        Stream stream,
        ulong requestId,
        CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClockProbeRequest = new ClockProbeRequest { RequestId = requestId },
        }, cancellationToken);

    private static ValueTask SendCommandAsync(Stream stream, CancellationToken cancellationToken)
        => SendCommandAtTickAsync(stream, FixedTick, cancellationToken);

    private static ValueTask SendCommandAtTickAsync(
        Stream stream,
        ulong requestedTick,
        CancellationToken cancellationToken)
        => EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = requestedTick,
                        AttackRequested = new AttackRequested { TargetEntityId = 1 },
                    },
                },
            },
        }, cancellationToken);

    private static async Task<Envelope> ReadRequiredEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var envelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        return envelope ?? throw new XunitException("The cohort host closed before sending the expected response.");
    }

    private static async Task AssertConnectionClosesWithoutResponseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        try {
            var response = await EnvelopeCodec.ReadAsync(stream, cancellationToken)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            Assert.Null(response);
        } catch (Exception exception) when (
            exception is IOException or SocketException or ObjectDisposedException) {
            // The host fences and closes the lane after authoritative failure.
        }
    }

    private static async Task DisposeServerAsync(
        AuthoritativeTcpServer server,
        Exception? expectedFailure)
    {
        var failure = await Record.ExceptionAsync(async () => await server.DisposeAsync());
        if (failure is not null)
            Assert.Same(expectedFailure, failure);
    }

    private static CancellationTokenSource CreateTimeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        return timeout;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
            await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "Godev-Cohort-Snapshot-Poll-" + Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    public enum PollFailure
    {
        InvalidEventBatch,
        InvalidSnapshot,
        Publication,
    }

    private sealed class SignalStream(
        Stream inner,
        Func<bool> isArmed,
        Action onRead,
        Func<bool>? shouldBlockWrite = null,
        Action? onWriteBlocked = null,
        Action? onWriteCanceled = null,
        Task? releaseWrite = null) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            Observe(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            Observe(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            Observe(read);
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var read = await inner.ReadAsync(buffer, offset, count, cancellationToken);
            Observe(read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
            => WriteObservedAsync(buffer, cancellationToken);
        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => WriteObservedAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }

        private void Observe(int read)
        {
            if (read > 0 && isArmed())
                onRead();
        }

        private async ValueTask WriteObservedAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (shouldBlockWrite?.Invoke() == true) {
                onWriteBlocked?.Invoke();
                try {
                    await (releaseWrite ?? throw new InvalidOperationException("A blocked write needs a release task.")).WaitAsync(cancellationToken);
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    onWriteCanceled?.Invoke();
                    throw;
                }
            }
            await inner.WriteAsync(buffer, cancellationToken);
        }
    }

    private sealed class FixedClock(ulong tick) : IAuthoritativeClock
    {
        public ulong CurrentTick => tick;
    }

    private sealed class MutableClock(ulong tick) : IAuthoritativeClock
    {
        private long currentTick = checked((long)tick);

        public ulong CurrentTick => checked((ulong)Interlocked.Read(ref currentTick));

        public void Set(ulong value)
            => Interlocked.Exchange(ref currentTick, checked((long)value));
    }

    private sealed class SnapshotPollAdapter :
        IAuthoritativeGameHostAdapter,
        IAuthoritativeGameCohortAdapter
    {
        private readonly object synchronization = new();
        private readonly List<AuthoritativeModuleCohortParticipant> participants = [];
        private ulong tick;
        private int eventDrainCount;
        private int snapshotCount;
        private int captureCount;
        private string? lastCapturedSerializedState;

        public bool EmitNextEvent { get; set; }

        public bool ReturnInvalidEventBatch { get; set; }

        public bool ReturnInvalidSnapshot { get; set; }

        public int CaptureCount => Volatile.Read(ref captureCount);

        public int EventDrainCount
        {
            get { lock (synchronization) return eventDrainCount; }
        }

        public int SnapshotCount
        {
            get { lock (synchronization) return snapshotCount; }
        }

        public bool SupportsCohortPersistence => true;

        public string CohortModuleId => "clock-gated-snapshot-poll-tests";

        public int CohortFormatVersion => 1;

        public bool IsEntityIdOccupied(uint entityId) => false;

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
            => CommandExecutionResult.Accepted;

        public void AdvanceTo(ulong tick)
        {
            lock (synchronization)
                this.tick = tick;
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong requestedTick)
        {
            lock (synchronization) {
                snapshotCount++;
                return new Snapshot {
                    Tick = requestedTick,
                    StateSha256 = ReturnInvalidSnapshot ? "invalid" : string.Empty,
                    Players = { new PlayerSnapshot { EntityId = entityId } },
                };
            }
        }

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong requestedTick)
        {
            lock (synchronization) {
                eventDrainCount++;
                if (ReturnInvalidEventBatch)
                    return new EventBatch { Tick = requestedTick, Events = { new GameEvent() } };
                if (!EmitNextEvent)
                    return new EventBatch { Tick = requestedTick };
                EmitNextEvent = false;
                return new EventBatch {
                    Tick = requestedTick,
                    Events = { new GameEvent { Experience = new ExperienceEvent { Amount = 1 } } },
                };
            }
        }

        public string ExportPlayerSave(string sessionId) => "{}";

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
        }

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
            if (selection.EntityId is not uint entityId || entityId == 0)
                throw new InvalidDataException("The test participant must receive a stable entity ID.");
            lock (synchronization)
                participants.Add(new AuthoritativeModuleCohortParticipant(sessionId, entityId));
        }

        public AuthoritativeModuleCohortState CaptureCohortState()
        {
            lock (synchronization) {
                Interlocked.Increment(ref captureCount);
                var serialized = SerializeCurrentStateCore();
                lastCapturedSerializedState = serialized;
                return new AuthoritativeModuleCohortState(
                    CohortModuleId,
                    CohortFormatVersion,
                    tick,
                    participants.OrderBy(participant => participant.SessionId, StringComparer.Ordinal).ToArray(),
                    serialized);
            }
        }

        public string SerializeCurrentState()
        {
            lock (synchronization)
                return SerializeCurrentStateCore();
        }

        public string LastCapturedSerializedState
        {
            get {
                lock (synchronization)
                    return lastCapturedSerializedState
                        ?? throw new InvalidOperationException("No cohort state has been captured yet.");
            }
        }

        public IAuthoritativePreparedCohortRestore PrepareCohortRestore(
            AuthoritativeModuleCohortState state)
            => throw new NotSupportedException("Restore is outside this snapshot-poll fixture.");

        public void CommitCohortRestore(IAuthoritativePreparedCohortRestore prepared)
            => throw new NotSupportedException("Restore is outside this snapshot-poll fixture.");

        private string SerializeCurrentStateCore()
            => JsonSerializer.Serialize(new SnapshotPollState(
                tick,
                eventDrainCount,
                snapshotCount,
                participants.OrderBy(participant => participant.SessionId, StringComparer.Ordinal).ToArray()));

        private sealed record SnapshotPollState(
            ulong Tick,
            int EventDrainCount,
            int SnapshotCount,
            IReadOnlyList<AuthoritativeModuleCohortParticipant> Participants);
    }
}
