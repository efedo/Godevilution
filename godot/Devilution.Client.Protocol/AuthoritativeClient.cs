using System.Collections.Concurrent;
using System.Diagnostics;
using System.Buffers.Binary;
using System.Net.Sockets;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;

namespace Devilution.Client.Protocol;

/**
 * TCP client for the frozen authoritative protocol. Gameplay commands are
 * retained until acknowledged and resent using an adaptive latency timeout.
 */
public sealed class AuthoritativeClient : IAsyncDisposable
{
    private readonly ClientConnectionOptions options;
    private readonly ConcurrentQueue<AuthoritativeClientMessage> incoming = new();
    private readonly Dictionary<ulong, PendingCommand> pending = [];
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly SemaphoreSlim pollLock = new(1, 1);
    private readonly object pendingLock = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly AuthoritativeCommandClock commandClock = new();
    private TcpClient? tcpClient;
    private NetworkStream? stream;
    private Task? receiveTask;
    private CancellationTokenSource? transportLifetime;
    private long transportGeneration;
    private int transportConnected;
    private ulong nextClientSequence;
    private long lastSnapshotRequestMs;
    private double roundTripMilliseconds = 100;
    private string effectiveContentManifestHash;
    private string effectiveRulesetIdentityHash;
    private TimingProbe? timingProbe;
    private ulong nextTimingRequestId;
    private long timingEpoch;
    private long? lastTimingProbeRequestMs;
    private bool timingCalibrationReady;
    private int timingCalibrationProbeAttempts;
    private bool timingCalibrationTimedOut;
    private CommandSchedulingMode commandSchedulingMode;
    // Probe expiry must not inherit a short command retry estimate: a delayed
    // but valid snapshot reply is the signal that raises the gated lead.
    private static readonly TimeSpan TimingProbeTimeout = TimeSpan.FromSeconds(2);
    // Readiness retries have their own bounded cadence; an idle snapshot poll
    // interval may intentionally be much longer than startup calibration.
    private static readonly TimeSpan TimingCalibrationRetryInterval = TimeSpan.FromMilliseconds(250);
    private const int MaximumCalibrationProbeAttempts = 3;

    public string SessionToken { get; private set; } = string.Empty;

    public uint ServerTickRateHz { get; private set; } = 20;

    public GameMode ServerGameMode { get; private set; } = GameMode.Unspecified;

    /** The negotiated scheduler; omitted or Unspecified values remain immediate. */
    public CommandSchedulingMode ServerCommandSchedulingMode => commandSchedulingMode;

    public bool IsClockGatedCommandScheduling
        => commandSchedulingMode == CommandSchedulingMode.ClockGated;

    /** The most recently accepted server identity; retained across reconnects. */
    public AuthoritativeServerIdentity? ServerIdentity { get; private set; }

    public ulong SuggestedCommandTick(ulong observedTick)
    {
        var leadMilliseconds = IsClockGatedCommandScheduling
            ? Volatile.Read(ref roundTripMilliseconds)
            : RetryTimeout.TotalMilliseconds;
        var leadTicks = Math.Max(1, (int)Math.Ceiling(
            leadMilliseconds / 1000 * ServerTickRateHz) + 1);
        return checked(commandClock.Estimate(observedTick, ServerTickRateHz) + (ulong)leadTicks);
    }

    public AuthoritativeClient(ClientConnectionOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        effectiveContentManifestHash = options.ContentManifestHash;
        effectiveRulesetIdentityHash = options.RulesetIdentityHash;
    }

    public bool IsConnected => Volatile.Read(ref transportConnected) == 1;

    public int PendingCommandCount
    {
        get
        {
            lock (pendingLock)
                return pending.Count;
        }
    }

