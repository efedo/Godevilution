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
    private readonly object initialConnectionObservationLock = new();
    private long initialConnectionAttemptEpoch;
    private AuthoritativeInitialConnectionObservation? initialConnectionObservation;
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
    private long lastSnapshotReceiptMs;
    private long lastSnapshotReceiptGeneration;
    private long? negotiatedSnapshotRequestSentMs;
    private long negotiatedSnapshotRequestGeneration;
    private long nextNegotiatedSnapshotRequestToken;
    private long? negotiatedSnapshotRequestToken;
    private long negotiatedPostAckSnapshotGeneration;
    private ulong? negotiatedPostAckSnapshotAppliedTick;
    private long? negotiatedPostAckSnapshotObservedMs;
    private double roundTripMilliseconds = 100;
    private string effectiveContentManifestHash;
    private string effectiveRulesetIdentityHash;
    private TimingProbe? timingProbe;
    private ulong nextTimingRequestId;
    private long timingEpoch;
    private long? lastTimingProbeRequestMs;
    private long? lastClockOnlyProbeSentMs;
    private bool supportsClockOnlyProbes;
    private bool supportsTargetCellPathing;
    private bool supportsQueuedRangedAttacks;
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

    /** Credential-free handshake and first decoded wire snapshot for the current attempt. */
    public AuthoritativeInitialConnectionObservation? InitialConnectionObservation
    {
        get
        {
            lock (initialConnectionObservationLock)
                return initialConnectionObservation;
        }
    }

    /** Optional bounded, read-only diagnostic capture; null keeps tracing inert. */
    public AuthoritativeClientCommandTimingRecorder? CommandTimingRecorder { get; init; }

    public uint ServerTickRateHz { get; private set; } = 20;

    public GameMode ServerGameMode { get; private set; } = GameMode.Unspecified;

    /** The negotiated scheduler; omitted or Unspecified values remain immediate. */
    public CommandSchedulingMode ServerCommandSchedulingMode => commandSchedulingMode;

    public bool IsClockGatedCommandScheduling
        => commandSchedulingMode == CommandSchedulingMode.ClockGated;

    /** Native target-cell continuation is opt-in per negotiated server connection. */
    public bool SupportsTargetCellPathing
    {
        get {
            lock (pendingLock)
                return supportsTargetCellPathing;
        }
    }

    /** Native ranged attacks may be buffered through one active move on this connection. */
    public bool SupportsQueuedRangedAttacks
    {
        get {
            lock (pendingLock)
                return supportsQueuedRangedAttacks;
        }
    }

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
        var observationEpoch = BeginInitialConnectionObservation();
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
                lastClockOnlyProbeSentMs = null;
                negotiatedSnapshotRequestSentMs = null;
                negotiatedSnapshotRequestGeneration = 0;
                negotiatedSnapshotRequestToken = null;
                negotiatedPostAckSnapshotGeneration = 0;
                negotiatedPostAckSnapshotAppliedTick = null;
                negotiatedPostAckSnapshotObservedMs = null;
                timingCalibrationReady = false;
                timingCalibrationProbeAttempts = 0;
                timingCalibrationTimedOut = false;
                supportsTargetCellPathing = false;
                supportsQueuedRangedAttacks = false;
            }
            commandSchedulingMode = CommandSchedulingMode.Unspecified;
            supportsClockOnlyProbes = false;
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
            CaptureInitialServerHello(observationEpoch, hello.ServerHello, identityRefreshForResumedSession);
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

            var peerSupportsTargetCellPathing = hello.ServerHello.SupportsTargetCellPathing;
            var peerSupportsQueuedRangedAttacks = hello.ServerHello.SupportsQueuedRangedAttacks;
            lock (pendingLock) {
                if (!peerSupportsTargetCellPathing
                    && pending.Values.Any(command =>
                        command.Command.IntentCase == Command.IntentOneofCase.MoveRequested
                        && command.Command.MoveRequested.FollowPath))
                    throw new InvalidDataException(
                        "The resumed server does not support target-cell pathing required by a pending command.");
                if (!peerSupportsQueuedRangedAttacks
                    && pending.Values.Any(command =>
                        command.Command.IntentCase == Command.IntentOneofCase.AttackRequested
                        && command.Command.AttackRequested.QueueDuringMove))
                    throw new InvalidDataException(
                        "The resumed server does not support queued ranged attacks required by a pending command.");
                supportsTargetCellPathing = peerSupportsTargetCellPathing;
                supportsQueuedRangedAttacks = peerSupportsQueuedRangedAttacks;
            }

            SessionToken = hello.ServerHello.SessionToken;
            ServerTickRateHz = hello.ServerHello.TickRateHz == 0 ? 20 : hello.ServerHello.TickRateHz;
            ServerGameMode = hello.ServerHello.GameMode;
            commandSchedulingMode = hello.ServerHello.CommandSchedulingMode;
            supportsClockOnlyProbes = commandSchedulingMode == CommandSchedulingMode.ClockGated
                && hello.ServerHello.SupportsClockOnlyProbes;
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
            CaptureFirstWireSnapshot(observationEpoch, initialSnapshot.Snapshot);
            ValidateInitialSnapshotIdentity(initialSnapshot.Snapshot);
            // Do not renumber pending retries. Only new intents advance beyond
            // the authoritative ledger restored by a fresh host/client process.
            lock (pendingLock)
                nextClientSequence = Math.Max(nextClientSequence, hello.ServerHello.LastClientSequence);
            Enqueue(initialSnapshot);
            var initialSnapshotReceiptMs = clock.ElapsedMilliseconds;
            var connectedStream = stream;
            var connectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            var connectionGeneration = Interlocked.Increment(ref transportGeneration);
            if (supportsClockOnlyProbes)
                lastSnapshotRequestMs = 0;
            lock (pendingLock) {
                timingEpoch = connectionGeneration;
                negotiatedSnapshotRequestGeneration = connectionGeneration;
                negotiatedSnapshotRequestSentMs = null;
                negotiatedSnapshotRequestToken = null;
                negotiatedPostAckSnapshotGeneration = connectionGeneration;
                negotiatedPostAckSnapshotAppliedTick = null;
                negotiatedPostAckSnapshotObservedMs = null;
                if (supportsClockOnlyProbes && IsClockGatedCommandScheduling) {
                    lastSnapshotReceiptMs = initialSnapshotReceiptMs;
                    lastSnapshotReceiptGeneration = connectionGeneration;
                } else {
                    lastSnapshotReceiptMs = 0;
                    lastSnapshotReceiptGeneration = 0;
                }
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
            CompleteInitialConnectionAdmission(observationEpoch, admitted: true);
        } catch {
            CompleteInitialConnectionAdmission(observationEpoch, admitted: false);
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
                lastClockOnlyProbeSentMs = null;
                negotiatedSnapshotRequestSentMs = null;
                negotiatedSnapshotRequestGeneration = 0;
                negotiatedSnapshotRequestToken = null;
                negotiatedPostAckSnapshotGeneration = 0;
                negotiatedPostAckSnapshotAppliedTick = null;
                negotiatedPostAckSnapshotObservedMs = null;
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
            var queuedCommand = command.Clone();
            if (observedTick.HasValue
                && supportsTargetCellPathing
                && queuedCommand.IntentCase == Command.IntentOneofCase.MoveRequested
                && queuedCommand.MoveRequested.TargetCell)
                queuedCommand.MoveRequested.FollowPath = true;
            if (observedTick.HasValue
                && supportsQueuedRangedAttacks
                && queuedCommand.IntentCase == Command.IntentOneofCase.AttackRequested)
                queuedCommand.AttackRequested.QueueDuringMove = true;
            pending.Add(sequence, new PendingCommand(queuedCommand, observedTick));
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
            AuthoritativeClientProbeOutcomeTiming? expiredTimingProbe;
            var pollTimingRecorder = CommandTimingRecorder;
            var expirationStopwatchTimestamp = pollTimingRecorder is null ? 0 : Stopwatch.GetTimestamp();
            lock (pendingLock) {
                expiredTimingProbe = ExpireTimingProbeLocked(now, expirationStopwatchTimestamp);
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
            if (expiredTimingProbe is not null)
                TryRecordProbeOutcome(pollTimingRecorder, expiredTimingProbe);

            if (timingCalibrationTimedOut)
                await FailTimingCalibrationAsync(cancellationToken);

            if (needsTimingCalibration)
                await TrySendTimingProbeAsync(
                    forceCalibration: true,
                    cancellationToken: cancellationToken);

            var attemptedCommandDispatch = dueCommands.Length != 0;
            foreach (var entry in dueCommands) {
                var commandTimingRecorder = CommandTimingRecorder;
                CommandDispatchCapture? commandDispatch = null;
                Action<WriteAttemptTiming>? commandWriteObserver = null;
                if (commandTimingRecorder is not null) {
                    commandWriteObserver = writeTiming => {
                        if (commandDispatch is not { } dispatch)
                            return;
                        TryRecord(commandTimingRecorder, new AuthoritativeClientCommandTiming(
                            dispatch.ClientSequence,
                            dispatch.TransportGeneration,
                            dispatch.DispatchKind,
                            dispatch.Attempt,
                            dispatch.ObservedTick,
                            dispatch.RequestedTick,
                            dispatch.FreezeElapsedMilliseconds,
                            writeTiming.WriteStartElapsedMilliseconds,
                            writeTiming.WriteEndElapsedMilliseconds,
                            writeTiming.WriteOutcome,
                            dispatch.FreezeStopwatchTimestamp,
                            writeTiming.WriteStartStopwatchTimestamp,
                            writeTiming.WriteEndStopwatchTimestamp,
                            dispatch.MeasuredRoundTripMilliseconds,
                            dispatch.TimingCalibrationReady) {
                            IntentKind = dispatch.Intent.IntentKind,
                            MoveTargetCell = dispatch.Intent.MoveTargetCell,
                            MoveFollowPath = dispatch.Intent.MoveFollowPath,
                            MoveTargetX = dispatch.Intent.MoveTargetX,
                            MoveTargetY = dispatch.Intent.MoveTargetY,
                            MoveDirectionX = dispatch.Intent.MoveDirectionX,
                            MoveDirectionY = dispatch.Intent.MoveDirectionY,
                            AttackQueueDuringMove = dispatch.Intent.AttackQueueDuringMove,
                        });
                    };
                }
                try {
                    await WriteEnvelopeAsync(() => {
                        lock (pendingLock) {
                            if (!pending.ContainsKey(entry.Command.ClientSequence))
                                return null;
                            if (!entry.TransmissionStarted) {
                                if (entry.ObservedTick.HasValue
                                    && supportsClockOnlyProbes
                                    && IsClockGatedCommandScheduling) {
                                    var dispatchNow = clock.ElapsedMilliseconds;
                                    var currentGeneration = Volatile.Read(ref transportGeneration);
                                    var snapshotRequestBlocksDispatch
                                        = negotiatedSnapshotRequestSentMs is { } snapshotSentAt
                                            && negotiatedSnapshotRequestGeneration == currentGeneration
                                            && dispatchNow - snapshotSentAt
                                                < TimingProbeTimeout.TotalMilliseconds;
                                    var acknowledgementSnapshotBlocksDispatch
                                        = negotiatedPostAckSnapshotAppliedTick.HasValue
                                            && negotiatedPostAckSnapshotGeneration == currentGeneration
                                            && negotiatedPostAckSnapshotObservedMs is { } acknowledgementObservedAt
                                            && dispatchNow - acknowledgementObservedAt
                                                < TimingProbeTimeout.TotalMilliseconds;
                                    if (snapshotRequestBlocksDispatch
                                        || acknowledgementSnapshotBlocksDispatch)
                                        return null;
                                }
                                if (entry.ObservedTick is { } observedTick)
                                    entry.Command.RequestedTick = SuggestedCommandTick(observedTick);
                            }
                            entry.Retransmitted |= entry.TransmissionStarted;
                            // Freeze before any write: cancellation/partial writes may
                            // leave the server with this exact command identity.
                            entry.TransmissionStarted = true;
                            if (commandTimingRecorder is not null)
                                entry.DispatchAttempt++;
                            var freezeElapsedMilliseconds = clock.ElapsedMilliseconds;
                            var freezeStopwatchTimestamp = commandTimingRecorder is null
                                ? 0
                                : Stopwatch.GetTimestamp();
                            entry.LastSentMs = freezeElapsedMilliseconds;
                            if (IsClockGatedCommandScheduling)
                                entry.RetryNotBeforeMs = ComputeRetryNotBeforeMs(
                                    entry.Command.RequestedTick,
                                    entry.LastSentMs.Value);
                            if (commandTimingRecorder is not null)
                                commandDispatch = new CommandDispatchCapture(
                                    entry.Command.ClientSequence,
                                    Volatile.Read(ref transportGeneration),
                                    entry.Retransmitted
                                        ? AuthoritativeClientCommandDispatchKind.Retransmission
                                        : AuthoritativeClientCommandDispatchKind.FirstDispatch,
                                    entry.DispatchAttempt,
                                    entry.ObservedTick,
                                    entry.Command.RequestedTick,
                                    freezeElapsedMilliseconds,
                                    freezeStopwatchTimestamp,
                                    Volatile.Read(ref roundTripMilliseconds),
                                    timingCalibrationReady,
                                    CaptureCommandIntent(entry.Command));
                            return new Envelope { CommandBatch = new CommandBatch { Commands = { entry.Command } } };
                        }
                    }, cancellationToken, commandWriteObserver);
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
            AuthoritativeClientProbeOutcomeTiming? laterExpiredTimingProbe;
            lock (pendingLock) {
                var probeNow = clock.ElapsedMilliseconds;
                var probeStopwatchTimestamp = pollTimingRecorder is null ? 0 : Stopwatch.GetTimestamp();
                laterExpiredTimingProbe = ExpireTimingProbeLocked(probeNow, probeStopwatchTimestamp);
                hasPendingCommands = pending.Count > 0;
                forceTimingProbe = IsClockGatedCommandScheduling
                    && !timingCalibrationReady
                    && pending.Values.Any(command =>
                        !command.TransmissionStarted && command.ObservedTick.HasValue);
            }
            if (laterExpiredTimingProbe is not null)
                TryRecordProbeOutcome(pollTimingRecorder, laterExpiredTimingProbe);
            if (timingCalibrationTimedOut || this.timingCalibrationTimedOut)
                await FailTimingCalibrationAsync(cancellationToken);
            now = clock.ElapsedMilliseconds;
            if (supportsClockOnlyProbes && IsClockGatedCommandScheduling) {
                var currentGeneration = Volatile.Read(ref transportGeneration);
                var snapshotPollDue = !forceTimingProbe
                    && !hasPendingCommands
                    && IsNegotiatedSnapshotPollDue(now, currentGeneration)
                    && !attemptedCommandDispatch;
                if (forceTimingProbe || (!hasPendingCommands && !attemptedCommandDispatch))
                    await TrySendTimingProbeAsync(forceTimingProbe, cancellationToken);

                // Keep expensive full-snapshot work bounded independently of the
                // lightweight clock probe; only an admitted snapshot releases this slot.
                if (snapshotPollDue) {
                    var requestRegistered = false;
                    var requestToken = 0L;
                    try {
                        await WriteEnvelopeAsync(() => {
                            lock (pendingLock) {
                                var sentAt = clock.ElapsedMilliseconds;
                                if (!IsNegotiatedSnapshotPollDueLocked(sentAt, currentGeneration))
                                    return null;

                                var nextToken = unchecked(nextNegotiatedSnapshotRequestToken + 1);
                                if (nextToken == 0)
                                    nextToken = 1;
                                nextNegotiatedSnapshotRequestToken = nextToken;
                                negotiatedSnapshotRequestToken = nextToken;
                                negotiatedSnapshotRequestGeneration = currentGeneration;
                                negotiatedSnapshotRequestSentMs = sentAt;
                                requestToken = nextToken;
                                requestRegistered = true;
                                return new Envelope {
                                    SnapshotRequest = new Envelope.Types.SnapshotRequest(),
                                };
                            }
                        }, cancellationToken);
                        if (requestRegistered)
                            lastSnapshotRequestMs = clock.ElapsedMilliseconds;
                    } catch {
                        if (requestRegistered)
                            CancelNegotiatedSnapshotRequest(requestToken, currentGeneration);
                        throw;
                    }
                }
            } else if ((!forceTimingProbe
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
        var usesClockOnlyProbe = supportsClockOnlyProbes && IsClockGatedCommandScheduling;
        var probeDispatchElapsedMilliseconds = 0L;
        var probeTimingRecorder = CommandTimingRecorder;
        ProbeSentCapture? probeSent = null;
        Action<WriteAttemptTiming>? probeWriteObserver = null;
        if (probeTimingRecorder is not null) {
            probeWriteObserver = writeTiming => {
                if (probeSent is not { } sent)
                    return;
                TryRecordProbeSent(probeTimingRecorder, new AuthoritativeClientProbeSentTiming(
                    sent.TimingRequestId,
                    sent.TransportGeneration,
                    sent.RequiresCalibration,
                    sent.SentElapsedMilliseconds,
                    writeTiming.WriteStartElapsedMilliseconds,
                    writeTiming.WriteEndElapsedMilliseconds,
                    writeTiming.WriteOutcome,
                    sent.SentStopwatchTimestamp,
                    writeTiming.WriteStartStopwatchTimestamp,
                    writeTiming.WriteEndStopwatchTimestamp,
                    sent.MeasuredRoundTripMilliseconds,
                    sent.TimingCalibrationReady));
            };
        }
        try {
            await WriteEnvelopeAsync(() => {
                lock (pendingLock) {
                    var dispatchNow = clock.ElapsedMilliseconds;
                    var requiresCalibration = forceCalibration
                        && !timingCalibrationReady
                        && pending.Values.Any(command =>
                            !command.TransmissionStarted && command.ObservedTick.HasValue);
                    var probeInterval = requiresCalibration
                        ? TimingCalibrationRetryInterval.TotalMilliseconds
                        : options.SnapshotPollInterval.TotalMilliseconds;
                    var probeIntervalElapsed = usesClockOnlyProbe
                        ? !lastClockOnlyProbeSentMs.HasValue
                            || dispatchNow - lastClockOnlyProbeSentMs.Value >= probeInterval
                        : !lastTimingProbeRequestMs.HasValue
                            || dispatchNow - lastTimingProbeRequestMs.Value >= probeInterval;
                    var snapshotRequestBlocksProbe = usesClockOnlyProbe
                        && negotiatedSnapshotRequestSentMs is { } snapshotSentAt
                        && negotiatedSnapshotRequestGeneration == timingEpoch
                        && dispatchNow - snapshotSentAt < TimingProbeTimeout.TotalMilliseconds;
                    var acknowledgementSnapshotBlocksProbe = usesClockOnlyProbe
                        && negotiatedPostAckSnapshotAppliedTick.HasValue
                        && negotiatedPostAckSnapshotGeneration == timingEpoch
                        && negotiatedPostAckSnapshotObservedMs is { } acknowledgementObservedAt
                        && dispatchNow - acknowledgementObservedAt < TimingProbeTimeout.TotalMilliseconds;
                    var unresolvedNegotiatedResponse = snapshotRequestBlocksProbe
                        || acknowledgementSnapshotBlocksProbe;
                    var probeNotAllowed = usesClockOnlyProbe
                        ? requiresCalibration
                            ? !probeIntervalElapsed || unresolvedNegotiatedResponse
                            : forceCalibration || pending.Count > 0 || !probeIntervalElapsed
                                || unresolvedNegotiatedResponse
                        : requiresCalibration
                            ? !probeIntervalElapsed
                            : forceCalibration
                                || pending.Count > 0
                                || dispatchNow - lastSnapshotRequestMs
                                    < options.SnapshotPollInterval.TotalMilliseconds;
                    if (timingProbe is not null || probeNotAllowed)
                        return null;

                    timingRequestId = NextTimingRequestId();
                    var sentStopwatchTimestamp = probeTimingRecorder is null ? 0 : Stopwatch.GetTimestamp();
                    timingProbe = new TimingProbe(
                        timingRequestId,
                        timingEpoch,
                        requiresCalibration,
                        usesClockOnlyProbe,
                        dispatchNow,
                        SaturatingAdd(dispatchNow, TimingProbeTimeout.TotalMilliseconds),
                        sentStopwatchTimestamp);
                    lastTimingProbeRequestMs = dispatchNow;
                    probeDispatchElapsedMilliseconds = dispatchNow;
                    probeRegistered = true;
                    if (probeTimingRecorder is not null)
                        probeSent = new ProbeSentCapture(
                            timingRequestId,
                            timingEpoch,
                            requiresCalibration,
                            dispatchNow,
                            sentStopwatchTimestamp,
                            Volatile.Read(ref roundTripMilliseconds),
                            timingCalibrationReady);
                    return usesClockOnlyProbe
                        ? new Envelope {
                            ClockProbeRequest = new ClockProbeRequest { RequestId = timingRequestId },
                        }
                        : new Envelope {
                            SnapshotRequest = new Envelope.Types.SnapshotRequest {
                                TimingRequestId = timingRequestId,
                            },
                        };
                }
            }, cancellationToken, probeWriteObserver);
            if (probeRegistered) {
                if (usesClockOnlyProbe)
                    lastClockOnlyProbeSentMs = probeDispatchElapsedMilliseconds;
                else
                    lastSnapshotRequestMs = clock.ElapsedMilliseconds;
            }
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

    private bool IsNegotiatedSnapshotPollDue(long now, long generation)
    {
        lock (pendingLock)
            return IsNegotiatedSnapshotPollDueLocked(now, generation);
    }

    private bool IsNegotiatedSnapshotPollDueLocked(long now, long generation)
    {
        if (generation != Volatile.Read(ref transportGeneration) || pending.Count > 0)
            return false;
        if (negotiatedPostAckSnapshotGeneration == generation
            && negotiatedPostAckSnapshotAppliedTick.HasValue
            && negotiatedPostAckSnapshotObservedMs is { } acknowledgementObservedAt
            && now - acknowledgementObservedAt < TimingProbeTimeout.TotalMilliseconds)
            return false;
        if (negotiatedSnapshotRequestSentMs is { } previousSentAt
            && negotiatedSnapshotRequestGeneration == generation
            && now - previousSentAt < TimingProbeTimeout.TotalMilliseconds)
            return false;

        var retryDue = negotiatedSnapshotRequestSentMs is { } sentAt
            && (negotiatedSnapshotRequestGeneration != generation
                || now - sentAt >= TimingProbeTimeout.TotalMilliseconds);
        var cadenceBaseline = lastSnapshotRequestMs;
        if (lastSnapshotReceiptGeneration == generation)
            cadenceBaseline = Math.Max(cadenceBaseline, lastSnapshotReceiptMs);
        return retryDue
            || now - cadenceBaseline >= options.SnapshotPollInterval.TotalMilliseconds;
    }

    private void RecordNegotiatedSnapshotReceipt(long generation)
    {
        if (!supportsClockOnlyProbes || !IsClockGatedCommandScheduling)
            return;

        var receivedAt = clock.ElapsedMilliseconds;
        lock (pendingLock) {
            if (generation != Volatile.Read(ref transportGeneration))
                return;
            lastSnapshotReceiptMs = receivedAt;
            lastSnapshotReceiptGeneration = generation;
        }
    }

    private void CancelNegotiatedSnapshotRequest(long token, long generation)
    {
        lock (pendingLock) {
            if (negotiatedSnapshotRequestGeneration != generation
                || negotiatedSnapshotRequestToken != token)
                return;
            negotiatedSnapshotRequestSentMs = null;
            negotiatedSnapshotRequestToken = null;
        }
    }

    private void CompleteNegotiatedSnapshotRequest(long generation)
    {
        if (generation != Volatile.Read(ref transportGeneration))
            return;
        lock (pendingLock) {
            if (negotiatedSnapshotRequestGeneration != generation)
                return;
            negotiatedSnapshotRequestSentMs = null;
            negotiatedSnapshotRequestToken = null;
        }
    }

    private void CompleteNegotiatedPostAckSnapshot(long generation, ulong snapshotTick)
    {
        if (generation != Volatile.Read(ref transportGeneration))
            return;
        lock (pendingLock) {
            if (negotiatedPostAckSnapshotGeneration != generation
                || negotiatedPostAckSnapshotAppliedTick is not { } appliedTick
                || snapshotTick < appliedTick)
                return;
            negotiatedPostAckSnapshotAppliedTick = null;
            negotiatedPostAckSnapshotObservedMs = null;
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
        var terminalKind = AuthoritativeClientReceiveTerminalKind.Cancellation;
        Exception? terminalFailure = null;
        try {
            while (!cancellationToken.IsCancellationRequested) {
                var envelope = await ReadEnvelopeAsync(networkStream, cancellationToken);
                if (envelope is null) {
                    terminalKind = AuthoritativeClientReceiveTerminalKind.EndOfStream;
                    break;
                }
                Enqueue(envelope, connectionGeneration);
            }
        } catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && (exception is IOException or SocketException or EndOfStreamException or ObjectDisposedException)) {
            terminalKind = exception is InvalidProtocolBufferException
                ? AuthoritativeClientReceiveTerminalKind.ProtocolFailure
                : AuthoritativeClientReceiveTerminalKind.TransportFailure;
            terminalFailure = exception;
            EnqueueConnectionClosed(exception.Message);
        } catch (Exception exception) {
            terminalFailure = exception;
            terminalKind = cancellationToken.IsCancellationRequested
                ? AuthoritativeClientReceiveTerminalKind.Cancellation
                : exception is InvalidDataException or InvalidProtocolBufferException
                    ? AuthoritativeClientReceiveTerminalKind.ProtocolFailure
                    : AuthoritativeClientReceiveTerminalKind.UnexpectedFailure;
            throw;
        } finally {
            // Keep the original receive outcome even when the UI reports only connection loss.
            // Diagnostics must never replace an existing transport/protocol failure.
            if (CommandTimingRecorder is { } recorder) {
                try {
                    recorder.RecordReceiveTerminal(new AuthoritativeClientReceiveTerminalTiming(
                        connectionGeneration, terminalKind, clock.ElapsedMilliseconds,
                        Stopwatch.GetTimestamp(), terminalFailure?.GetType().FullName, terminalFailure?.HResult));
                } catch { }
            }
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
        var snapshotTiming = envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot
            ? CaptureSnapshotTiming(envelope.Snapshot, connectionGeneration)
            : null;
        if (envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot
            && !ObserveSnapshotIdentity(envelope.Snapshot))
            return;
        if (envelope.PayloadCase == Envelope.PayloadOneofCase.Snapshot) {
            if (connectionGeneration is { } snapshotGeneration) {
                RecordNegotiatedSnapshotReceipt(snapshotGeneration);
                CompleteNegotiatedSnapshotRequest(snapshotGeneration);
                CompleteNegotiatedPostAckSnapshot(snapshotGeneration, envelope.Snapshot.Tick);
            }
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
            Acknowledge(envelope.CommandAck, connectionGeneration);
        }
        if (snapshotTiming is not null)
            TryRecordSnapshot(CommandTimingRecorder, snapshotTiming);
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

    private AuthoritativeClientSnapshotTiming? CaptureSnapshotTiming(
        Snapshot snapshot,
        long? connectionGeneration)
    {
        var recorder = CommandTimingRecorder;
        if (recorder is null)
            return null;

        var receivedElapsedMilliseconds = clock.ElapsedMilliseconds;
        var receivedStopwatchTimestamp = Stopwatch.GetTimestamp();
        ulong? pendingTimingRequestId = null;
        bool calibrationReady;
        lock (pendingLock) {
            pendingTimingRequestId = timingProbe?.RequestId;
            calibrationReady = timingCalibrationReady;
        }
        return new AuthoritativeClientSnapshotTiming(
            connectionGeneration ?? Volatile.Read(ref transportGeneration),
            snapshot.Tick,
            receivedElapsedMilliseconds,
            pendingTimingRequestId,
            receivedStopwatchTimestamp,
            calibrationReady);
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

    private long BeginInitialConnectionObservation()
    {
        lock (initialConnectionObservationLock) {
            var epoch = checked(initialConnectionAttemptEpoch + 1);
            initialConnectionAttemptEpoch = epoch;
            initialConnectionObservation = new AuthoritativeInitialConnectionObservation(epoch);
            return epoch;
        }
    }

    private void CaptureInitialServerHello(
        long epoch,
        ServerHello hello,
        bool resumedSessionMatched)
    {
        lock (initialConnectionObservationLock) {
            if (initialConnectionAttemptEpoch != epoch
                || initialConnectionObservation is not { ServerHello: null } observation)
                return;

            initialConnectionObservation = observation.WithServerHello(
                new AuthoritativeServerHelloObservation(
                    hello.ServerBuildId,
                    hello.ProtocolSchemaVersion,
                    hello.ContentManifestHash,
                    hello.RulesetIdentityHash,
                    hello.TickRateHz,
                    hello.GameMode,
                    hello.CommandSchedulingMode,
                    hello.LastClientSequence,
                    resumedSessionMatched)
                {
                    SupportsQueuedRangedAttacks = hello.SupportsQueuedRangedAttacks,
                });
        }
    }

    private void CaptureFirstWireSnapshot(long epoch, Snapshot snapshot)
    {
        lock (initialConnectionObservationLock) {
            if (initialConnectionAttemptEpoch != epoch
                || initialConnectionObservation is not { HasFirstWireSnapshot: false } observation)
                return;

            initialConnectionObservation = observation.WithFirstWireSnapshot(snapshot);
        }
    }

    private void CompleteInitialConnectionAdmission(long epoch, bool admitted)
    {
        lock (initialConnectionObservationLock) {
            if (initialConnectionAttemptEpoch != epoch
                || initialConnectionObservation is not { } observation)
                return;

            initialConnectionObservation = observation.WithAdmissionCompleted(admitted);
        }
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
        var recorder = CommandTimingRecorder;
        var outcomeStopwatchTimestamp = recorder is null ? 0 : Stopwatch.GetTimestamp();
        AuthoritativeClientProbeOutcomeTiming? outcome = null;
        lock (pendingLock) {
            if (timingProbe is not { } probe
                || probe.RequestId != reply.RequestId
                || probe.Epoch != connectionGeneration
                || timingEpoch != connectionGeneration
                || now >= probe.ExpiresAtMs)
                return;
            if (probe.UsesClockOnlyProbe) {
                // A negotiated clock-only reply must include its physical
                // scheduling sample; leave the probe outstanding for the
                // existing bounded expiry path when an older peer omits it.
                if (!reply.HasServerSchedulingClockTick)
                    return;
                commandClock.ObserveMonotonic(reply.ServerSchedulingClockTick, ServerTickRateHz);
            }
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
            if (recorder is not null)
                outcome = new AuthoritativeClientProbeOutcomeTiming(
                    probe.RequestId,
                    probe.Epoch,
                    AuthoritativeClientProbeOutcome.Reply,
                    now,
                    sample,
                    outcomeStopwatchTimestamp,
                    timingCalibrationReady);
        }
        if (outcome is not null)
            TryRecordProbeOutcome(recorder, outcome);
    }

    private AuthoritativeClientProbeOutcomeTiming? ExpireTimingProbeLocked(
        long now,
        long outcomeStopwatchTimestamp)
    {
        if (timingProbe is not { } probe || now < probe.ExpiresAtMs)
            return null;

        timingProbe = null;
        if (probe.RequiresCalibration
            && ++timingCalibrationProbeAttempts >= MaximumCalibrationProbeAttempts) {
            timingCalibrationTimedOut = pending.Values.Any(command =>
                !command.TransmissionStarted && command.ObservedTick.HasValue);
        }
        return CommandTimingRecorder is null
            ? null
            : new AuthoritativeClientProbeOutcomeTiming(
                probe.RequestId,
                probe.Epoch,
                AuthoritativeClientProbeOutcome.Expired,
                now,
                null,
                outcomeStopwatchTimestamp,
                timingCalibrationReady);
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

    private void Acknowledge(CommandAck acknowledgement, long? connectionGeneration)
    {
        var now = clock.ElapsedMilliseconds;
        var recorder = CommandTimingRecorder;
        var receivedStopwatchTimestamp = recorder is null ? 0 : Stopwatch.GetTimestamp();
        var transportGenerationForRecord = connectionGeneration ?? Volatile.Read(ref transportGeneration);
        foreach (var result in acknowledgement.Results) {
            var trackedPendingCommand = false;
            var wasRetransmitted = false;
            long? commandRoundTripMilliseconds = null;
            lock (pendingLock) {
                if (supportsClockOnlyProbes
                    && IsClockGatedCommandScheduling
                    && transportGenerationForRecord == Volatile.Read(ref transportGeneration)) {
                    if (negotiatedPostAckSnapshotGeneration != transportGenerationForRecord
                        || negotiatedPostAckSnapshotAppliedTick is not { } previousAppliedTick
                        || result.AppliedTick > previousAppliedTick) {
                        negotiatedPostAckSnapshotGeneration = transportGenerationForRecord;
                        negotiatedPostAckSnapshotAppliedTick = result.AppliedTick;
                    }
                    // Every validated ACK is followed by durable snapshot publication.
                    // Keep the next clock sample behind that response boundary.
                    negotiatedPostAckSnapshotObservedMs = now;
                }
                if (pending.Remove(result.ClientSequence, out var removedCommand)
                    && removedCommand is not null) {
                    trackedPendingCommand = true;
                    wasRetransmitted = removedCommand.Retransmitted;
                    if (!wasRetransmitted && removedCommand.LastSentMs is { } lastSentMs)
                        commandRoundTripMilliseconds = now - lastSentMs;
                }
                if (commandRoundTripMilliseconds is { } commandRoundTrip
                    && !IsClockGatedCommandScheduling) {
                    // An ACK after retransmission cannot identify which send it times.
                    Volatile.Write(ref roundTripMilliseconds,
                        (roundTripMilliseconds * 0.75) + (commandRoundTrip * 0.25));
                }
            }
            if (recorder is not null)
                TryRecordAcknowledgement(recorder, new AuthoritativeClientAcknowledgementTiming(
                    transportGenerationForRecord,
                    result.ClientSequence,
                    now,
                    result.Status,
                    result.RejectReason,
                    result.AppliedTick,
                    trackedPendingCommand,
                    wasRetransmitted,
                    commandRoundTripMilliseconds,
                    receivedStopwatchTimestamp));
        }
    }

    private Task WriteEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken)
        => WriteEnvelopeAsync(() => envelope, cancellationToken);

    private async Task WriteEnvelopeAsync(
        Func<Envelope?> createEnvelope,
        CancellationToken cancellationToken,
        Action<WriteAttemptTiming>? timingObserver = null)
    {
        await writeLock.WaitAsync(cancellationToken);
        Envelope? envelope = null;
        long? writeStartElapsedMilliseconds = null;
        long? writeEndElapsedMilliseconds = null;
        long? writeStartStopwatchTimestamp = null;
        long? writeEndStopwatchTimestamp = null;
        var writeOutcome = AuthoritativeClientWriteOutcome.Failed;
        try {
            envelope = createEnvelope();
            if (envelope is null)
                return;
            var payload = envelope.ToByteArray();
            if (payload.Length == 0 || payload.Length > AuthoritativeProtocolLimits.MaximumEnvelopeBytes)
                throw new InvalidDataException("The authoritative envelope is outside the allowed size range.");
            var header = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
            var currentStream = stream ?? throw new IOException("The authoritative transport is closed.");
            if (timingObserver is not null) {
                writeStartElapsedMilliseconds = clock.ElapsedMilliseconds;
                writeStartStopwatchTimestamp = Stopwatch.GetTimestamp();
            }
            await currentStream.WriteAsync(header, cancellationToken);
            await currentStream.WriteAsync(payload, cancellationToken);
            await currentStream.FlushAsync(cancellationToken);
            writeOutcome = AuthoritativeClientWriteOutcome.Completed;
        } finally {
            if (timingObserver is not null && writeStartElapsedMilliseconds.HasValue) {
                writeEndElapsedMilliseconds = clock.ElapsedMilliseconds;
                writeEndStopwatchTimestamp = Stopwatch.GetTimestamp();
            }
            if (timingObserver is not null && envelope is not null) {
                try {
                    timingObserver(new WriteAttemptTiming(
                        writeStartElapsedMilliseconds,
                        writeEndElapsedMilliseconds,
                        writeStartStopwatchTimestamp,
                        writeEndStopwatchTimestamp,
                        writeOutcome));
                } catch {
                    // Diagnostics cannot mask a transport or gameplay failure.
                }
            }
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

    private static void TryRecord(
        AuthoritativeClientCommandTimingRecorder? recorder,
        AuthoritativeClientCommandTiming timing)
    {
        if (recorder is null)
            return;
        try {
            recorder.RecordCommand(timing);
        } catch {
            // Diagnostics cannot mask a transport or gameplay failure.
        }
    }

    private static void TryRecordProbeSent(
        AuthoritativeClientCommandTimingRecorder? recorder,
        AuthoritativeClientProbeSentTiming timing)
    {
        if (recorder is null)
            return;
        try {
            recorder.RecordProbeSent(timing);
        } catch {
        }
    }

    private static void TryRecordProbeOutcome(
        AuthoritativeClientCommandTimingRecorder? recorder,
        AuthoritativeClientProbeOutcomeTiming timing)
    {
        if (recorder is null)
            return;
        try {
            recorder.RecordProbeOutcome(timing);
        } catch {
        }
    }

    private static void TryRecordSnapshot(
        AuthoritativeClientCommandTimingRecorder? recorder,
        AuthoritativeClientSnapshotTiming timing)
    {
        if (recorder is null)
            return;
        try {
            recorder.RecordSnapshot(timing);
        } catch {
        }
    }

    private static void TryRecordAcknowledgement(
        AuthoritativeClientCommandTimingRecorder? recorder,
        AuthoritativeClientAcknowledgementTiming timing)
    {
        if (recorder is null)
            return;
        try {
            recorder.RecordAcknowledgement(timing);
        } catch {
        }
    }

    private sealed class PendingCommand(Command command, ulong? observedTick)
    {
        public Command Command { get; } = command;
        public ulong? ObservedTick { get; } = observedTick;
        public bool TransmissionStarted { get; set; }
        public bool Retransmitted { get; set; }
        public int DispatchAttempt { get; set; }
        public long? LastSentMs { get; set; }
        public long? RetryNotBeforeMs { get; set; }
    }

    private static CommandIntentCapture CaptureCommandIntent(Command command)
    {
        var intentKind = command.IntentCase;
        if (intentKind == Command.IntentOneofCase.AttackRequested)
            return new CommandIntentCapture(
                intentKind,
                false,
                false,
                null,
                null,
                null,
                null,
                command.AttackRequested.QueueDuringMove);
        if (intentKind != Command.IntentOneofCase.MoveRequested)
            return new CommandIntentCapture(intentKind, false, false, null, null, null, null, false);

        var move = command.MoveRequested;
        return move.TargetCell
            ? new CommandIntentCapture(intentKind, true, move.FollowPath,
                move.TargetX, move.TargetY, null, null, false)
            : new CommandIntentCapture(intentKind, false, move.FollowPath,
                null, null, move.DirectionX, move.DirectionY, false);
    }

    private readonly record struct CommandIntentCapture(
        Command.IntentOneofCase IntentKind,
        bool MoveTargetCell,
        bool MoveFollowPath,
        int? MoveTargetX,
        int? MoveTargetY,
        int? MoveDirectionX,
        int? MoveDirectionY,
        bool AttackQueueDuringMove);

    private readonly record struct CommandDispatchCapture(
        ulong ClientSequence,
        long TransportGeneration,
        AuthoritativeClientCommandDispatchKind DispatchKind,
        int Attempt,
        ulong? ObservedTick,
        ulong RequestedTick,
        long FreezeElapsedMilliseconds,
        long FreezeStopwatchTimestamp,
        double MeasuredRoundTripMilliseconds,
        bool TimingCalibrationReady,
        CommandIntentCapture Intent);

    private readonly record struct ProbeSentCapture(
        ulong TimingRequestId,
        long TransportGeneration,
        bool RequiresCalibration,
        long SentElapsedMilliseconds,
        long SentStopwatchTimestamp,
        double MeasuredRoundTripMilliseconds,
        bool TimingCalibrationReady);

    private readonly record struct WriteAttemptTiming(
        long? WriteStartElapsedMilliseconds,
        long? WriteEndElapsedMilliseconds,
        long? WriteStartStopwatchTimestamp,
        long? WriteEndStopwatchTimestamp,
        AuthoritativeClientWriteOutcome WriteOutcome);

    private sealed record TimingProbe(
        ulong RequestId,
        long Epoch,
        bool RequiresCalibration,
        bool UsesClockOnlyProbe,
        long SentAtMs,
        long ExpiresAtMs,
        long SentStopwatchTimestamp);
}
