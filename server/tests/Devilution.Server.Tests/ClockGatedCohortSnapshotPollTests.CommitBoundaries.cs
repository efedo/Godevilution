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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitBoundarySnapshotFallsBackForDuplicateOrMultiCommandWork(bool multiCommand)
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter { CommitBoundarySnapshotsEnabled = true };
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

            var firstBaseline = store.Load()!;
            var firstCaptureCount = adapter.CaptureCount;
            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            var ready = await responseReady.Task.WaitAsync(timeout.Token);
            Assert.Equal(1UL, ready.ClientSequence);
            clock.Set(FixedTick + 1);
            var firstAck = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, firstAck.PayloadCase);
            Assert.Equal(CommandStatus.Accepted, Assert.Single(firstAck.CommandAck.Results).Status);
            Assert.Equal(FixedTick + 1, Assert.Single(firstAck.CommandAck.Results).AppliedTick);
            var firstSnapshot = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, firstSnapshot.PayloadCase);
            // The command boundary is followed by the simulation loop's own
            // publication for the newly advanced physical tick.
            await WaitUntilAsync(
                () => store.Load()?.Generation >= firstBaseline.Generation + 2
                    && adapter.CaptureCount >= firstCaptureCount + 2,
                timeout.Token);
            Assert.Equal(firstBaseline.Generation + 2, store.Load()!.Generation);
            Assert.Equal(firstCaptureCount + 2, adapter.CaptureCount);

            var fallbackBaseline = store.Load()!;
            var fallbackCaptureCount = adapter.CaptureCount;
            if (multiCommand) {
                await SendCommandBatchAsync(
                    stream,
                    [
                        CreateCommand(2, FixedTick + 1),
                        CreateCommand(3, FixedTick + 1),
                    ],
                    timeout.Token);
            } else {
                await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            }

            var fallbackAck = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, fallbackAck.PayloadCase);
            if (multiCommand) {
                Assert.Equal(
                    [CommandStatus.Accepted, CommandStatus.Accepted],
                    fallbackAck.CommandAck.Results.Select(result => result.Status));
            } else {
                var duplicate = Assert.Single(fallbackAck.CommandAck.Results);
                Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
                Assert.Equal(
                    Assert.Single(firstAck.CommandAck.Results).ServerReceiptSequence,
                    duplicate.ServerReceiptSequence);
            }
            var fallbackSnapshot = await ReadRequiredEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, fallbackSnapshot.PayloadCase);
            Assert.Equal(FixedTick + 1, fallbackSnapshot.Snapshot.Tick);

            var after = store.Load()!;
            var expectedPublications = multiCommand ? 3UL : 1UL;
            Assert.Equal(fallbackBaseline.Generation + expectedPublications, after.Generation);
            Assert.Equal(fallbackCaptureCount + (int)expectedPublications, adapter.CaptureCount);
        } finally {
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData(PollFailure.InvalidEventBatch)]
    [InlineData(PollFailure.InvalidSnapshot)]
    [InlineData(PollFailure.Publication)]
    public async Task CommitBoundaryCaptureFailureFencesBeforeAnyResponse(PollFailure failure)
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter { CommitBoundarySnapshotsEnabled = true };
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
        Exception? injectedPublicationFailure = null;
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var stream = client.GetStream();
            await ReadHandshakeAsync(stream, hasSnapshot: true, timeout.Token);

            var before = store.Load()!;
            var snapshotsBefore = adapter.SnapshotCount;
            switch (failure) {
            case PollFailure.InvalidEventBatch:
                adapter.ReturnInvalidEventBatch = true;
                break;
            case PollFailure.InvalidSnapshot:
                adapter.ReturnInvalidSnapshot = true;
                break;
            case PollFailure.Publication:
                var publicationFailure = new IOException("Injected commit-boundary publication failure.");
                injectedPublicationFailure = publicationFailure;
                store.PublicationObserver = (phase, _) => {
                    if (phase == AuthoritativePublicationPhase.BeforeStageWrite)
                        throw publicationFailure;
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure));
            }

            await SendCommandWithSequenceAsync(stream, 1, FixedTick + 1, timeout.Token);
            var ready = await responseReady.Task.WaitAsync(timeout.Token);
            Assert.Equal(1UL, ready.ClientSequence);
            clock.Set(FixedTick + 1);
            await AssertConnectionClosesWithoutResponseAsync(stream, timeout.Token);
            var hostFailure = await Record.ExceptionAsync(async () =>
                await server.Completion.WaitAsync(timeout.Token));
            Assert.NotNull(hostFailure);
            Assert.Same(hostFailure, server.LastClientFailure);

            var after = store.Load()!;
            Assert.Equal(before.Generation, after.Generation);
            Assert.Equal(before.SerializedModuleState, after.SerializedModuleState);
            Assert.Empty(Assert.Single(after.Participants).CommandReceipts);
            Assert.Equal(1, adapter.EventDrainCount);
            Assert.Equal(snapshotsBefore + (failure == PollFailure.InvalidEventBatch ? 0 : 1), adapter.SnapshotCount);
            if (injectedPublicationFailure is not null)
                Assert.Same(injectedPublicationFailure, server.LastClientFailure);
        } finally {
            await DisposeServerAsync(server, server.LastClientFailure);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CommitBoundaryCancellationAfterEligibilityKeepsDurableReceiptWithoutFencingOtherParticipants()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter {
            CommitBoundarySnapshotsEnabled = true,
            EmitNextEvent = true,
        };
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var responseReady = Channel.CreateUnbounded<(string SessionId, ulong ClientSequence)>();
        var pipelineCancellationObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandPipelineReadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipelineCancellationRegistration = default(CancellationTokenRegistration);
        var pipelineCancellationRegistrationInstalled = 0;
        var firstPipelineReadCount = 0;
        var decoratedConnections = 0;
        var abortiveCloseRequested = 0;
        var server = CreateServer(
            adapter,
            clock,
            store,
            clockGatedTransportDecorator: stream => {
                var connectionOrdinal = Interlocked.Increment(ref decoratedConnections);
                return new SignalStream(
                    stream,
                    () => false,
                    () => { },
                    onReadStarted: connectionOrdinal == 1
                        ? token => {
                            var readCount = Interlocked.Increment(ref firstPipelineReadCount);
                            if (Interlocked.CompareExchange(ref pipelineCancellationRegistrationInstalled, 1, 0) == 0) {
                                pipelineCancellationRegistration = token.Register(
                                    () => pipelineCancellationObserved.TrySetResult(true));
                            }
                            // The decorated reader is installed after handshake. A
                            // command envelope uses at least two reads (length and
                            // payload); the third starts the next pending read.
                            if (readCount >= 3)
                                commandPipelineReadStarted.TrySetResult(true);
                        }
                        : null,
                    failReadOnEof: connectionOrdinal == 1
                        ? () => Volatile.Read(ref abortiveCloseRequested) != 0
                        : null);
            },
            commitBoundaryResponseReadyObserverForTests: (sessionId, clientSequence) =>
                responseReady.Writer.TryWrite((sessionId, clientSequence)));
        try {
            server.Start();
            using var timeout = CreateTimeout();
            using var firstClient = new TcpClient();
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var firstStream = firstClient.GetStream();
            var firstHandshakeSnapshot = await ReadHandshakeSnapshotAsync(
                firstStream,
                "cohort-poll-session-cancelled",
                "CancelTester",
                timeout.Token);

            using var secondClient = new TcpClient();
            await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var secondStream = secondClient.GetStream();
            var secondHandshakeSnapshot = await ReadHandshakeSnapshotAsync(
                secondStream,
                "cohort-poll-session-survivor",
                "SurvivorTester",
                timeout.Token);
            var firstEntityId = Assert.Single(firstHandshakeSnapshot.Players).EntityId;
            var secondEntityId = Assert.Single(secondHandshakeSnapshot.Players).EntityId;
            Assert.NotEqual(firstEntityId, secondEntityId);

            var beforeFirstCommand = store.Load()!;
            var eventDrainsBefore = adapter.EventDrainCount;
            var snapshotsBefore = adapter.SnapshotCount;
            adapter.OnSecondCommitBoundaryFeatureRead = () => {
                // The first post-handshake feature read registers the commit callback
                // during admission. The second occurs after its initial token check.
                // Force this fixture's pending read down the existing transport-failure
                // path; a socket close alone may surface as clean EOF and leave the
                // per-connection pipeline token uncanceled.
                if (!commandPipelineReadStarted.Task.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("The first participant reader did not reach its next read after the command.");
                Interlocked.Exchange(ref abortiveCloseRequested, 1);
                firstClient.Client.LingerState = new LingerOption(true, 0);
                firstClient.Dispose();
                if (!pipelineCancellationObserved.Task.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("The first participant pipeline did not observe its transport cancellation.");
            };

            await SendCommandWithSequenceAsync(firstStream, 1, FixedTick + 1, timeout.Token);
            var firstReady = await responseReady.Reader.ReadAsync(timeout.Token);
            Assert.Equal(1UL, firstReady.ClientSequence);
            clock.Set(FixedTick + 1);
            await pipelineCancellationObserved.Task.WaitAsync(timeout.Token);
            await WaitUntilAsync(
                () => store.Load()?.Participants
                    .SingleOrDefault(participant => participant.SessionId == firstReady.SessionId)?
                    .CommandReceipts.Any(receipt => receipt.ClientSequence == 1
                        && receipt.Status == CommandStatus.Accepted
                        && receipt.AppliedTick == FixedTick + 1) == true,
                timeout.Token);

            var afterCancelledCommand = store.Load()!;
            Assert.True(afterCancelledCommand.Generation > beforeFirstCommand.Generation);
            Assert.Equal(eventDrainsBefore, adapter.EventDrainCount);
            Assert.Equal(snapshotsBefore, adapter.SnapshotCount);

            await SendCommandWithSequenceAsync(secondStream, 1, FixedTick + 2, timeout.Token);
            var secondReady = await responseReady.Reader.ReadAsync(timeout.Token);
            Assert.Equal(1UL, secondReady.ClientSequence);
            Assert.NotEqual(firstReady.SessionId, secondReady.SessionId);
            clock.Set(FixedTick + 2);

            var secondAcknowledgement = await ReadRequiredEnvelopeAsync(secondStream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, secondAcknowledgement.PayloadCase);
            var secondResult = Assert.Single(secondAcknowledgement.CommandAck.Results);
            Assert.Equal(CommandStatus.Accepted, secondResult.Status);
            Assert.Equal(FixedTick + 2, secondResult.AppliedTick);
            var secondEvent = await ReadRequiredEnvelopeAsync(secondStream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.EventBatch, secondEvent.PayloadCase);
            Assert.Equal(FixedTick + 2, secondEvent.EventBatch.Tick);
            Assert.Single(secondEvent.EventBatch.Events);
            var secondSnapshot = await ReadRequiredEnvelopeAsync(secondStream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, secondSnapshot.PayloadCase);
            Assert.Equal(FixedTick + 2, secondSnapshot.Snapshot.Tick);
            Assert.Equal(secondEntityId, Assert.Single(secondSnapshot.Snapshot.Players).EntityId);

            var afterSurvivorCommand = store.Load()!;
            Assert.True(afterSurvivorCommand.Generation > afterCancelledCommand.Generation);
            Assert.Equal(eventDrainsBefore + 1, adapter.EventDrainCount);
            Assert.Equal(snapshotsBefore + 1, adapter.SnapshotCount);
            var firstDurableReceipt = Assert.Single(afterSurvivorCommand.Participants
                .Single(participant => participant.SessionId == firstReady.SessionId).CommandReceipts);
            Assert.Equal(CommandStatus.Accepted, firstDurableReceipt.Status);
            Assert.Equal(FixedTick + 1, firstDurableReceipt.AppliedTick);
            var secondDurableReceipt = Assert.Single(afterSurvivorCommand.Participants
                .Single(participant => participant.SessionId == secondReady.SessionId).CommandReceipts);
            Assert.Equal(CommandStatus.Accepted, secondDurableReceipt.Status);
            Assert.Equal(FixedTick + 2, secondDurableReceipt.AppliedTick);
        } finally {
            pipelineCancellationRegistration.Dispose();
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CommitBoundaryResponseRetainsItsOwnParticipantProjectionAcrossLaterCommit()
    {
        var root = NewRoot();
        var adapter = new SnapshotPollAdapter {
            CommitBoundarySnapshotsEnabled = true,
            EmitNextEvent = true,
            ReuseSnapshotInstance = true,
        };
        var clock = new MutableClock(FixedTick);
        var store = new AuthoritativeCohortStore(root);
        var connectionOrdinal = 0;
        var blockFirstWrite = 0;
        var firstWriteClaimed = 0;
        var firstWriteBlocked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var responseReady = Channel.CreateUnbounded<(string SessionId, ulong ClientSequence)>();
        var server = CreateServer(
            adapter,
            clock,
            store,
            commitBoundaryResponseReadyObserverForTests: (sessionId, clientSequence) =>
                responseReady.Writer.TryWrite((sessionId, clientSequence)),
            clockGatedTransportDecorator: stream => {
                if (Interlocked.Increment(ref connectionOrdinal) != 1)
                    return stream;
                return new SignalStream(
                    stream,
                    () => false,
                    () => { },
                    shouldBlockWrite: () => Volatile.Read(ref blockFirstWrite) != 0
                        && Interlocked.CompareExchange(ref firstWriteClaimed, 1, 0) == 0,
                    onWriteBlocked: () => firstWriteBlocked.TrySetResult(true),
                    releaseWrite: releaseFirstWrite.Task);
            });
        try {
            server.Start();
            using var timeout = CreateTimeout();

            using var firstClient = new TcpClient();
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var firstStream = firstClient.GetStream();
            await ReadHandshakeAsync(firstStream, hasSnapshot: true, timeout.Token, "commit-boundary-first");
            var firstEntityId = Assert.Single(adapter.ParticipantEntityIds);

            Volatile.Write(ref blockFirstWrite, 1);
            await SendCommandWithSequenceAsync(firstStream, 1, FixedTick + 1, timeout.Token);
            var firstReady = await responseReady.Reader.ReadAsync(timeout.Token);
            Assert.Equal(1UL, firstReady.ClientSequence);
            clock.Set(FixedTick + 1);
            await firstWriteBlocked.Task.WaitAsync(timeout.Token);
            await WaitUntilAsync(
                () => store.Load()?.Participants
                    .SelectMany(participant => participant.CommandReceipts)
                    .Any(receipt => receipt.ClientSequence == 1 && receipt.Status == CommandStatus.Accepted) == true,
                timeout.Token);

            // The first ACK write is held after its command-boundary response
            // has been captured and durably published. A second session reuses
            // the adapter's mutable projection object at a later boundary.
            var firstReceipt = Assert.Single(Assert.Single(
                store.Load()!.Participants,
                participant => participant.EntityId == firstEntityId).CommandReceipts);
            Assert.Equal(CommandStatus.Accepted, firstReceipt.Status);
            Assert.Equal(FixedTick + 1, firstReceipt.AppliedTick);

            clock.Set(FixedTick + 2);
            using var secondClient = new TcpClient();
            await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, timeout.Token);
            await using var secondStream = secondClient.GetStream();
            var secondHandshakeSnapshot = await ReadHandshakeSnapshotAsync(
                secondStream,
                "commit-boundary-second",
                "PollTesterTwo",
                timeout.Token);
            Assert.Equal(FixedTick + 2, secondHandshakeSnapshot.Tick);
            var secondEntityId = Assert.Single(adapter.ParticipantEntityIds, entityId => entityId != firstEntityId);

            await SendCommandWithSequenceAsync(secondStream, 1, FixedTick + 3, timeout.Token);
            var secondReady = await responseReady.Reader.ReadAsync(timeout.Token);
            Assert.Equal(1UL, secondReady.ClientSequence);
            Assert.NotEqual(firstReady.SessionId, secondReady.SessionId);
            clock.Set(FixedTick + 3);
            await WaitUntilAsync(
                () => store.Load()?.Participants
                    .Where(participant => participant.EntityId == secondEntityId)
                    .SelectMany(participant => participant.CommandReceipts)
                    .Any(receipt => receipt.ClientSequence == 1
                        && receipt.Status == CommandStatus.Accepted
                        && receipt.AppliedTick == FixedTick + 3) == true,
                timeout.Token);
            var secondAck = await ReadRequiredEnvelopeAsync(secondStream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, secondAck.PayloadCase);
            Assert.Equal(FixedTick + 3, Assert.Single(secondAck.CommandAck.Results).AppliedTick);
            var secondSnapshot = await ReadRequiredEnvelopeAsync(secondStream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, secondSnapshot.PayloadCase);
            Assert.Equal(FixedTick + 3, secondSnapshot.Snapshot.Tick);
            Assert.Equal(secondEntityId, Assert.Single(secondSnapshot.Snapshot.Players).EntityId);

            releaseFirstWrite.TrySetResult(true);
            var firstAck = await ReadRequiredEnvelopeAsync(firstStream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.CommandAck, firstAck.PayloadCase);
            Assert.Equal(FixedTick + 1, Assert.Single(firstAck.CommandAck.Results).AppliedTick);
            var firstEvent = await ReadRequiredEnvelopeAsync(firstStream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.EventBatch, firstEvent.PayloadCase);
            Assert.Equal(FixedTick + 1, firstEvent.EventBatch.Tick);
            var firstSnapshot = await ReadRequiredEnvelopeAsync(firstStream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, firstSnapshot.PayloadCase);
            Assert.Equal(FixedTick + 1, firstSnapshot.Snapshot.Tick);
            Assert.Equal(firstEntityId, Assert.Single(firstSnapshot.Snapshot.Players).EntityId);
        } finally {
            releaseFirstWrite.TrySetResult(true);
            await DisposeServerAsync(server, expectedFailure: null);
            DeleteRoot(root);
        }
    }
}