    public TimeSpan RetryTimeout => TimeSpan.FromMilliseconds(Math.Clamp(
        Volatile.Read(ref roundTripMilliseconds) * 4,
        options.MinimumRetryTimeout.TotalMilliseconds,
        options.MaximumRetryTimeout.TotalMilliseconds));

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            throw new InvalidOperationException("The authoritative client is already connected.");
        if (!ClientConnectionOptions.TryValidateEndpoint(options.Host, options.Port, out var endpointError))
            throw new InvalidOperationException(endpointError);
        if (string.IsNullOrWhiteSpace(effectiveContentManifestHash))
            throw new InvalidOperationException("DEVILUTION_CONTENT_HASH must match the server manifest before connecting.");
        if (string.Equals(
                options.ProtocolSchemaVersion,
                ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
                StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(effectiveRulesetIdentityHash))
            throw new InvalidOperationException(
                "DEVILUTION_RULESET_HASH is required by this protocol version.");

        try {
            commandClock.Reset();
            lock (pendingLock) {
                timingProbe = null;
                timingEpoch = 0;
                lastTimingProbeRequestMs = null;
                timingCalibrationReady = false;
                timingCalibrationProbeAttempts = 0;
                timingCalibrationTimedOut = false;
            }
            commandSchedulingMode = CommandSchedulingMode.Unspecified;
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(options.Host, options.Port, cancellationToken);
            stream = tcpClient.GetStream();
            var requestedResumeToken = string.IsNullOrWhiteSpace(SessionToken)
                ? options.ResumeToken
                : SessionToken;
            var clientHello = new ClientHello {
                ClientBuildId = options.ClientBuildId,
                ProtocolSchemaVersion = options.ProtocolSchemaVersion,
                ContentManifestHash = effectiveContentManifestHash,
                RulesetIdentityHash = effectiveRulesetIdentityHash,
                ResumeToken = requestedResumeToken,
                SupportsClockGatedCommands = true,
            };
            if (options.RequestedCharacterClassId.HasValue) {
                clientHello.RequestedCharacterClassId
                    = options.RequestedCharacterClassId.Value;
            }
            if (!string.IsNullOrEmpty(options.RequestedCharacterName))
                clientHello.RequestedCharacterName = options.RequestedCharacterName;
            await WriteEnvelopeAsync(new Envelope { ClientHello = clientHello }, cancellationToken);

            var hello = await ReadEnvelopeAsync(stream, cancellationToken)
                ?? throw new EndOfStreamException("The server closed the connection during handshake.");
            if (hello.PayloadCase == Envelope.PayloadOneofCase.Error)
                throw new InvalidDataException(hello.Error.Detail);
            if (hello.PayloadCase != Envelope.PayloadOneofCase.ServerHello)
                throw new InvalidDataException("The first server response must be ServerHello.");
            var identityRefreshForResumedSession = !string.IsNullOrWhiteSpace(requestedResumeToken)
                && hello.ServerHello.SessionToken == requestedResumeToken;
            if (hello.ServerHello.ProtocolSchemaVersion != options.ProtocolSchemaVersion
                || (!identityRefreshForResumedSession
                    && hello.ServerHello.ContentManifestHash != effectiveContentManifestHash)
                || (!identityRefreshForResumedSession
                    && !string.IsNullOrWhiteSpace(effectiveRulesetIdentityHash)
                    && hello.ServerHello.RulesetIdentityHash != effectiveRulesetIdentityHash)
                || hello.ServerHello.GameMode is not (GameMode.SinglePlayer or GameMode.Multiplayer))
                throw new InvalidDataException("The server returned an incompatible protocol, content, or ruleset identity.");

            if (hello.ServerHello.CommandSchedulingMode is not (
                    CommandSchedulingMode.Unspecified
                    or CommandSchedulingMode.Immediate
                    or CommandSchedulingMode.ClockGated))
                throw new InvalidDataException("The server returned an unknown command scheduling mode.");

            SessionToken = hello.ServerHello.SessionToken;
            ServerTickRateHz = hello.ServerHello.TickRateHz == 0 ? 20 : hello.ServerHello.TickRateHz;
            ServerGameMode = hello.ServerHello.GameMode;
            commandSchedulingMode = hello.ServerHello.CommandSchedulingMode;
            effectiveContentManifestHash = hello.ServerHello.ContentManifestHash;
            effectiveRulesetIdentityHash = hello.ServerHello.RulesetIdentityHash;
            ServerIdentity = new AuthoritativeServerIdentity(
                hello.ServerHello.ServerBuildId,
                hello.ServerHello.ProtocolSchemaVersion,
                hello.ServerHello.ContentManifestHash,
                hello.ServerHello.RulesetIdentityHash,
                ServerTickRateHz,
                ServerGameMode);

            var initialSnapshot = await ReadEnvelopeAsync(stream, cancellationToken)
                ?? throw new EndOfStreamException("The server closed the connection before the initial snapshot.");
            if (initialSnapshot.PayloadCase == Envelope.PayloadOneofCase.Error)
                throw new InvalidDataException(initialSnapshot.Error.Detail);
            if (initialSnapshot.PayloadCase != Envelope.PayloadOneofCase.Snapshot)
                throw new InvalidDataException("The second server response must be an authoritative snapshot.");
            ValidateInitialSnapshotIdentity(initialSnapshot.Snapshot);
            // Do not renumber pending retries. Only new intents advance beyond
            // the authoritative ledger restored by a fresh host/client process.
            lock (pendingLock)
                nextClientSequence = Math.Max(nextClientSequence, hello.ServerHello.LastClientSequence);
            Enqueue(initialSnapshot);
            var connectedStream = stream;
            var connectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            var connectionGeneration = Interlocked.Increment(ref transportGeneration);
            lock (pendingLock) {
                timingEpoch = connectionGeneration;
                timingCalibrationReady = commandSchedulingMode != CommandSchedulingMode.ClockGated;
                lastTimingProbeRequestMs = null;
                timingCalibrationProbeAttempts = 0;
                timingCalibrationTimedOut = false;
            }
            transportLifetime = connectionLifetime;
            Volatile.Write(ref transportConnected, 1);
            receiveTask = Task.Run(
                () => ReceiveLoopAsync(connectedStream, connectionLifetime.Token, connectionGeneration),
                CancellationToken.None);
        } catch {
            await CloseTransportAsync();
            throw;
        }
    }

