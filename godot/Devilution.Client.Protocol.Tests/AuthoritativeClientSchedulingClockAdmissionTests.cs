using System.Diagnostics;
using System.Net;
using Devilution.Client.Protocol;
using Devilution.Engine;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeClientSchedulingClockAdmissionTests
{
    [Fact]
    public async Task FirstRealtimeDispatchWaitsForOutstandingSnapshotAndRecomputesItsTick()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));

        var clock = new ControlledAuthoritativeClock();
        var adapter = new AdmissionTestAdapter(holdSecondSnapshot: true);
        var commandServer = new AuthoritativeCommandServer(adapter);
        var identityHash = new string('b', 64);
        var identity = new ProtocolServerIdentity(
            "first-dispatch-snapshot-gate-test",
            ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            identityHash,
            20) {
            RulesetIdentityHash = identityHash,
        };
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            new ProtocolHandshake(identity),
            clock,
            adapter,
            address: IPAddress.Loopback) {
            UseClockGatedCommandScheduling = true,
        };
        server.Start();

        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            server.Port,
            "first-dispatch-snapshot-gate-test",
            identity.ProtocolSchemaVersion,
            identity.ContentManifestHash,
            identity.RulesetIdentityHash) {
            SnapshotPollInterval = TimeSpan.Zero,
            MinimumRetryTimeout = TimeSpan.FromSeconds(1),
            MaximumRetryTimeout = TimeSpan.FromSeconds(2),
        }) {
            CommandTimingRecorder = recorder,
        };

        Task? connectTask = null;
        try {
            connectTask = client.ConnectAsync(timeout.Token);
            await adapter.InitialSnapshotEntered.Task.WaitAsync(timeout.Token);
            adapter.ReleaseInitialSnapshot();
            await connectTask;
            connectTask = null;

            var initial = Assert.Single(client.DrainMessages());
            var model = new AuthoritativeClientModel();
            model.Apply(initial);
            Assert.Equal(0UL, model.CurrentTick);

            // This idle poll sends a clock sample and starts a genuine full
            // snapshot request. The adapter captures the resulting tick, then
            // holds the actual snapshot response in flight.
            clock.Set(1000);
            var previousProbeReplies = recorder.CaptureReport().ProbeOutcomes.Count(outcome =>
                outcome.Outcome == AuthoritativeClientProbeOutcome.Reply);
            await client.PollAsync(timeout.Token);
            var heldSnapshot = await adapter.SecondSnapshotEntered.Task.WaitAsync(timeout.Token);
            Assert.Equal(1000UL, heldSnapshot.Tick);
            await WaitUntilAsync(
                () => recorder.CaptureReport().ProbeOutcomes.Any(outcome =>
                    outcome.Outcome == AuthoritativeClientProbeOutcome.Reply)
                    && recorder.CaptureReport().ProbeOutcomes.Count(outcome =>
                        outcome.Outcome == AuthoritativeClientProbeOutcome.Reply) > previousProbeReplies,
                timeout.Token,
                "the scheduling-clock sample preceding the held full snapshot");
            var sample = recorder.CaptureReport().ProbeOutcomes.Last(outcome =>
                outcome.Outcome == AuthoritativeClientProbeOutcome.Reply);

            var sequence = client.QueueRealtime(
                new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
                model.CurrentTick);
            await client.PollAsync(timeout.Token);

            // The full-snapshot gate is independent of clock calibration: the
            // command remains pending and unfrozen while the snapshot reply is
            // outstanding, even though a clock probe has already replied.
            Assert.Equal(1, client.PendingCommandCount);
            Assert.Empty(recorder.CaptureReport().Commands);
            Assert.Equal(0, adapter.ExecutedCommandCount);

            var staleSuggestion = client.SuggestedCommandTick(model.CurrentTick);
            await Task.Delay(TimeSpan.FromMilliseconds(750), timeout.Token);
            clock.Set(EstimatePhysicalTick(1000, identity.TickRateHz, sample.OutcomeStopwatchTimestamp));
            Assert.True(clock.CurrentTick >= heldSnapshot.Tick + 10);
            Assert.True(staleSuggestion < clock.CurrentTick);

            adapter.ReleaseSecondSnapshot();
            var refreshedSnapshot = await WaitForSnapshotAsync(client, model, timeout.Token);
            Assert.Equal(heldSnapshot.Tick, refreshedSnapshot.Tick);
            Assert.True(clock.CurrentTick > refreshedSnapshot.Tick);

            await client.PollAsync(timeout.Token);
            AuthoritativeClientCommandTiming? dispatch = null;
            await WaitUntilAsync(
                () => {
                    dispatch = recorder.CaptureReport().Commands.SingleOrDefault(command =>
                        command.ClientSequence == sequence
                        && command.DispatchKind == AuthoritativeClientCommandDispatchKind.FirstDispatch);
                    return dispatch is not null;
                },
                timeout.Token,
                "the recomputed first command dispatch after the full snapshot boundary");
            Assert.NotNull(dispatch);
            Assert.True(
                dispatch.RequestedTick >= clock.CurrentTick,
                $"The deferred first dispatch used stale RequestedTick {dispatch.RequestedTick} behind physical tick {clock.CurrentTick}.");
            await WaitUntilAsync(
                () => server.PendingCommandCount == 1,
                timeout.Token,
                "the host to queue the recomputed command");
            Assert.Equal(0, adapter.ExecutedCommandCount);

            clock.Set(dispatch.RequestedTick);
            var execution = await adapter.CommandExecuted.Task.WaitAsync(timeout.Token);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence, timeout.Token);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            Assert.Equal(CommandRejectReason.Unspecified, acknowledgement.RejectReason);
            Assert.Equal(dispatch.RequestedTick, acknowledgement.AppliedTick);
            Assert.Equal(dispatch.RequestedTick, execution.AppliedTick);
            Assert.Equal(sequence, execution.ClientSequence);
            Assert.Equal(1, adapter.ExecutedCommandCount);
            Assert.Equal(1000UL, model.CurrentTick);
        } finally {
            adapter.ReleaseInitialSnapshot();
            adapter.ReleaseSecondSnapshot();
            if (connectTask is not null) {
                try {
                    await connectTask.WaitAsync(TimeSpan.FromSeconds(2));
                } catch {
                    // Preserve the assertion or timeout that led to cleanup.
                }
            }
        }
    }

    [Fact]
    public async Task FirstRealtimeDispatchUsesBoundedFallbackWhenFullSnapshotNeverArrives()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));

        var clock = new ControlledAuthoritativeClock();
        var adapter = new AdmissionTestAdapter(holdSecondSnapshot: true);
        var commandServer = new AuthoritativeCommandServer(adapter);
        var identityHash = new string('c', 64);
        var identity = new ProtocolServerIdentity(
            "first-dispatch-snapshot-fallback-test",
            ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            identityHash,
            20) {
            RulesetIdentityHash = identityHash,
        };
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            new ProtocolHandshake(identity),
            clock,
            adapter,
            address: IPAddress.Loopback) {
            UseClockGatedCommandScheduling = true,
        };
        server.Start();

        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            server.Port,
            "first-dispatch-snapshot-fallback-test",
            identity.ProtocolSchemaVersion,
            identity.ContentManifestHash,
            identity.RulesetIdentityHash) {
            SnapshotPollInterval = TimeSpan.Zero,
            MinimumRetryTimeout = TimeSpan.FromSeconds(1),
            MaximumRetryTimeout = TimeSpan.FromSeconds(2),
        }) {
            CommandTimingRecorder = recorder,
        };

        Task? connectTask = null;
        try {
            connectTask = client.ConnectAsync(timeout.Token);
            await adapter.InitialSnapshotEntered.Task.WaitAsync(timeout.Token);
            adapter.ReleaseInitialSnapshot();
            await connectTask;
            connectTask = null;
            var initial = Assert.Single(client.DrainMessages());
            var model = new AuthoritativeClientModel();
            model.Apply(initial);

            clock.Set(100);
            var previousProbeReplies = recorder.CaptureReport().ProbeOutcomes.Count(outcome =>
                outcome.Outcome == AuthoritativeClientProbeOutcome.Reply);
            await client.PollAsync(timeout.Token);
            var heldSnapshot = await adapter.SecondSnapshotEntered.Task.WaitAsync(timeout.Token);
            Assert.Equal(100UL, heldSnapshot.Tick);
            await WaitUntilAsync(
                () => recorder.CaptureReport().ProbeOutcomes.Count(outcome =>
                    outcome.Outcome == AuthoritativeClientProbeOutcome.Reply) > previousProbeReplies,
                timeout.Token,
                "the clock sample before the withheld snapshot");
            var sample = recorder.CaptureReport().ProbeOutcomes.Last(outcome =>
                outcome.Outcome == AuthoritativeClientProbeOutcome.Reply);

            var sequence = client.QueueRealtime(
                new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
                model.CurrentTick);
            var gateStartedAt = Stopwatch.GetTimestamp();
            await client.PollAsync(timeout.Token);
            Assert.Equal(1, client.PendingCommandCount);
            Assert.Empty(recorder.CaptureReport().Commands);
            Assert.Equal(0, adapter.ExecutedCommandCount);
            await Task.Delay(TimeSpan.FromMilliseconds(750), timeout.Token);
            clock.Set(EstimatePhysicalTick(100, identity.TickRateHz, sample.OutcomeStopwatchTimestamp));
            await client.PollAsync(timeout.Token);
            Assert.Empty(recorder.CaptureReport().Commands);
            Assert.Equal(1, client.PendingCommandCount);

            // A response arriving within the two-second bound releases the
            // gate; if it never arrives, the existing bounded fallback must
            // still permit a first send without changing the command identity.
            await Task.Delay(TimeSpan.FromMilliseconds(1400), timeout.Token);
            clock.Set(EstimatePhysicalTick(100, identity.TickRateHz, sample.OutcomeStopwatchTimestamp));
            await client.PollAsync(timeout.Token);
            AuthoritativeClientCommandTiming? dispatch = null;
            await WaitUntilAsync(
                () => {
                    dispatch = recorder.CaptureReport().Commands.SingleOrDefault(command =>
                        command.ClientSequence == sequence
                        && command.DispatchKind == AuthoritativeClientCommandDispatchKind.FirstDispatch);
                    return dispatch is not null;
                },
                timeout.Token,
                "the first dispatch after the bounded full-snapshot fallback");
            Assert.NotNull(dispatch);
            Assert.True(Stopwatch.GetElapsedTime(gateStartedAt, dispatch.FreezeStopwatchTimestamp)
                >= TimeSpan.FromSeconds(2));
            Assert.Equal(1, client.PendingCommandCount);
            Assert.Equal(0, adapter.ExecutedCommandCount);

            adapter.ReleaseSecondSnapshot();
            var snapshot = await WaitForSnapshotAsync(client, model, timeout.Token);
            Assert.Equal(100UL, snapshot.Tick);
            await WaitUntilAsync(
                () => server.PendingCommandCount == 1,
                timeout.Token,
                "the host to queue the command sent after snapshot fallback");
            clock.Set(dispatch.RequestedTick);
            var execution = await adapter.CommandExecuted.Task.WaitAsync(timeout.Token);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence, timeout.Token);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            Assert.Equal(CommandRejectReason.Unspecified, acknowledgement.RejectReason);
            Assert.Equal(dispatch.RequestedTick, acknowledgement.AppliedTick);
            Assert.Equal(dispatch.RequestedTick, execution.AppliedTick);
            Assert.Equal(sequence, execution.ClientSequence);
            Assert.Equal(1, adapter.ExecutedCommandCount);
        } finally {
            adapter.ReleaseInitialSnapshot();
            adapter.ReleaseSecondSnapshot();
            if (connectTask is not null) {
                try {
                    await connectTask.WaitAsync(TimeSpan.FromSeconds(2));
                } catch {
                    // Preserve the assertion or timeout that led to cleanup.
                }
            }
        }
    }

    [Fact]
    public async Task DelayedSnapshotUsesClockProbeSampleForRealCommandAdmission()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));

        var clock = new ControlledAuthoritativeClock();
        var adapter = new AdmissionTestAdapter();
        var commandServer = new AuthoritativeCommandServer(adapter);
        var identityHash = new string('a', 64);
        var identity = new ProtocolServerIdentity(
            "scheduling-clock-admission-test",
            ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            identityHash,
            20) {
            RulesetIdentityHash = identityHash,
        };
        await using var server = new AuthoritativeTcpServer(
            commandServer,
            new ProtocolHandshake(identity),
            clock,
            adapter,
            address: IPAddress.Loopback) {
            UseClockGatedCommandScheduling = true,
        };
        server.Start();

        var recorder = new AuthoritativeClientCommandTimingRecorder();
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            server.Port,
            "scheduling-clock-admission-test",
            identity.ProtocolSchemaVersion,
            identity.ContentManifestHash,
            identity.RulesetIdentityHash) {
            SnapshotPollInterval = TimeSpan.FromHours(1),
            MinimumRetryTimeout = TimeSpan.FromSeconds(1),
            MaximumRetryTimeout = TimeSpan.FromSeconds(2),
        }) {
            CommandTimingRecorder = recorder,
        };

        Task? connectTask = null;
        try {
            connectTask = client.ConnectAsync(timeout.Token);
            var initialCapture = await adapter.InitialSnapshotEntered.Task.WaitAsync(timeout.Token);
            Assert.Equal(0UL, initialCapture.Tick);

            // The provider captured tick zero and is holding the genuine full
            // handshake snapshot. Advance the physical host clock before that
            // old snapshot is delivered to the client.
            clock.Set(1000);
            adapter.ReleaseInitialSnapshot();
            await connectTask;
            connectTask = null;

            var initialMessage = Assert.Single(client.DrainMessages());
            Assert.NotNull(initialMessage.Snapshot);
            var model = new AuthoritativeClientModel();
            model.Apply(initialMessage);
            Assert.Equal(0UL, model.CurrentTick);
            await adapter.AdvancedToPhysicalTick.Task.WaitAsync(timeout.Token);
            Assert.Equal(1000UL, clock.CurrentTick);

            var staleSnapshotSuggestion = client.SuggestedCommandTick(model.CurrentTick);
            Assert.True(
                staleSnapshotSuggestion < clock.CurrentTick,
                "The old snapshot alone should produce a command tick behind physical host time.");

            var sequence = client.QueueRealtime(
                new Command { MoveRequested = new MoveRequested { DirectionX = 1 } },
                model.CurrentTick);
            await client.PollAsync(timeout.Token);
            await WaitUntilAsync(
                () => recorder.CaptureReport().ProbeOutcomes.Any(outcome =>
                    outcome.Outcome == AuthoritativeClientProbeOutcome.Reply),
                timeout.Token,
                "the correlated physical-clock probe reply");

            Assert.Equal(0UL, model.CurrentTick);
            Assert.Equal(1, adapter.SnapshotCallCount);
            Assert.Empty(client.DrainMessages());
            var sampledCommandTick = client.SuggestedCommandTick(model.CurrentTick);
            Assert.True(
                sampledCommandTick >= clock.CurrentTick,
                "The negotiated clock sample must move scheduling to the physical host timeline.");
            var advancesAfterClockReached1000 = adapter.AdvanceCallCount;

            await client.PollAsync(timeout.Token);
            AuthoritativeClientCommandTiming? dispatch = null;
            await WaitUntilAsync(
                () => {
                    dispatch = recorder.CaptureReport().Commands.SingleOrDefault(command =>
                        command.ClientSequence == sequence
                        && command.DispatchKind == AuthoritativeClientCommandDispatchKind.FirstDispatch);
                    return dispatch is not null;
                },
                timeout.Token,
                "the ordinary command dispatch");
            Assert.NotNull(dispatch);
            Assert.True(dispatch.RequestedTick > clock.CurrentTick);
            await WaitUntilAsync(
                () => server.PendingCommandCount == 1,
                timeout.Token,
                "the host to queue the command at its actual admission boundary");
            Assert.Equal(advancesAfterClockReached1000, adapter.AdvanceCallCount);
            Assert.Equal(0, adapter.ExecutedCommandCount);

            // Advance the shared host clock exactly to the client's real
            // requested boundary; the host scheduler and command executor
            // then perform the normal admission path.
            clock.Set(dispatch.RequestedTick);
            var execution = await adapter.CommandExecuted.Task.WaitAsync(timeout.Token);
            var acknowledgement = await WaitForAcknowledgementAsync(
                client,
                sequence,
                timeout.Token);

            Assert.True(client.IsClockGatedCommandScheduling);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            Assert.Equal(CommandRejectReason.Unspecified, acknowledgement.RejectReason);
            Assert.Equal(dispatch.RequestedTick, acknowledgement.AppliedTick);
            Assert.Equal(dispatch.RequestedTick, execution.AppliedTick);
            Assert.Equal(sequence, execution.ClientSequence);
            Assert.Equal(1, adapter.ExecutedCommandCount);
            Assert.Equal(0UL, model.CurrentTick);
        } finally {
            adapter.ReleaseInitialSnapshot();
            if (connectTask is not null) {
                try {
                    await connectTask.WaitAsync(TimeSpan.FromSeconds(2));
                } catch {
                    // Preserve the assertion or timeout that led to cleanup.
                }
            }
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        CancellationToken cancellationToken,
        string description)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (!condition()) {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"Timed out waiting for {description}.");
            await Task.Delay(2, cancellationToken);
        }
    }

    private static async Task<CommandResult> WaitForAcknowledgementAsync(
        AuthoritativeClient client,
        ulong sequence,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTime.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var message in client.DrainMessages()) {
                if (message.Acknowledgement is { } acknowledgement) {
                    var result = acknowledgement.Results.SingleOrDefault(value =>
                        value.ClientSequence == sequence);
                    if (result is not null)
                        return result;
                }
                if (message.Error is { } error)
                    throw new InvalidDataException(error.Detail);
            }
            await Task.Delay(2, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for the admitted command acknowledgement.");
    }

    private static async Task<Snapshot> WaitForSnapshotAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTime.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var message in client.DrainMessages()) {
                if (message.Error is { } error)
                    throw new InvalidDataException(error.Detail);
                if (message.Snapshot is { } snapshot) {
                    model.Apply(message);
                    return snapshot;
                }
                model.Apply(message);
            }
            await Task.Delay(2, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for the held full snapshot response.");
    }

    private static ulong EstimatePhysicalTick(ulong sampleTick, uint tickRateHz, long sampleTimestamp)
    {
        var elapsedSeconds = Stopwatch.GetElapsedTime(sampleTimestamp).TotalSeconds;
        var elapsedTicks = checked((ulong)Math.Ceiling(elapsedSeconds * tickRateHz));
        return checked(sampleTick + elapsedTicks);
    }

    private sealed class ControlledAuthoritativeClock : IAuthoritativeClock
    {
        private long currentTick;

        public ulong CurrentTick => checked((ulong)Volatile.Read(ref currentTick));

        public void Set(ulong tick)
            => Interlocked.Exchange(ref currentTick, checked((long)tick));
    }

    private sealed class AdmissionTestAdapter : IAuthoritativeGameHostAdapter
    {
        private readonly ManualResetEventSlim releaseInitialSnapshot = new();
        private readonly ManualResetEventSlim releaseSecondSnapshot = new();
        private readonly bool holdSecondSnapshot;
        private int snapshotCallCount;
        private int advanceCallCount;
        private int executedCommandCount;

        public AdmissionTestAdapter(bool holdSecondSnapshot = false)
            => this.holdSecondSnapshot = holdSecondSnapshot;

        public TaskCompletionSource<(ulong Tick, uint EntityId)> InitialSnapshotEntered { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<(ulong Tick, uint EntityId)> SecondSnapshotEntered { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AdvancedToPhysicalTick { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<(ulong ClientSequence, ulong AppliedTick)> CommandExecuted { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int SnapshotCallCount => Volatile.Read(ref snapshotCallCount);

        public int AdvanceCallCount => Volatile.Read(ref advanceCallCount);

        public int ExecutedCommandCount => Volatile.Read(ref executedCommandCount);

        public void ReleaseInitialSnapshot() => releaseInitialSnapshot.Set();

        public void ReleaseSecondSnapshot() => releaseSecondSnapshot.Set();

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            Interlocked.Increment(ref executedCommandCount);
            CommandExecuted.TrySetResult((command.ClientSequence, appliedTick));
            return CommandExecutionResult.Accepted;
        }

        public void AdvanceTo(ulong tick)
        {
            Interlocked.Increment(ref advanceCallCount);
            if (tick >= 1000)
                AdvancedToPhysicalTick.TrySetResult();
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            var snapshotCall = Interlocked.Increment(ref snapshotCallCount);
            if (snapshotCall == 1) {
                InitialSnapshotEntered.TrySetResult((tick, entityId));
                if (!releaseInitialSnapshot.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("The test did not release the initial snapshot capture.");
            } else if (snapshotCall == 2 && holdSecondSnapshot) {
                SecondSnapshotEntered.TrySetResult((tick, entityId));
                if (!releaseSecondSnapshot.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("The test did not release the held full snapshot response.");
            }

            return new Snapshot {
                Tick = tick,
                Players = { new PlayerSnapshot { EntityId = entityId, LevelId = 0 } },
            };
        }

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick) => null;

        public string ExportPlayerSave(string sessionId) => "{}";

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
        }

        public void InitializeParticipant(string sessionId, AuthoritativeParticipantSelection selection)
        {
        }
    }
}
