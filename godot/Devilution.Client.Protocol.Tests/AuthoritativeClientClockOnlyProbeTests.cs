using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeClientClockOnlyProbeTests
{
    [Fact]
    public async Task NegotiatedProbeCorrelatesSeparatelyAndDoesNotReplaceSnapshotPoll()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var probeSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(stream, supportsClockOnlyProbes: true, timeout.Token);

            var probeEnvelope = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, probeEnvelope.PayloadCase);
            var probe = probeEnvelope.ClockProbeRequest;
            Assert.NotEqual(0UL, probe.RequestId);
            probeSeen.TrySetResult();

            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply {
                    RequestId = probe.RequestId + 1,
                    ServerSchedulingClockTick = 9000,
                },
            }, timeout.Token);
            using (var noCommand = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token)) {
                noCommand.CancelAfter(100);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => ReadEnvelopeAsync(stream, noCommand.Token));
            }

            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply {
                    RequestId = probe.RequestId,
                    ServerSchedulingClockTick = 500,
                },
            }, timeout.Token);
            // A delayed snapshot older than the physical server-clock sample
            // must not rewind command scheduling.
            await WriteEnvelopeAsync(stream, new Envelope {
                Snapshot = new Snapshot { Tick = 101 },
            }, timeout.Token);
            var command = Assert.Single((await ReadEnvelopeAsync(stream, timeout.Token)).CommandBatch.Commands);
            Assert.Equal(1UL, command.ClientSequence);
            Assert.InRange(command.RequestedTick, 500UL, 8999UL);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = new CommandAck { Results = { new CommandResult {
                    ClientSequence = command.ClientSequence,
                    Status = CommandStatus.Accepted,
                    AppliedTick = command.RequestedTick,
                } } },
            }, timeout.Token);
            await WriteEnvelopeAsync(stream, new Envelope {
                Snapshot = new Snapshot { Tick = command.RequestedTick },
            }, timeout.Token);

            Envelope snapshotRequest;
            while (true) {
                snapshotRequest = await ReadEnvelopeAsync(stream, timeout.Token);
                if (snapshotRequest.PayloadCase != Envelope.PayloadOneofCase.ClockProbeRequest)
                    break;
                await WriteEnvelopeAsync(stream, new Envelope {
                    TimingReply = new TimingReply {
                        RequestId = snapshotRequest.ClockProbeRequest.RequestId,
                        ServerSchedulingClockTick = command.RequestedTick,
                    },
                }, timeout.Token);
            }
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, snapshotRequest.PayloadCase);
            Assert.Equal(0UL, snapshotRequest.SnapshotRequest.TimingRequestId);
            await WriteEnvelopeAsync(stream, new Envelope {
                Snapshot = new Snapshot { Tick = 102 },
            }, timeout.Token);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener, TimeSpan.FromSeconds(1)));
        await client.ConnectAsync(timeout.Token);
        // Leave an ordinary snapshot poll due before forcing clock calibration.
        await Task.Delay(1100, timeout.Token);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionY = 1 } },
            100);
        await client.PollAsync(timeout.Token);
        await probeSeen.Task.WaitAsync(TimeSpan.FromSeconds(2), timeout.Token);

        for (var attempt = 0; attempt < 300 && !serverTask.IsCompleted; attempt++) {
            await Task.Delay(10, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        await serverTask.WaitAsync(timeout.Token);
        Assert.Equal(0, client.PendingCommandCount);
        var followupSnapshotObserved = false;
        for (var attempt = 0; attempt < 100 && !followupSnapshotObserved; attempt++) {
            followupSnapshotObserved = client.DrainMessages().Any(message => message.Snapshot?.Tick == 102);
            if (!followupSnapshotObserved)
                await Task.Delay(10, timeout.Token);
        }
        Assert.True(followupSnapshotObserved, "The admitted post-poll snapshot should reach the client after the server observes its request.");
    }

    [Fact]
    public async Task NegotiatedProbeRequiresClockSampleAndTreatsPresentZeroAsValid()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(stream, supportsClockOnlyProbes: true, timeout.Token);

            var probe = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, probe.PayloadCase);
            Assert.NotEqual(0UL, probe.ClockProbeRequest.RequestId);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = probe.ClockProbeRequest.RequestId },
            }, timeout.Token);
            using (var noCommand = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token)) {
                noCommand.CancelAfter(100);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => ReadEnvelopeAsync(stream, noCommand.Token));
            }

            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply {
                    RequestId = probe.ClockProbeRequest.RequestId,
                    ServerSchedulingClockTick = 0,
                },
            }, timeout.Token);
            var command = Assert.Single((await ReadEnvelopeAsync(stream, timeout.Token)).CommandBatch.Commands);
            Assert.Equal(1UL, command.ClientSequence);
            Assert.InRange(command.RequestedTick, 100UL, 999UL);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener, TimeSpan.FromMinutes(1)));
        await client.ConnectAsync(timeout.Token);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionY = 1 } },
            100);
        await client.PollAsync(timeout.Token);
        for (var attempt = 0; attempt < 300 && !serverTask.IsCompleted; attempt++) {
            await Task.Delay(10, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        await serverTask.WaitAsync(timeout.Token);
    }

    [Fact]
    public async Task OutstandingClockProbeDoesNotStarvePeriodicSnapshotRequests()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(stream, supportsClockOnlyProbes: true, timeout.Token);

            var probe = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, probe.PayloadCase);
            Assert.NotEqual(0UL, probe.ClockProbeRequest.RequestId);

            var firstSnapshotRequest = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, firstSnapshotRequest.PayloadCase);
            Assert.Equal(0UL, firstSnapshotRequest.SnapshotRequest.TimingRequestId);

            await WriteEnvelopeAsync(stream, new Envelope {
                Snapshot = new Snapshot { Tick = 101 },
            }, timeout.Token);

            var secondSnapshotRequest = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, secondSnapshotRequest.PayloadCase);
            Assert.Equal(0UL, secondSnapshotRequest.SnapshotRequest.TimingRequestId);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener, TimeSpan.FromMilliseconds(25)));
        await client.ConnectAsync(timeout.Token);
        await Task.Delay(35, timeout.Token);
        await client.PollAsync(timeout.Token);
        for (var poll = 0; poll < 20 && !serverTask.IsCompleted; poll++) {
            await Task.Delay(25, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        await serverTask.WaitAsync(timeout.Token);
    }

    [Fact]
    public async Task IdentityRejectedSnapshotDoesNotResetSnapshotPollCadence()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var invalidSnapshotSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextPollElapsed = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(stream, supportsClockOnlyProbes: true, timeout.Token);

            // The handshake snapshot is the last valid receipt. Send a partial
            // identity late in its interval; it must not move the poll baseline.
            await Task.Delay(900, timeout.Token);
            await WriteEnvelopeAsync(stream, new Envelope {
                Snapshot = new Snapshot {
                    Tick = 101,
                    ContentManifestHash = "content-hash",
                },
            }, timeout.Token);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            invalidSnapshotSent.TrySetResult();

            while (true) {
                var envelope = await ReadEnvelopeAsync(stream, timeout.Token);
                if (envelope.PayloadCase == Envelope.PayloadOneofCase.ClockProbeRequest) {
                    await WriteEnvelopeAsync(stream, new Envelope {
                        TimingReply = new TimingReply {
                            RequestId = envelope.ClockProbeRequest.RequestId,
                            ServerSchedulingClockTick = 100,
                        },
                    }, timeout.Token);
                    continue;
                }

                Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, envelope.PayloadCase);
                Assert.Equal(0UL, envelope.SnapshotRequest.TimingRequestId);
                nextPollElapsed.TrySetResult(stopwatch.Elapsed);
                return;
            }
        }, timeout.Token);

        const int pollIntervalMilliseconds = 1500;
        await using var client = new AuthoritativeClient(
            Options(listener, TimeSpan.FromMilliseconds(pollIntervalMilliseconds)));
        await client.ConnectAsync(timeout.Token);
        await invalidSnapshotSent.Task.WaitAsync(timeout.Token);

        for (var attempt = 0; attempt < 300 && !serverTask.IsCompleted; attempt++) {
            await client.PollAsync(timeout.Token);
            await Task.Delay(10, timeout.Token);
        }

        await serverTask.WaitAsync(timeout.Token);
        var elapsedAfterInvalid = await nextPollElapsed.Task;
        Assert.True(
            elapsedAfterInvalid < TimeSpan.FromMilliseconds(1200),
            $"The poll should remain due from the last valid snapshot receipt; it arrived {elapsedAfterInvalid.TotalMilliseconds:F0} ms after the rejected identity snapshot.");
        Assert.Contains(
            client.DrainMessages(),
            message => message.Error is { Code: ProtocolErrorCode.InvalidMessage } error
                && error.Detail.Contains("incomplete ruleset identity", StringComparison.Ordinal));
    }
    [Fact]
    public async Task NegotiatedSnapshotPollDoesNotQueueDuplicatesWhileResponseIsDelayed()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var delayedSnapshotSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSnapshotRequestSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(stream, supportsClockOnlyProbes: true, timeout.Token);

            var probe = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, probe.PayloadCase);
            var firstSnapshotRequest = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, firstSnapshotRequest.PayloadCase);
            Assert.Equal(0UL, firstSnapshotRequest.SnapshotRequest.TimingRequestId);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply {
                    RequestId = probe.ClockProbeRequest.RequestId,
                    ServerSchedulingClockTick = 100,
                },
            }, timeout.Token);

            var noSnapshotWatch = System.Diagnostics.Stopwatch.StartNew();
            while (noSnapshotWatch.Elapsed < TimeSpan.FromMilliseconds(150)) {
                Assert.False(stream.DataAvailable, "An outstanding full snapshot must suppress clock-only probes and duplicate polls.");
                await Task.Delay(2, timeout.Token);
            }

            await WriteEnvelopeAsync(stream, new Envelope {
                Snapshot = new Snapshot { Tick = 101 },
            }, timeout.Token);
            delayedSnapshotSent.TrySetResult();
            while (true) {
                var envelope = await ReadEnvelopeAsync(stream, timeout.Token);
                if (envelope.PayloadCase == Envelope.PayloadOneofCase.ClockProbeRequest) {
                    await WriteEnvelopeAsync(stream, new Envelope {
                        TimingReply = new TimingReply {
                            RequestId = envelope.ClockProbeRequest.RequestId,
                            ServerSchedulingClockTick = 100,
                        },
                    }, timeout.Token);
                    continue;
                }
                Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, envelope.PayloadCase);
                Assert.Equal(0UL, envelope.SnapshotRequest.TimingRequestId);
                secondSnapshotRequestSeen.TrySetResult();
                return;
            }
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener, TimeSpan.FromMilliseconds(25)));
        await client.ConnectAsync(timeout.Token);
        for (var poll = 0; poll < 5; poll++) {
            await Task.Delay(35, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        await delayedSnapshotSent.Task.WaitAsync(timeout.Token);
        for (var poll = 0; poll < 20 && !secondSnapshotRequestSeen.Task.IsCompleted; poll++) {
            await Task.Delay(25, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        await serverTask.WaitAsync(timeout.Token);
        Assert.True(secondSnapshotRequestSeen.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ClockOnlyProbeWaitsForAckFollowingSnapshotAtOrBeyondAppliedTick()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(stream, supportsClockOnlyProbes: true, timeout.Token);

            var initialProbe = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, initialProbe.PayloadCase);
            var initialPoll = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, initialPoll.PayloadCase);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply {
                    RequestId = initialProbe.ClockProbeRequest.RequestId,
                    ServerSchedulingClockTick = 100,
                },
            }, timeout.Token);
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 100 } }, timeout.Token);

            var command = Assert.Single((await ReadEnvelopeAsync(stream, timeout.Token)).CommandBatch.Commands);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = new CommandAck { Results = { new CommandResult {
                    ClientSequence = command.ClientSequence,
                    Status = CommandStatus.Rejected,
                    RejectReason = CommandRejectReason.NotAllowed,
                    AppliedTick = 120,
                } } },
            }, timeout.Token);

            var postAckWatch = System.Diagnostics.Stopwatch.StartNew();
            while (postAckWatch.Elapsed < TimeSpan.FromMilliseconds(80)) {
                Assert.False(stream.DataAvailable, "A validated ACK must hold due probes and polls until its following snapshot or timeout.");
                await Task.Delay(2, timeout.Token);
            }
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 119 } }, timeout.Token);
            var lowerSnapshotWatch = System.Diagnostics.Stopwatch.StartNew();
            while (lowerSnapshotWatch.Elapsed < TimeSpan.FromMilliseconds(80)) {
                Assert.False(stream.DataAvailable, "A lower-tick snapshot must not release the post-ACK response gate or trigger another poll.");
                await Task.Delay(2, timeout.Token);
            }
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 120 } }, timeout.Token);

            var nextProbe = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, nextProbe.PayloadCase);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply {
                    RequestId = nextProbe.ClockProbeRequest.RequestId,
                    ServerSchedulingClockTick = 120,
                },
            }, timeout.Token);
            var resumedPoll = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, resumedPoll.PayloadCase);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener, TimeSpan.FromMilliseconds(25)));
        await client.ConnectAsync(timeout.Token);
        await Task.Delay(35, timeout.Token);
        await client.PollAsync(timeout.Token);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionY = 1 } },
            100);
        for (var poll = 0; poll < 500 && !serverTask.IsCompleted; poll++) {
            await Task.Delay(10, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        await serverTask.WaitAsync(timeout.Token);
        Assert.Equal(0, client.PendingCommandCount);
    }

    [Fact]
    public async Task ClockOnlyProbeUsesBoundedFallbackWhenAckSnapshotNeverArrives()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(stream, supportsClockOnlyProbes: true, timeout.Token);

            var initialProbe = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, initialProbe.PayloadCase);
            var initialPoll = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, initialPoll.PayloadCase);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply {
                    RequestId = initialProbe.ClockProbeRequest.RequestId,
                    ServerSchedulingClockTick = 100,
                },
            }, timeout.Token);
            await WriteEnvelopeAsync(stream, new Envelope { Snapshot = new Snapshot { Tick = 100 } }, timeout.Token);

            var command = Assert.Single((await ReadEnvelopeAsync(stream, timeout.Token)).CommandBatch.Commands);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = new CommandAck { Results = { new CommandResult {
                    ClientSequence = command.ClientSequence,
                    Status = CommandStatus.Rejected,
                    RejectReason = CommandRejectReason.NotAllowed,
                    AppliedTick = 120,
                } } },
            }, timeout.Token);
            var fallbackProbe = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, fallbackProbe.PayloadCase);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply {
                    RequestId = fallbackProbe.ClockProbeRequest.RequestId,
                    ServerSchedulingClockTick = 120,
                },
            }, timeout.Token);
            var retriedSnapshot = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, retriedSnapshot.PayloadCase);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener, TimeSpan.FromMilliseconds(25)));
        await client.ConnectAsync(timeout.Token);
        await Task.Delay(35, timeout.Token);
        await client.PollAsync(timeout.Token);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionY = 1 } },
            100);
        for (var poll = 0; poll < 400 && !serverTask.IsCompleted; poll++) {
            await Task.Delay(15, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        await serverTask.WaitAsync(timeout.Token);
        Assert.Equal(0, client.PendingCommandCount);
    }

    [Fact]
    public async Task ReconnectClearsNegotiatedSnapshotResponseGates()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var firstAcknowledgementSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using (var connection = await listener.AcceptTcpClientAsync(timeout.Token)) {
                await using var stream = connection.GetStream();
                await DispatchHandshakeAsync(stream, supportsClockOnlyProbes: true, timeout.Token);
                var probe = await ReadEnvelopeAsync(stream, timeout.Token);
                Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, probe.PayloadCase);
                var initialPoll = await ReadEnvelopeAsync(stream, timeout.Token);
                Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, initialPoll.PayloadCase);
                await WriteEnvelopeAsync(stream, new Envelope {
                    TimingReply = new TimingReply {
                        RequestId = probe.ClockProbeRequest.RequestId,
                        ServerSchedulingClockTick = 9000,
                    },
                }, timeout.Token);
                var command = Assert.Single((await ReadEnvelopeAsync(stream, timeout.Token)).CommandBatch.Commands);
                Assert.True(command.RequestedTick >= 9000);
                await WriteEnvelopeAsync(stream, new Envelope {
                    CommandAck = new CommandAck { Results = { new CommandResult {
                        ClientSequence = command.ClientSequence,
                        Status = CommandStatus.Rejected,
                        RejectReason = CommandRejectReason.NotAllowed,
                        AppliedTick = command.RequestedTick,
                    } } },
                }, timeout.Token);
                firstAcknowledgementSent.TrySetResult();
                await Assert.ThrowsAnyAsync<IOException>(async () => {
                    using var closeTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                    closeTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                    await ReadEnvelopeAsync(stream, closeTimeout.Token);
                });
            }

            using var resumedConnection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var resumedStream = resumedConnection.GetStream();
            await DispatchHandshakeAsync(resumedStream, supportsClockOnlyProbes: true, timeout.Token);
            var resumedProbe = await ReadEnvelopeAsync(resumedStream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, resumedProbe.PayloadCase);
            await WriteEnvelopeAsync(resumedStream, new Envelope {
                TimingReply = new TimingReply {
                    RequestId = resumedProbe.ClockProbeRequest.RequestId,
                    ServerSchedulingClockTick = 100,
                },
            }, timeout.Token);
            var resumedCommand = Assert.Single((await ReadEnvelopeAsync(resumedStream, timeout.Token)).CommandBatch.Commands);
            Assert.True(resumedCommand.RequestedTick < 9000, "A previous connection's clock sample must not leak into the new transport epoch.");
            await WriteEnvelopeAsync(resumedStream, new Envelope {
                CommandAck = new CommandAck { Results = { new CommandResult {
                    ClientSequence = resumedCommand.ClientSequence,
                    Status = CommandStatus.Accepted,
                    AppliedTick = resumedCommand.RequestedTick,
                } } },
            }, timeout.Token);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener, TimeSpan.FromMilliseconds(25)));
        await client.ConnectAsync(timeout.Token);
        await Task.Delay(35, timeout.Token);
        await client.PollAsync(timeout.Token);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionY = 1 } },
            100);
        for (var poll = 0; poll < 300 && !firstAcknowledgementSent.Task.IsCompleted; poll++) {
            await Task.Delay(10, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        await firstAcknowledgementSent.Task.WaitAsync(timeout.Token);
        for (var poll = 0; poll < 300 && client.PendingCommandCount != 0; poll++) {
            await Task.Delay(10, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        Assert.Equal(0, client.PendingCommandCount);
        await client.ReconnectAsync(timeout.Token);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
            100);
        for (var attempt = 0; attempt < 300 && client.PendingCommandCount != 0; attempt++) {
            await Task.Delay(10, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        Assert.Equal(0, client.PendingCommandCount);
        await serverTask.WaitAsync(timeout.Token);
    }

    [Fact]
    public async Task NegotiatedSnapshotPollRetriesAfterBoundedResponseTimeout()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var retrySeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(stream, supportsClockOnlyProbes: true, timeout.Token);

            var probe = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, probe.PayloadCase);
            var firstSnapshotRequest = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, firstSnapshotRequest.PayloadCase);

            while (true) {
                var envelope = await ReadEnvelopeAsync(stream, timeout.Token);
                if (envelope.PayloadCase != Envelope.PayloadOneofCase.SnapshotRequest)
                    continue;
                Assert.Equal(0UL, envelope.SnapshotRequest.TimingRequestId);
                retrySeen.TrySetResult();
                return;
            }
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener, TimeSpan.FromMilliseconds(25)));
        await client.ConnectAsync(timeout.Token);
        await Task.Delay(35, timeout.Token);
        await client.PollAsync(timeout.Token);
        for (var poll = 0; poll < 75 && !retrySeen.Task.IsCompleted; poll++) {
            await Task.Delay(35, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        await retrySeen.Task.WaitAsync(timeout.Token);
        await serverTask.WaitAsync(timeout.Token);
    }

    [Fact]
    public async Task ClockOnlyProbePrecedesSnapshotWhenBothAreDue()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(stream, supportsClockOnlyProbes: true, timeout.Token);

            var probe = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.ClockProbeRequest, probe.PayloadCase);
            Assert.NotEqual(0UL, probe.ClockProbeRequest.RequestId);
            var snapshot = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, snapshot.PayloadCase);
            Assert.Equal(0UL, snapshot.SnapshotRequest.TimingRequestId);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener, TimeSpan.FromMilliseconds(25)));
        await client.ConnectAsync(timeout.Token);
        await Task.Delay(35, timeout.Token);
        await client.PollAsync(timeout.Token);
        await serverTask.WaitAsync(timeout.Token);
    }

    [Fact]
    public async Task MissingNegotiationRetainsSnapshotTimingProbeFallback()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = Task.Run(async () => {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await DispatchHandshakeAsync(stream, supportsClockOnlyProbes: false, timeout.Token);

            var probe = await ReadEnvelopeAsync(stream, timeout.Token);
            Assert.Equal(Envelope.PayloadOneofCase.SnapshotRequest, probe.PayloadCase);
            Assert.NotEqual(0UL, probe.SnapshotRequest.TimingRequestId);
            await WriteEnvelopeAsync(stream, new Envelope {
                TimingReply = new TimingReply { RequestId = probe.SnapshotRequest.TimingRequestId },
            }, timeout.Token);
            var command = Assert.Single((await ReadEnvelopeAsync(stream, timeout.Token)).CommandBatch.Commands);
            await WriteEnvelopeAsync(stream, new Envelope {
                CommandAck = Accepted(command.ClientSequence),
            }, timeout.Token);
        }, timeout.Token);

        await using var client = new AuthoritativeClient(Options(listener, TimeSpan.FromMinutes(1)));
        await client.ConnectAsync(timeout.Token);
        client.QueueRealtime(
            new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
            100);
        await client.PollAsync(timeout.Token);
        for (var attempt = 0; attempt < 300 && client.PendingCommandCount != 0; attempt++) {
            await Task.Delay(10, timeout.Token);
            await client.PollAsync(timeout.Token);
        }
        Assert.Equal(0, client.PendingCommandCount);
        await serverTask.WaitAsync(timeout.Token);
    }

    private static ClientConnectionOptions Options(TcpListener listener, TimeSpan snapshotPollInterval)
        => new(
            "127.0.0.1",
            ((IPEndPoint)listener.LocalEndpoint).Port,
            "clock-only-probe-test",
            "0.1.0",
            "content-hash") {
            SnapshotPollInterval = snapshotPollInterval,
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(25),
            MaximumRetryTimeout = TimeSpan.FromSeconds(2),
        };

    private static async Task DispatchHandshakeAsync(
        NetworkStream stream,
        bool supportsClockOnlyProbes,
        CancellationToken cancellationToken)
    {
        var hello = await ReadEnvelopeAsync(stream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ClientHello, hello.PayloadCase);
        await WriteEnvelopeAsync(stream, new Envelope {
            ServerHello = new ServerHello {
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                SessionToken = "clock-only-probe-session",
                TickRateHz = 20,
                GameMode = GameMode.SinglePlayer,
                CommandSchedulingMode = CommandSchedulingMode.ClockGated,
                SupportsClockOnlyProbes = supportsClockOnlyProbes,
            },
        }, cancellationToken);
        await WriteEnvelopeAsync(stream, new Envelope {
            Snapshot = new Snapshot { Tick = 100 },
        }, cancellationToken);
    }

    private static CommandAck Accepted(ulong clientSequence)
        => new() { Results = { new CommandResult {
            ClientSequence = clientSequence,
            Status = CommandStatus.Accepted,
        } } };

    private static async Task<Envelope> ReadEnvelopeAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return Envelope.Parser.ParseFrom(payload);
    }

    private static async Task WriteEnvelopeAsync(
        NetworkStream stream,
        Envelope envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.ToByteArray();
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
        }
    }
}