    /**
     * Reconnects this client instance using its existing resume token and
     * client-sequence ledger. Pending commands are made immediately eligible
     * for retry after the new authoritative snapshot arrives.
     */
    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        if (lifetime.IsCancellationRequested)
            throw new ObjectDisposedException(nameof(AuthoritativeClient));

        await CloseTransportAsync();
        // Let an old poll finish against the closed transport before publishing
        // a replacement stream or writing its handshake.
        await pollLock.WaitAsync(cancellationToken);
        try {
            DiscardClosedTransportNotifications();
            lock (pendingLock) {
                foreach (var pendingCommand in pending.Values)
                {
                    pendingCommand.LastSentMs = null;
                    pendingCommand.RetryNotBeforeMs = null;
                }
                timingProbe = null;
                timingCalibrationReady = false;
                lastTimingProbeRequestMs = null;
                timingCalibrationProbeAttempts = 0;
                timingCalibrationTimedOut = false;
            }
            lastSnapshotRequestMs = 0;
            await ConnectAsync(cancellationToken);
        } finally {
            pollLock.Release();
        }
    }

    /** Removes the previous transport's transient close marker before a new epoch. */
    private void DiscardClosedTransportNotifications()
    {
        var retained = new List<AuthoritativeClientMessage>();
        while (incoming.TryDequeue(out var message)) {
            if (message.Error?.Code == ProtocolErrorCode.NotAuthenticated
                && message.Error.Detail.StartsWith(
                    "Authoritative connection closed:",
                    StringComparison.Ordinal))
                continue;
            retained.Add(message);
        }
        foreach (var message in retained)
            incoming.Enqueue(message);
    }

    public ulong Queue(Command command, ulong requestedTick)
        => Queue(command, requestedTick, observedTick: null);

    /** Schedules new interactive input at first dispatch; transmitted retries never change ticks. */
    public ulong QueueRealtime(Command command, ulong observedTick)
        => Queue(command, requestedTick: 0, observedTick);

    private ulong Queue(Command command, ulong requestedTick, ulong? observedTick)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (pendingLock) {
            var sequence = checked(++nextClientSequence);
            command.ClientSequence = sequence;
            command.RequestedTick = requestedTick;
            pending.Add(sequence, new PendingCommand(command.Clone(), observedTick));
            return sequence;
        }
    }

    /** Sends new commands and resubmits commands whose adaptive timeout elapsed. */
    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        await pollLock.WaitAsync(cancellationToken);
        try {
            if (!IsConnected || stream is null)
                return;

            var now = clock.ElapsedMilliseconds;
            var normalRetryMilliseconds = RetryTimeout.TotalMilliseconds;
            PendingCommand[] dueCommands;
            bool needsTimingCalibration;
            bool timingCalibrationTimedOut;
            lock (pendingLock) {
                ExpireTimingProbeLocked(now);
                needsTimingCalibration = IsClockGatedCommandScheduling
                    && !timingCalibrationReady
                    && pending.Values.Any(command =>
                        !command.TransmissionStarted && command.ObservedTick.HasValue);
                timingCalibrationTimedOut = this.timingCalibrationTimedOut;
                dueCommands = pending.Values
                    .Where(command => IsCommandDue(command, now, normalRetryMilliseconds)
                        && (!needsTimingCalibration
                            || command.TransmissionStarted
                            || !command.ObservedTick.HasValue))
                    .ToArray();
            }

            if (timingCalibrationTimedOut)
                await FailTimingCalibrationAsync(cancellationToken);

            if (needsTimingCalibration)
                await TrySendTimingProbeAsync(
                    forceCalibration: true,
                    cancellationToken: cancellationToken);

            var attemptedCommandDispatch = dueCommands.Length != 0;
            foreach (var entry in dueCommands) {
                try {
                    await WriteEnvelopeAsync(() => {
                        lock (pendingLock) {
                            if (!pending.ContainsKey(entry.Command.ClientSequence))
                                return null;
                            if (!entry.TransmissionStarted) {
                                if (entry.ObservedTick is { } observedTick)
                                    entry.Command.RequestedTick = SuggestedCommandTick(observedTick);
                            }
                            entry.Retransmitted |= entry.TransmissionStarted;
                            // Freeze before any write: cancellation/partial writes may
                            // leave the server with this exact command identity.
                            entry.TransmissionStarted = true;
                            entry.LastSentMs = clock.ElapsedMilliseconds;
                            if (IsClockGatedCommandScheduling)
                                entry.RetryNotBeforeMs = ComputeRetryNotBeforeMs(
                                    entry.Command.RequestedTick,
                                    entry.LastSentMs.Value);
                            return new Envelope { CommandBatch = new CommandBatch { Commands = { entry.Command } } };
                        }
                    }, cancellationToken);
                } catch {
                    lock (pendingLock) {
                        if (pending.TryGetValue(entry.Command.ClientSequence, out var current) && ReferenceEquals(current, entry))
                        {
                            entry.LastSentMs = null;
                            entry.RetryNotBeforeMs = null;
                        }
                    }
                    throw;
                }
            }

            var hasPendingCommands = false;
            bool forceTimingProbe;
            lock (pendingLock) {
                var probeNow = clock.ElapsedMilliseconds;
                ExpireTimingProbeLocked(probeNow);
                hasPendingCommands = pending.Count > 0;
                forceTimingProbe = IsClockGatedCommandScheduling
                    && !timingCalibrationReady
                    && pending.Values.Any(command =>
                        !command.TransmissionStarted && command.ObservedTick.HasValue);
            }
            if (timingCalibrationTimedOut || this.timingCalibrationTimedOut)
                await FailTimingCalibrationAsync(cancellationToken);
            now = clock.ElapsedMilliseconds;
            if ((!forceTimingProbe
                    && !hasPendingCommands
                    && now - lastSnapshotRequestMs >= options.SnapshotPollInterval.TotalMilliseconds
                    && (!IsClockGatedCommandScheduling || !attemptedCommandDispatch))
                || forceTimingProbe) {
                if (!IsClockGatedCommandScheduling) {
                    await WriteEnvelopeAsync(
                        new Envelope { SnapshotRequest = new Envelope.Types.SnapshotRequest() },
                        cancellationToken);
                    lastSnapshotRequestMs = clock.ElapsedMilliseconds;
                    return;
                }
                await TrySendTimingProbeAsync(forceTimingProbe, cancellationToken);
            }
        } finally {
            pollLock.Release();
        }
    }

    private async Task<bool> TrySendTimingProbeAsync(
        bool forceCalibration,
        CancellationToken cancellationToken)
    {
        if (!IsClockGatedCommandScheduling)
            return false;

        var timingRequestId = 0UL;
        var probeRegistered = false;
        try {
            await WriteEnvelopeAsync(() => {
                lock (pendingLock) {
                    var dispatchNow = clock.ElapsedMilliseconds;
                    var requiresCalibration = forceCalibration
                        && !timingCalibrationReady
                        && pending.Values.Any(command =>
                            !command.TransmissionStarted && command.ObservedTick.HasValue);
                    var probeIntervalElapsed = !lastTimingProbeRequestMs.HasValue
                        || dispatchNow - lastTimingProbeRequestMs.Value
                            >= (requiresCalibration
                                ? TimingCalibrationRetryInterval.TotalMilliseconds
                                : options.SnapshotPollInterval.TotalMilliseconds);
                    if (timingProbe is not null
                        || (requiresCalibration
                            ? !probeIntervalElapsed
                            : forceCalibration
                                || pending.Count > 0
                                || dispatchNow - lastSnapshotRequestMs
                                    < options.SnapshotPollInterval.TotalMilliseconds))
                        return null;

                    timingRequestId = NextTimingRequestId();
                    timingProbe = new TimingProbe(
                        timingRequestId,
                        timingEpoch,
                        requiresCalibration,
                        dispatchNow,
                        SaturatingAdd(dispatchNow, TimingProbeTimeout.TotalMilliseconds));
                    lastTimingProbeRequestMs = dispatchNow;
                    probeRegistered = true;
                    return new Envelope {
                        SnapshotRequest = new Envelope.Types.SnapshotRequest {
                            TimingRequestId = timingRequestId,
                        },
                    };
                }
            }, cancellationToken);
            if (probeRegistered)
                lastSnapshotRequestMs = clock.ElapsedMilliseconds;
            return probeRegistered;
        } catch {
            if (probeRegistered) {
                lock (pendingLock) {
                    if (timingProbe?.RequestId == timingRequestId)
                        timingProbe = null;
                }
            }
            throw;
        }
    }

    private async Task FailTimingCalibrationAsync(CancellationToken cancellationToken)
    {
        const string detail = "Clock-gated timing calibration did not complete after the bounded probe deadline; the transport was closed without sending uncalibrated realtime commands.";
        await CloseTransportAsync();
        EnqueueConnectionClosed(detail);
        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException(detail);
    }

    public IReadOnlyList<AuthoritativeClientMessage> DrainMessages()
    {
        var messages = new List<AuthoritativeClientMessage>();
        while (incoming.TryDequeue(out var message))
            messages.Add(message);
        return messages;
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        await CloseTransportAsync();
        lifetime.Dispose();
        // In-flight sends may still unwind and release these managed gates.
        // No wait handle is allocated; let GC reclaim them after their users.
    }

    private async Task CloseTransportAsync()
    {
        Interlocked.Increment(ref transportGeneration);
        Volatile.Write(ref transportConnected, 0);
        transportLifetime?.Cancel();
        stream?.Dispose();
        tcpClient?.Dispose();
        stream = null;
        tcpClient = null;
        if (receiveTask is not null) {
            try {
                await receiveTask;
            } catch (Exception) {
            }
            receiveTask = null;
        }
        transportLifetime?.Dispose();
        transportLifetime = null;
    }

    private async Task ReceiveLoopAsync(
        NetworkStream networkStream,
        CancellationToken cancellationToken,
        long connectionGeneration)
    {
        try {
            while (!cancellationToken.IsCancellationRequested) {
                var envelope = await ReadEnvelopeAsync(networkStream, cancellationToken);
                if (envelope is null)
                    break;
                Enqueue(envelope, connectionGeneration);
            }
        } catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && (exception is IOException or SocketException or EndOfStreamException or ObjectDisposedException)) {
            EnqueueConnectionClosed(exception.Message);
        } finally {
            if (Volatile.Read(ref transportGeneration) == connectionGeneration)
                Volatile.Write(ref transportConnected, 0);
        }
    }

    private void EnqueueConnectionClosed(string detail = "The authoritative connection closed.")
    {
        incoming.Enqueue(new AuthoritativeClientMessage(Error: new ProtocolError {
            Code = ProtocolErrorCode.NotAuthenticated,
            Detail = $"Authoritative connection closed: {detail}",
        }));
    }

    private void Enqueue(Envelope envelope, long? connectionGeneration = null)
    {
        if (envelope.PayloadCase == Envelope.PayloadOneofCase.TimingReply) {
            if (connectionGeneration is { } epoch)
                ObserveTimingReply(envelope.TimingReply, epoch);
            return;
        }
        if (envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot
            && !ObserveSnapshotIdentity(envelope.Snapshot))
            return;
        if (envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot) {
            // Model admission happens later; delayed snapshots must not rewind
            // the clock-gated first-send estimate before that admission.
            if (IsClockGatedCommandScheduling)
                commandClock.ObserveMonotonic(envelope.Snapshot.Tick, ServerTickRateHz);
            else
                commandClock.Observe(envelope.Snapshot.Tick);
        }
        if (envelope.PayloadCase == Envelope.PayloadOneofCase.CommandAck) {
            try {
                AuthoritativeCommandAckValidator.Validate(envelope.CommandAck);
            } catch (InvalidDataException exception) {
                incoming.Enqueue(new AuthoritativeClientMessage(Error: new ProtocolError {
                    Code = ProtocolErrorCode.InvalidMessage,
                    Detail = exception.Message,
                }));
                return;
            }
            Acknowledge(envelope.CommandAck);
        }
        incoming.Enqueue(envelope.PayloadCase switch {
            Envelope.PayloadOneofCase.Snapshot => new AuthoritativeClientMessage(Snapshot: envelope.Snapshot),
            Envelope.PayloadOneofCase.EventBatch => new AuthoritativeClientMessage(Events: envelope.EventBatch),
            Envelope.PayloadOneofCase.CommandAck => new AuthoritativeClientMessage(Acknowledgement: envelope.CommandAck),
            Envelope.PayloadOneofCase.Error => new AuthoritativeClientMessage(Error: envelope.Error),
            _ => new AuthoritativeClientMessage(Error: new ProtocolError {
                Code = ProtocolErrorCode.InvalidMessage,
                Detail = $"Unexpected server payload: {envelope.PayloadCase}",
            }),
        });
    }

    private bool ObserveSnapshotIdentity(Snapshot snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.ContentManifestHash)
            && string.IsNullOrEmpty(snapshot.RulesetIdentityHash))
            return true;
        if (string.IsNullOrEmpty(snapshot.ContentManifestHash)
            || string.IsNullOrEmpty(snapshot.RulesetIdentityHash)) {
            incoming.Enqueue(new AuthoritativeClientMessage(Error: new ProtocolError {
                Code = ProtocolErrorCode.InvalidMessage,
                Detail = "The authoritative snapshot contains an incomplete ruleset identity.",
            }));
            return false;
        }
        if (ServerIdentity is { } identity)
            ServerIdentity = identity with {
                ContentManifestHash = snapshot.ContentManifestHash,
                RulesetIdentityHash = snapshot.RulesetIdentityHash,
            };
        effectiveContentManifestHash = snapshot.ContentManifestHash;
        effectiveRulesetIdentityHash = snapshot.RulesetIdentityHash;
        return true;
    }

    /**
     * The first snapshot may reflect a ruleset generation published between
     * the hello and the projection, so a complete identity can intentionally
     * replace the hello identity. A partial identity is never actionable, and
     * older protocol snapshots may omit both additive fields entirely.
     */
    private void ValidateInitialSnapshotIdentity(Snapshot snapshot)
    {
        var hasContentIdentity = !string.IsNullOrEmpty(snapshot.ContentManifestHash);
        var hasRulesetIdentity = !string.IsNullOrEmpty(snapshot.RulesetIdentityHash);
        if (!hasContentIdentity && !hasRulesetIdentity)
            return;
        if (!hasContentIdentity || !hasRulesetIdentity)
            throw new InvalidDataException(
                "The initial authoritative snapshot contains an incomplete ruleset identity.");
    }

    private bool IsCommandDue(PendingCommand command, long now, double retryMilliseconds)
    {
        if (command.LastSentMs is null)
            return true;
        if (now - command.LastSentMs.Value < retryMilliseconds)
            return false;
        return !IsClockGatedCommandScheduling
            || command.RetryNotBeforeMs is null
            || now >= command.RetryNotBeforeMs.Value;
    }

    private long ComputeRetryNotBeforeMs(ulong requestedTick, long sentAtMs)
    {
        if (!IsClockGatedCommandScheduling)
            return sentAtMs;

        var estimatedCurrentTick = commandClock.Estimate(0, ServerTickRateHz);
        var remainingTicks = requestedTick > estimatedCurrentTick
            ? requestedTick - estimatedCurrentTick
            : 0;
        var remainingMilliseconds = remainingTicks == 0
            ? 0
            : Math.Ceiling(remainingTicks * 1000d / ServerTickRateHz);
        return SaturatingAdd(
            sentAtMs,
            remainingMilliseconds + RetryTimeout.TotalMilliseconds);
    }

    private void ObserveTimingReply(TimingReply reply, long connectionGeneration)
    {
        if (!IsClockGatedCommandScheduling || reply.RequestId == 0)
            return;

        var now = clock.ElapsedMilliseconds;
        lock (pendingLock) {
            if (timingProbe is not { } probe
                || probe.RequestId != reply.RequestId
                || probe.Epoch != connectionGeneration
                || timingEpoch != connectionGeneration
                || now >= probe.ExpiresAtMs)
                return;
            timingProbe = null;
            timingCalibrationReady = true;
            timingCalibrationProbeAttempts = 0;
            lastTimingProbeRequestMs = null;
            var sample = Math.Max(0, now - probe.SentAtMs);
            var prior = Volatile.Read(ref roundTripMilliseconds);
            // Gated admission waits on the full measured round trip. Keep the
            // estimate conservative so a later short probe cannot lower the
            // lead below an earlier valid sample.
            Volatile.Write(ref roundTripMilliseconds, Math.Max(prior, sample));
        }
    }

    private void ExpireTimingProbeLocked(long now)
    {
        if (timingProbe is not { } probe || now < probe.ExpiresAtMs)
            return;

        timingProbe = null;
        if (probe.RequiresCalibration
            && ++timingCalibrationProbeAttempts >= MaximumCalibrationProbeAttempts) {
            timingCalibrationTimedOut = pending.Values.Any(command =>
                !command.TransmissionStarted && command.ObservedTick.HasValue);
        }
    }

    private ulong NextTimingRequestId()
    {
        if (nextTimingRequestId == ulong.MaxValue)
            throw new InvalidOperationException("Timing request ID space is exhausted.");
        return ++nextTimingRequestId;
    }

    private static long SaturatingAdd(long origin, double milliseconds)
    {
        if (milliseconds <= 0)
            return origin;
        if (milliseconds >= long.MaxValue - origin)
            return long.MaxValue;
        return origin + (long)Math.Ceiling(milliseconds);
    }

    private void Acknowledge(CommandAck acknowledgement)
    {
        var now = clock.ElapsedMilliseconds;
        foreach (var result in acknowledgement.Results) {
            lock (pendingLock) {
                if (!pending.Remove(result.ClientSequence, out var command) || command.LastSentMs is null || command.Retransmitted)
                    continue;
                if (!IsClockGatedCommandScheduling) {
                    // An ACK after retransmission cannot identify which send it times.
                    Volatile.Write(ref roundTripMilliseconds,
                        (roundTripMilliseconds * 0.75) + ((now - command.LastSentMs.Value) * 0.25));
                }
            }
        }
    }

    private Task WriteEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken)
        => WriteEnvelopeAsync(() => envelope, cancellationToken);

    private async Task WriteEnvelopeAsync(Func<Envelope?> createEnvelope, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try {
            var envelope = createEnvelope();
            if (envelope is null)
                return;
            var payload = envelope.ToByteArray();
            if (payload.Length == 0 || payload.Length > AuthoritativeProtocolLimits.MaximumEnvelopeBytes)
                throw new InvalidDataException("The authoritative envelope is outside the allowed size range.");
            var header = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
            var currentStream = stream ?? throw new IOException("The authoritative transport is closed.");
            await currentStream.WriteAsync(header, cancellationToken);
            await currentStream.WriteAsync(payload, cancellationToken);
            await currentStream.FlushAsync(cancellationToken);
        } finally {
            writeLock.Release();
        }
    }

    private static async Task<Envelope?> ReadEnvelopeAsync(NetworkStream networkStream, CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        if (!await ReadExactlyAsync(networkStream, header, cancellationToken))
            return null;
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > AuthoritativeProtocolLimits.MaximumEnvelopeBytes)
            throw new InvalidDataException("The authoritative envelope length is outside the allowed range.");
        var payload = new byte[length];
        if (!await ReadExactlyAsync(networkStream, payload, cancellationToken))
            throw new EndOfStreamException("The authoritative envelope payload was truncated.");
        return Envelope.Parser.ParseFrom(payload);
    }

    private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
                return false;
            offset += read;
        }
        return true;
    }

    private sealed class PendingCommand(Command command, ulong? observedTick)
    {
        public Command Command { get; } = command;
        public ulong? ObservedTick { get; } = observedTick;
        public bool TransmissionStarted { get; set; }
        public bool Retransmitted { get; set; }
        public long? LastSentMs { get; set; }
        public long? RetryNotBeforeMs { get; set; }
    }

    private sealed record TimingProbe(
        ulong RequestId,
        long Epoch,
        bool RequiresCalibration,
        long SentAtMs,
        long ExpiresAtMs);
}
