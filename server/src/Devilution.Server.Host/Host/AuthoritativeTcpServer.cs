using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using EngineClock = Devilution.Engine.IAuthoritativeClock;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;
using EngineEntityIdAllocator = Devilution.Engine.DeterministicEntityIdAllocator;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Devilution.Server.Simulation;
using Devilution.Server.Stores;

namespace Devilution.Server.Host;

/**
 * TCP session host for length-delimited Protobuf envelopes.
 *
 * Each connection is a session for the current slice. The server-issued
 * resume token also provides a stable save identity, allowing a local server
 * restart to restore the same authoritative session state.
 */
public sealed partial class AuthoritativeTcpServer : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly AuthoritativeCommandServer commandServer;
    private readonly ProtocolHandshake handshake;
    private readonly EngineClock clock;
    private readonly IAuthoritativeSnapshotProvider? snapshotProvider;
    private readonly IAuthoritativeEventProvider? eventProvider;
    private readonly AuthoritativeSaveStore? saveStore;
    private readonly AuthoritativeCommandReceiptStore? commandReceiptStore;
    private readonly AuthoritativeSessionStore? sessionStore;
    private readonly AuthoritativeSessionPersistence sessionPersistence;
    private readonly IAuthoritativeSaveProvider? saveProvider;
    private readonly IAuthoritativeParticipantInitializer? participantInitializer;
    private IAuthoritativeGameHostAdapter? gameAdapter;
    private readonly EngineEntityIdAllocator entityIds = new();
    private readonly ConcurrentDictionary<TcpClient, byte> clients = new();
    private readonly ConcurrentDictionary<TcpClient, Task> clientHandlers = new();
    private readonly ConcurrentDictionary<string, SessionState> sessionsByToken = new(StringComparer.Ordinal);
    // This fences only the token's admission; it cannot roll back shared-world mutations by a provider.
    // Recovery requires a fresh host or explicit operator repair; never delete the durable checkpoint.
    private readonly HashSet<string> poisonedSessionTokens = new(StringComparer.Ordinal);
    private readonly object sessionCreationSynchronization = new();
    private readonly object lifecycleSynchronization = new();
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ClockGatedCommandScheduler? commandScheduler;
    private readonly CohortAuthorityTurnGate cohortAuthorityTurnGate = new();
    private ClockGatedResponseBudget? clockGatedResponseBudget;
    private Exception? lastClientFailure;
    private Task? disposal;
    private CancellationTokenSource? cancellation;
    private Task? acceptLoop;
    private Task? simulationLoop;
    private ulong lastSimulationTick;
    private long nextConnectionOrdinal;
    private long nextTimingOrdinal;
    private AuthoritativeSessionRestorePolicy sessionRestorePolicy =
        AuthoritativeSessionRestorePolicy.Compatibility;

    public AuthoritativeTcpServer(
        AuthoritativeCommandServer commandServer,
        ProtocolHandshake handshake,
        EngineClock clock,
        IAuthoritativeGameHostAdapter gameAdapter,
        int port = 0,
        IPAddress? address = null,
        AuthoritativeSaveStore? saveStore = null,
        AuthoritativeCommandReceiptStore? commandReceiptStore = null,
        AuthoritativeSessionStore? sessionStore = null)
        : this(
            commandServer,
            handshake,
            clock,
            port,
            address,
            gameAdapter,
            gameAdapter,
            saveStore,
            gameAdapter,
            gameAdapter,
            commandReceiptStore,
            sessionStore)
    {
        ArgumentNullException.ThrowIfNull(gameAdapter);
        this.gameAdapter = gameAdapter;
    }

    public AuthoritativeTcpServer(
        AuthoritativeCommandServer commandServer,
        ProtocolHandshake handshake,
        Func<ulong> currentTickProvider,
        int port = 0,
        IPAddress? address = null,
        IAuthoritativeSnapshotProvider? snapshotProvider = null,
        IAuthoritativeEventProvider? eventProvider = null,
        AuthoritativeSaveStore? saveStore = null,
        IAuthoritativeSaveProvider? saveProvider = null,
        IAuthoritativeParticipantInitializer? participantInitializer = null,
        AuthoritativeCommandReceiptStore? commandReceiptStore = null,
        AuthoritativeSessionStore? sessionStore = null)
        : this(commandServer, handshake, new DelegateAuthoritativeClock(currentTickProvider), port, address, snapshotProvider, eventProvider, saveStore, saveProvider, participantInitializer, commandReceiptStore, sessionStore)
    {
    }

    public AuthoritativeTcpServer(
        AuthoritativeCommandServer commandServer,
        ProtocolHandshake handshake,
        IAuthoritativeClock clock,
        int port = 0,
        IPAddress? address = null,
        IAuthoritativeSnapshotProvider? snapshotProvider = null,
        IAuthoritativeEventProvider? eventProvider = null,
        AuthoritativeSaveStore? saveStore = null,
        IAuthoritativeSaveProvider? saveProvider = null,
        IAuthoritativeParticipantInitializer? participantInitializer = null,
        AuthoritativeCommandReceiptStore? commandReceiptStore = null,
        AuthoritativeSessionStore? sessionStore = null)
        : this(
            commandServer,
            handshake,
            (EngineClock)clock,
            port,
            address,
            snapshotProvider,
            eventProvider,
            saveStore,
            saveProvider,
            participantInitializer,
            commandReceiptStore,
            sessionStore)
    {
    }

    public AuthoritativeTcpServer(
        AuthoritativeCommandServer commandServer,
        ProtocolHandshake handshake,
        EngineClock clock,
        int port = 0,
        IPAddress? address = null,
        IAuthoritativeSnapshotProvider? snapshotProvider = null,
        IAuthoritativeEventProvider? eventProvider = null,
        AuthoritativeSaveStore? saveStore = null,
        IAuthoritativeSaveProvider? saveProvider = null,
        IAuthoritativeParticipantInitializer? participantInitializer = null,
        AuthoritativeCommandReceiptStore? commandReceiptStore = null,
        AuthoritativeSessionStore? sessionStore = null)
    {
        this.commandServer = commandServer ?? throw new ArgumentNullException(nameof(commandServer));
        this.handshake = handshake ?? throw new ArgumentNullException(nameof(handshake));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.snapshotProvider = snapshotProvider;
        this.eventProvider = eventProvider;
        this.saveStore = saveStore;
        this.commandReceiptStore = commandReceiptStore
            ?? (saveStore is null
                ? null
                : new AuthoritativeCommandReceiptStore(saveStore.RootDirectory));
        this.sessionStore = sessionStore
            ?? (saveStore is null
                ? null
                : new AuthoritativeSessionStore(saveStore.RootDirectory));
        sessionPersistence = new AuthoritativeSessionPersistence(
            this.saveStore,
            this.commandReceiptStore,
            this.sessionStore);
        this.saveProvider = saveProvider;
        this.participantInitializer = participantInitializer;
        listener = new TcpListener(address ?? IPAddress.Loopback, port);
    }

    public int Port => ((IPEndPoint?)listener.LocalEndpoint)?.Port ?? 0;

    /** Explicit complete-cohort mode; this host owns the store's lifetime lease. */
    public AuthoritativeCohortStore? CohortStore { get; init; }

    /** Defaults to legacy sidecar fallback; strict restore is an explicit host opt-in. */
    public AuthoritativeSessionRestorePolicy SessionRestorePolicy
    {
        get => sessionRestorePolicy;
        init {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(SessionRestorePolicy));
            sessionRestorePolicy = value;
        }
    }

    /** Null by default. Diagnostics never alter authoritative ticks or protocol state. */
    public HostPerformanceRecorder? PerformanceRecorder { get; init; }

    /** Null by default. Timing evidence is bounded and never enters gameplay. */
    public HostCommandTimingRecorder? CommandTimingRecorder { get; init; }

    /** Internal transport seam for causal write/cancellation tests; normal hosts leave it null. */
    internal Func<Stream, Stream>? ClockGatedTransportDecorator { get; init; }

    /**
     * Optional post-lock drain hook. Hosts should enqueue the drain and return
     * quickly; failures are diagnostic and cannot replace a gameplay failure.
     */
    public Action<HostCommandTimingDrain>? CommandTimingDrainObserver { get; init; }

    /** Whole-host policy; legacy clients retain immediate scheduling by default. */
    public bool UseClockGatedCommandScheduling { get; init; }

    public int MaximumPendingCommands { get; init; } = ClockGatedCommandScheduler.DefaultMaximumPendingCommands;

    public int PendingCommandCount => commandScheduler?.PendingCommandCount ?? 0;

    /** Internal synchronization seam for deterministic authority-turn host tests. */
    internal CohortAuthorityTurnGate AuthorityTurnGateForTests => cohortAuthorityTurnGate;

    /** Background termination/failure, excluding isolated session failures. */
    public Task Completion => completion.Task;

    /** Isolated failed session publication is observable and never acknowledged. */
    public Exception? LastClientFailure => Volatile.Read(ref lastClientFailure);

    public Action<Exception>? ClientFailureObserver { get; init; }

    /** Combined identity hash advertised by the current handshake. */
    public string ActiveRulesetIdentityHash => handshake.Identity.RulesetIdentityHash;

    public string ActiveContentManifestHash => handshake.Identity.ContentManifestHash;

    /** Replaced generations awaiting host disposal, if live reload is enabled. */
    public int RetiredRulesetCount => gameAdapter is LeasedAuthoritativeGameHostAdapter leasedAdapter
        ? leasedAdapter.RetiredRulesetCount
        : 0;

    /** Returns initialized sessions in deterministic identity order. */
    public IReadOnlyList<string> ActiveSessionIds => CohortStore is not null
        ? commandServer.WithSynchronizedState(() => cohortSessionsById.Values
            .Where(session => session.ParticipantInitialized)
            .Select(session => session.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray())
        : sessionsByToken.Values
        .Where(session => session.ParticipantInitialized)
        .Select(session => session.Id)
        .OrderBy(sessionId => sessionId, StringComparer.Ordinal)
        .ToArray();

    /**
     * Reloads the active ruleset through the leased adapter and updates the
     * handshake identity only after every initialized session has migrated.
     */
    public Devilution.Engine.GameplayRulesetReloadResult<IAuthoritativeGameHostAdapter> TryReloadRuleset(
        ContentManifest candidateManifest,
        IEnumerable<Devilution.Engine.IGameplayModule> candidateModules)
        // Match ordinary host operations: command state before adapter barrier.
        // The candidate-save callback must never invert those two locks.
        => commandServer.WithSynchronizedState(() => TryReloadRulesetCore(candidateManifest, candidateModules));

    private Devilution.Engine.GameplayRulesetReloadResult<IAuthoritativeGameHostAdapter> TryReloadRulesetCore(
        ContentManifest candidateManifest,
        IEnumerable<Devilution.Engine.IGameplayModule> candidateModules)
    {
        if (CohortStore is not null)
            return TryReloadCohortCore(candidateManifest, candidateModules);
        if (commandScheduler is not null) {
            AdvanceCurrentTickCore();
            if (commandScheduler.PendingCommandCount != 0)
                throw new InvalidOperationException("Ruleset reload is unavailable while commands are queued.");
        }
        if (gameAdapter is not LeasedAuthoritativeGameHostAdapter leasedAdapter)
            throw new InvalidOperationException(
                "Live ruleset replacement requires the leased host adapter.");

        var activeSessions = sessionsByToken.Values
            .Where(session => session.ParticipantInitialized)
            .OrderBy(session => session.Id, StringComparer.Ordinal)
            .ToArray();
        var result = leasedAdapter.TryReload(
            candidateManifest,
            candidateModules,
            activeSessions.Select(session => session.Id).ToArray(),
            candidate => {
                var replacementIdentity = ProtocolServerIdentity.FromRuleset(
                    handshake.Identity.BuildId,
                    handshake.Identity.ProtocolSchemaVersion,
                    handshake.Identity.TickRateHz,
                    candidate.Identity,
                    handshake.Identity.GameMode);
                SaveSessions(
                    activeSessions,
                    candidate.Target,
                    replacementIdentity.RulesetIdentityHash);
            });
        if (result.Succeeded) {
            var replacementIdentity = ProtocolServerIdentity.FromRuleset(
                handshake.Identity.BuildId,
                handshake.Identity.ProtocolSchemaVersion,
                handshake.Identity.TickRateHz,
                result.Active.Identity,
                handshake.Identity.GameMode);
            leasedAdapter.DisposeRetiredAdapters();
            handshake.UpdateIdentity(replacementIdentity);
        }
        return result;
    }

    public void Start()
    {
        lock (lifecycleSynchronization) {
            ObjectDisposedException.ThrowIf(disposal is not null, this);
            StartCore();
        }
    }

    private void StartCore()
    {
        if (cancellation is not null)
            throw new InvalidOperationException("The server has already started.");

        ValidateSessionRestorePolicyForStart();

        var startupCancellation = new CancellationTokenSource();
        try {
            WithCohortBarrier(InitializeCohortHostCore);
            listener.Start();
            cancellation = startupCancellation;
            if (UseClockGatedCommandScheduling)
                clockGatedResponseBudget = new ClockGatedResponseBudget(MaximumPendingCommands);
            if (UseClockGatedCommandScheduling)
                commandScheduler = new ClockGatedCommandScheduler(
                    commandServer, PublishScheduledCommand, MaximumPendingCommands, startupCancellation.Token,
                    CohortStore is null ? null : FenceCohort);
            acceptLoop = RunBackgroundLoopAsync(() => AcceptLoopAsync(startupCancellation.Token), startupCancellation);
            simulationLoop = RunBackgroundLoopAsync(() => SimulationLoopAsync(startupCancellation.Token), startupCancellation);
        } catch {
            listener.Stop();
            commandScheduler?.Dispose();
            commandScheduler = null;
            clockGatedResponseBudget = null;
            startupCancellation.Dispose();
            cancellation = null;
            acceptLoop = null;
            simulationLoop = null;
            throw;
        }
    }

    private void ValidateSessionRestorePolicyForStart()
    {
        if (!Enum.IsDefined(SessionRestorePolicy))
            throw new ArgumentOutOfRangeException(nameof(SessionRestorePolicy));
        if (SessionRestorePolicy != AuthoritativeSessionRestorePolicy.CurrentRulesetOnly)
            return;
        if (CohortStore is not null)
            throw new NotSupportedException(
                "Ruleset-bound session restore is not supported with complete-cohort persistence.");
        if (sessionStore is null || saveProvider is null)
            throw new InvalidOperationException(
                "Ruleset-bound session restore requires a combined session store and save provider.");
        if (string.IsNullOrWhiteSpace(handshake.Identity.RulesetIdentityHash))
            throw new InvalidOperationException(
                "Ruleset-bound session restore requires an active ruleset identity.");
    }

    public ValueTask DisposeAsync()
    {
        lock (lifecycleSynchronization)
            return new ValueTask(disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        var shutdown = cancellation;
        Exception? failure = null;
        try {
            shutdown?.Cancel();
        } catch (Exception exception) {
            RecordShutdownFailure(exception, "cancellation");
        }
        listener.Stop();
        foreach (var client in clients.Keys)
            client.Dispose();
        // Stop accepting before taking the final handler set. Handlers are
        // registered before invocation, including synchronous completion.
        await ObserveAsync(acceptLoop, "accept-loop");
        foreach (var client in clients.Keys)
            client.Dispose();
        await ObserveAsync(simulationLoop, "simulation-loop");
        var handlerIndex = 0;
        foreach (var handler in clientHandlers.Values)
            await ObserveAsync(handler, $"client-handler-{handlerIndex++}");
        clientHandlers.Clear();
        try {
            commandScheduler?.Dispose();
        } catch (Exception exception) {
            RecordShutdownFailure(exception, "scheduler");
        } finally {
            clockGatedResponseBudget = null;
            shutdown?.Dispose();
            cancellation = null;
            acceptLoop = null;
            simulationLoop = null;
        }
        try {
            // Queued transport intents are nondurable. All handlers and the
            // scheduler are quiescent before the final settled publication.
            if (shutdown is not null && failure is null && Volatile.Read(ref cohortFailure) is null)
                WithCohortBarrier(PublishCohortCore);
        } catch (Exception exception) {
            RecordShutdownFailure(exception, "final-publication");
        }
        try {
            if (gameAdapter is LeasedAuthoritativeGameHostAdapter leasedAdapter)
                leasedAdapter.Dispose();
        } catch (Exception exception) {
            RecordShutdownFailure(exception, "game-adapter");
        } finally {
            try { CohortStore?.Dispose(); }
            catch (Exception exception) { RecordShutdownFailure(exception, "cohort-store"); }
        }
        completion.TrySetResult();
        if (failure is not null) {
            if (Volatile.Read(ref cohortFailure) is Exception primary && !ReferenceEquals(primary, failure)) {
                try { primary.Data["CohortShutdownCleanupFailure"] = failure; } catch { }
                ExceptionDispatchInfo.Capture(primary).Throw();
            }
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        void RecordShutdownFailure(Exception exception, string stage)
        {
            failure ??= exception; // Preserve legacy first-failure behavior.
            if (CohortStore is not null && Volatile.Read(ref cohortFailure) is Exception primary
                && !ReferenceEquals(primary, exception)) {
                // Awaited-loop failures must not swallow later resource-cleanup
                // diagnostics. Keep each stage on the original fatal outcome.
                try { primary.Data[$"CohortShutdownCleanupFailure:{stage}"] = exception; }
                catch { }
            }
        }

        async Task ObserveAsync(Task? task, string stage)
        {
            if (task is null)
                return;
            try {
                await task;
            } catch (OperationCanceledException) when (shutdown?.IsCancellationRequested == true) {
            } catch (Exception exception) {
                RecordShutdownFailure(exception, stage);
            }
        }
    }

    private async Task RunBackgroundLoopAsync(Func<Task> run, CancellationTokenSource shutdown)
    {
        try {
            await run();
        } catch (OperationCanceledException) when (shutdown.IsCancellationRequested) {
        } catch (Exception exception) {
            WithCohortBarrier(() => FenceCohort(exception));
            completion.TrySetException(exception);
            shutdown.Cancel();
            throw;
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested) {
            TcpClient client;
            try {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                break;
            } catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
                break;
            }

            clients.TryAdd(client, 0);
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            clientHandlers.TryAdd(client, finished.Task);
            _ = HandleTrackedClientAsync(client, finished, cancellationToken);
        }
    }

    private async Task HandleTrackedClientAsync(TcpClient client, TaskCompletionSource finished, CancellationToken token)
    {
        try {
            await HandleClientAsync(client, token);
            finished.TrySetResult();
            clientHandlers.TryRemove(client, out _);
        } catch (Exception exception) {
            // Retain unexpected handler faults for awaited host cleanup.
            WithCohortBarrier(() => FenceCohort(exception));
            finished.TrySetException(exception);
            completion.TrySetException(exception);
            cancellation?.Cancel();
        }
    }

    private async Task SimulationLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
        while (await timer.WaitForNextTickAsync(cancellationToken)) {
            var measurement = PerformanceRecorder?.Begin() ?? 0;
            var advancement = PerformanceRecorder?.Begin() ?? 0;
            bool advanced;
            IDisposable? authorityTurn = null;
            try {
                if (UseClockGatedCommandScheduling && CohortStore is not null)
                    authorityTurn = await cohortAuthorityTurnGate.EnterAsync(cancellationToken);
                advanced = commandServer.WithSynchronizedState(() => {
                    RequireCohortHealthy();
                    var tick = Math.Max(clock.CurrentTick, commandServer.CurrentTick);
                    if (tick == lastSimulationTick)
                        return false;
                    AdvanceCurrentTickCore();
                    lastSimulationTick = commandServer.CurrentTick;
                    PublishCohortCore();
                    return true;
                });
            } finally {
                authorityTurn?.Dispose();
            }
            if (!advanced)
                continue;
            PerformanceRecorder?.Complete(HostPerformanceMetric.PeriodicAdvanceLockInclusiveMilliseconds, advancement);
            if (CohortStore is null)
                foreach (var session in sessionsByToken.Values)
                    SaveSession(session);
            PerformanceRecorder?.Complete(HostPerformanceMetric.PeriodicAdvanceAndCheckpointMilliseconds, measurement);
        }
    }

    /** Caller holds the authoritative lock; no pre-lock physical clock samples. */
    private ulong AdvanceCurrentTickCore(ulong legacyMinimumTick = 0)
    {
        RequireCohortHealthy();
        try {
            var tick = Math.Max(clock.CurrentTick, commandServer.CurrentTick);
            if (commandScheduler is not null)
                commandScheduler.DrainAndAdvanceTo(tick);
            else
                commandServer.AdvanceTo(Math.Max(tick, legacyMinimumTick));
            return commandServer.CurrentTick;
        } catch (Exception failure) {
            FenceCohort(failure);
            throw;
        }
    }

    private void PublishScheduledCommand(string sessionId, CommandAck result)
    {
        var measurement = PerformanceRecorder?.Begin() ?? 0;
        try {
            AuthoritativeCommandAckValidator.Validate(result);
            if (CohortStore is not null)
                PublishCohortCore();
            else {
                var session = sessionsByToken.Values.Single(session => session.Id == sessionId);
                SaveSession(session);
            }
        } catch (Exception failure) {
            FenceCohort(failure);
            throw;
        }
        PerformanceRecorder?.Complete(HostPerformanceMetric.ScheduledCommandCheckpointMilliseconds, measurement);
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        NetworkStream? stream = null;
        var clockGatedPipelineOwnsWrites = false;
        var connectionOrdinal = CommandTimingRecorder is null
            ? 0
            : unchecked((int)Interlocked.Increment(ref nextConnectionOrdinal));
        try {
            stream = client.GetStream();
            var helloEnvelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
            if (helloEnvelope is null || helloEnvelope.PayloadCase != Envelope.PayloadOneofCase.ClientHello) {
                await SendErrorAsync(stream, ProtocolErrorCode.InvalidMessage, "The first envelope must be a client hello.", cancellationToken);
                return;
            }

            if (UseClockGatedCommandScheduling && !helloEnvelope.ClientHello.SupportsClockGatedCommands) {
                await SendErrorAsync(stream, ProtocolErrorCode.UnsupportedSchema,
                    "This host requires clock-gated command support.", cancellationToken);
                return;
            }

            SessionState session;
            ulong currentTick;
            if (CohortStore is not null) {
                var admission = commandServer.WithSynchronizedState(() =>
                    PrepareCohortHelloCore(helloEnvelope.ClientHello));
                if (admission.Error is not null) {
                    await WriteAuthoritativeEnvelopeAsync(stream, new Envelope { Error = admission.Error }, cancellationToken);
                    return;
                }
                session = admission.Session!;
                currentTick = admission.Tick;
                await WriteAuthoritativeEnvelopeAsync(stream, new Envelope { ServerHello = admission.Hello! }, cancellationToken);
                if (admission.Snapshot is not null)
                    await WriteAuthoritativeEnvelopeAsync(stream, new Envelope { Snapshot = admission.Snapshot }, cancellationToken);
            } else {
                if (SessionRestorePolicy == AuthoritativeSessionRestorePolicy.CurrentRulesetOnly) {
                    StrictSessionAdmission admission;
                    try {
                        admission = commandServer.WithSynchronizedState(
                            () => AdmitRulesetBoundSession(helloEnvelope.ClientHello, connectionOrdinal));
                    } catch (StrictSessionRestoreRejectedException) {
                        await SendErrorAsync(
                            stream,
                            ProtocolErrorCode.InvalidMessage,
                            "The saved session cannot be resumed under the configured restore policy.",
                            cancellationToken);
                        return;
                    } finally {
                        // Strict admission may record a snapshot-start event while holding the
                        // authority boundary; observer callbacks are always drained after release.
                        EmitCommandTimingOutsideAuthorityLock();
                    }
                    if (!admission.HandshakeResult.Accepted) {
                        await EnvelopeCodec.WriteAsync(
                            stream,
                            new Envelope { Error = admission.HandshakeResult.Error! },
                            cancellationToken);
                        return;
                    }
                    session = admission.Session!;
                    currentTick = admission.Tick;
                    await EnvelopeCodec.WriteAsync(
                        stream,
                        new Envelope { ServerHello = admission.Hello! },
                        cancellationToken);
                    if (admission.Snapshot is not null)
                        await WriteCapturedStrictSnapshotAsync(stream, admission, connectionOrdinal, cancellationToken);
                } else {
                    var existingSession = FindExistingSession(helloEnvelope.ClientHello.ResumeToken);
                    var persistedSessionMatchesActiveIdentity = existingSession is null
                        && HasPersistedSessionForActiveIdentity(helloEnvelope.ClientHello.ResumeToken);
                    var handshakeResult = handshake.Validate(
                        helloEnvelope.ClientHello,
                        allowResumedIdentityRefresh: existingSession is not null
                            || persistedSessionMatchesActiveIdentity);
                    if (!handshakeResult.Accepted) {
                        await EnvelopeCodec.WriteAsync(stream, new Envelope { Error = handshakeResult.Error! }, cancellationToken);
                        return;
                    }
                    session = existingSession
                        ?? GetOrCreateSession(helloEnvelope.ClientHello.ResumeToken);
                    PrepareSession(session, helloEnvelope.ClientHello);
                    var serverHello = handshakeResult.ServerHello!.Clone();
                    serverHello.SessionToken = session.Token;
                    serverHello.LastClientSequence = commandServer.WithSynchronizedSession(session.Id, () =>
                        commandScheduler?.LastAdmittedClientSequence(session.Id)
                        ?? commandServer.LastClientSequence(session.Id));
                    serverHello.CommandSchedulingMode = UseClockGatedCommandScheduling
                        ? CommandSchedulingMode.ClockGated : CommandSchedulingMode.Immediate;
                    serverHello.SupportsClockOnlyProbes = UseClockGatedCommandScheduling;

                    await EnvelopeCodec.WriteAsync(stream, new Envelope { ServerHello = serverHello }, cancellationToken);
                    currentTick = commandServer.WithSynchronizedState(() => AdvanceCurrentTickCore());
                    SaveSession(session);
                    await SendSnapshotIfAvailableAsync(
                        stream,
                        session.Id,
                        session.EntityId,
                        currentTick,
                        connectionOrdinal,
                        null,
                        cancellationToken);
                }
            }
            var sessionId = session.Id;
            var entityId = session.EntityId;
            if (commandScheduler is not null) {
                clockGatedPipelineOwnsWrites = true;
                await using var decoratedStream = ClockGatedTransportDecorator?.Invoke(stream);
                await HandleClockGatedConnectionAsync(
                    decoratedStream ?? stream,
                    sessionId,
                    entityId,
                    currentTick,
                    connectionOrdinal,
                    cancellationToken);
                return;
            }
            while (!cancellationToken.IsCancellationRequested) {
                var envelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
                if (envelope is null)
                    return;
                var envelopeReadCompletedTimestamp = CommandTimingRecorder is null
                    ? (long?)null
                    : Stopwatch.GetTimestamp();

                if (envelope.PayloadCase == Envelope.PayloadOneofCase.CommandBatch) {
                    AuthoritativeCommandBatchValidator.Validate(envelope.CommandBatch);
                    var measurement = PerformanceRecorder?.Begin() ?? 0;
                    CommandAck acknowledgement;
                    ulong sampledClockCurrentTick = 0;
                    ulong sampledCommandServerCurrentTick = 0;
                    ulong actualAdmissionTick = 0;
                    long lockWaitStopwatchTicks = 0;
                    IReadOnlyList<long> admissionTraceOrdinals = [];
                    var lockWaitStart = CommandTimingRecorder is null
                        ? 0L
                        : Stopwatch.GetTimestamp();
                    var lockEntered = 0L;
                    acknowledgement = commandServer.WithSynchronizedSession(sessionId, () => {
                            lockEntered = CommandTimingRecorder is null
                                ? 0L
                                : Stopwatch.GetTimestamp();
                            RequireCohortHealthy();
                            try {
                                sampledClockCurrentTick = clock.CurrentTick;
                                sampledCommandServerCurrentTick = commandServer.CurrentTick;
                                actualAdmissionTick = Math.Max(
                                    sampledClockCurrentTick,
                                    sampledCommandServerCurrentTick);
                                currentTick = actualAdmissionTick;
                                var result = commandServer.ProcessBatch(sessionId, envelope.CommandBatch, currentTick);
                                AuthoritativeCommandAckValidator.Validate(result);
                                // Admission, applied-tick advancement and persistence
                                // are one publication. The autonomous loop must not save
                                // a future action against the previous tick in between.
                                if (result.Results.Count != 0)
                                    currentTick = Math.Max(
                                        Math.Max(currentTick, commandServer.CurrentTick),
                                        result.Results.Max(entry => entry.AppliedTick));
                                AdvanceCurrentTickCore(currentTick);
                                SaveSession(session);
                                return result;
                            } catch (Exception failure) {
                                FenceCohort(failure);
                                throw;
                            }
                    });
                    if (lockEntered != 0)
                        lockWaitStopwatchTicks = Math.Max(0, lockEntered - lockWaitStart);
                    PerformanceRecorder?.Complete(HostPerformanceMetric.CommandAdmissionAndCheckpointMilliseconds, measurement);
                    var commandTraceOrdinals = RecordCommandTiming(
                        connectionOrdinal,
                        entityId,
                        envelopeReadCompletedTimestamp,
                        lockWaitStopwatchTicks,
                        sampledClockCurrentTick,
                        sampledCommandServerCurrentTick,
                        actualAdmissionTick,
                        envelope.CommandBatch,
                        acknowledgement,
                        admissionTraceOrdinals);
                    EmitCommandTimingOutsideAuthorityLock();
                    var ackWriteStartTimestamp = CommandTimingRecorder is null
                        ? (long?)null
                        : Stopwatch.GetTimestamp();
                    var ackWriteCompleted = false;
                    try {
                        await WriteAuthoritativeEnvelopeAsync(stream, new Envelope { CommandAck = acknowledgement }, cancellationToken);
                        ackWriteCompleted = true;
                    } finally {
                        var ackWriteEndTimestamp = CommandTimingRecorder is null
                            ? (long?)null
                            : Stopwatch.GetTimestamp();
                        RecordAckWriteTiming(
                            connectionOrdinal,
                            entityId,
                            envelopeReadCompletedTimestamp,
                            acknowledgement,
                            commandTraceOrdinals,
                            ackWriteStartTimestamp,
                            ackWriteEndTimestamp,
                            ackWriteCompleted);
                        EmitCommandTimingOutsideAuthorityLock();
                    }
                    await SendEventsIfAvailable(stream, sessionId, entityId, currentTick, cancellationToken);
                    await SendSnapshotIfAvailableAsync(
                        stream,
                        sessionId,
                        entityId,
                        currentTick,
                        connectionOrdinal,
                        envelopeReadCompletedTimestamp,
                        cancellationToken);
                } else if (envelope.PayloadCase == Envelope.PayloadOneofCase.SnapshotRequest) {
                    currentTick = commandServer.WithSynchronizedState(() => AdvanceAndPublishCohortCore(currentTick));
                    await SendEventsIfAvailable(stream, sessionId, entityId, currentTick, cancellationToken);
                    await SendSnapshotIfAvailableAsync(
                        stream,
                        sessionId,
                        entityId,
                        currentTick,
                        connectionOrdinal,
                        envelopeReadCompletedTimestamp,
                        envelope.SnapshotRequest.TimingRequestId == 0
                            ? null
                            : envelope.SnapshotRequest.TimingRequestId,
                        cancellationToken);
                    if (UseClockGatedCommandScheduling && envelope.SnapshotRequest.TimingRequestId != 0)
                        await WriteAuthoritativeEnvelopeAsync(stream, new Envelope { TimingReply = new TimingReply {
                            RequestId = envelope.SnapshotRequest.TimingRequestId,
                        } }, cancellationToken);
                    if (UseClockGatedCommandScheduling && envelope.SnapshotRequest.TimingRequestId != 0) {
                        RecordTimingReply(
                            connectionOrdinal,
                            entityId,
                            envelopeReadCompletedTimestamp,
                            envelope.SnapshotRequest.TimingRequestId);
                        EmitCommandTimingOutsideAuthorityLock();
                    }
                } else {
                    await SendErrorAsync(stream, ProtocolErrorCode.InvalidMessage, "The session only accepts command batches after the handshake.", cancellationToken);
                    return;
                }
            }
        } catch (EndOfStreamException) {
        } catch (ClockGatedCommandCapacityException exception)
            when (!clockGatedPipelineOwnsWrites) {
            try {
                if (stream is not null)
                    await SendErrorAsync(stream, ProtocolErrorCode.InvalidMessage, exception.Message, cancellationToken);
            } catch (Exception) {
            }
        } catch (InvalidDataException exception)
            when (!clockGatedPipelineOwnsWrites) {
            try {
                if (stream is not null) {
                    await SendErrorAsync(
                        stream,
                        ProtocolErrorCode.InvalidMessage,
                        exception.Message,
                        cancellationToken);
                }
            } catch (Exception) {
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        } catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
        } catch (Exception exception) {
            var clientFailure = exception is ClockGatedTransportException
                && exception.InnerException is { } transportFailure
                ? transportFailure
                : exception;
            if (CohortStore is null) {
                Volatile.Write(ref lastClientFailure, clientFailure);
                ClientFailureObserver?.Invoke(clientFailure); // Preserve legacy observer-fault reporting.
            } else {
                // Peer transport cleanup can fail after fencing. Retain the
                // first fatal cohort diagnostic under the same authority lock.
                WithCohortBarrier(() => Volatile.Write(ref lastClientFailure,
                    Volatile.Read(ref cohortFailure) ?? clientFailure));
                // A diagnostic observer cannot replace the primary cohort failure.
                try { ClientFailureObserver?.Invoke(clientFailure); }
                catch (Exception diagnosticFailure) {
                    try { clientFailure.Data["CohortClientFailureObserver"] = diagnosticFailure; } catch { }
                }
            }
        } finally {
            clients.TryRemove(client, out _);
            client.Dispose();
        }
    }

    private StrictSessionAdmission AdmitRulesetBoundSession(ClientHello hello, int connectionOrdinal)
    {
        var activeIdentity = handshake.Identity;
        if (!string.IsNullOrWhiteSpace(hello.ResumeToken)
            && poisonedSessionTokens.Contains(hello.ResumeToken))
            throw new StrictSessionRestoreRejectedException();

        var existingSession = FindExistingSession(hello.ResumeToken);
        AuthoritativeSessionDocument? document = null;
        if (existingSession is null && !string.IsNullOrWhiteSpace(hello.ResumeToken))
            document = LoadRulesetBoundSessionForAdmission(
                hello.ResumeToken,
                activeIdentity.RulesetIdentityHash);

        var handshakeResult = handshake.Validate(
            hello,
            allowResumedIdentityRefresh: existingSession is not null || document is not null);
        if (!ReferenceEquals(handshake.Identity, activeIdentity))
            throw new InvalidOperationException("The active ruleset identity changed during session admission.");
        if (!handshakeResult.Accepted)
            return new StrictSessionAdmission(null, handshakeResult);

        // Reject a cold restore before allocating a session or advancing queued
        // work. The document remains local to this admission and is not cached.
        if (existingSession is null
            && document is not null
            && commandScheduler is not null
            && commandScheduler.PendingCommandCount != 0)
            throw new StrictSessionRestoreRejectedException();

        var session = existingSession ?? GetOrCreateSession(hello.ResumeToken);
        if (existingSession is null) {
            try {
                PrepareSession(session, hello, document);
            } catch (Exception) {
                // Receipt import and provider import can mutate different shared state. A failed
                // cold preparation therefore fences this token instead of attempting a partial retry.
                poisonedSessionTokens.Add(session.Token);
                throw new StrictSessionRestoreRejectedException();
            }
        } else {
            PrepareSession(session, hello, document);
        }

        var serverHello = handshakeResult.ServerHello!.Clone();
        serverHello.ContentManifestHash = activeIdentity.ContentManifestHash;
        serverHello.RulesetIdentityHash = activeIdentity.RulesetIdentityHash;
        serverHello.SessionToken = session.Token;
        serverHello.LastClientSequence = commandScheduler?.LastAdmittedClientSequence(session.Id)
            ?? commandServer.LastClientSequence(session.Id);
        serverHello.CommandSchedulingMode = UseClockGatedCommandScheduling
            ? CommandSchedulingMode.ClockGated : CommandSchedulingMode.Immediate;
        serverHello.SupportsClockOnlyProbes = UseClockGatedCommandScheduling;


        // Keep the handshake cursor, authoritative tick, and initial projection in the same
        // ruleset generation. Network writes happen only after this authority lock is released.
        var tick = AdvanceCurrentTickCore();
        SaveSession(session);
        Snapshot? snapshot = null;
        StrictSnapshotCapture? snapshotCapture = null;
        if (snapshotProvider is not null) {
            var materialization = PerformanceRecorder?.Begin() ?? 0;
            var materializeStartTimestamp = CommandTimingRecorder is null
                ? (long?)null
                : Stopwatch.GetTimestamp();
            var snapshotTraceOrdinal = CommandTimingRecorder is null
                ? 0L
                : Interlocked.Increment(ref nextTimingOrdinal);
            CommandTimingRecorder?.Record(new HostCommandTimingRecord {
                Kind = "snapshot-start",
                ConnectionOrdinal = connectionOrdinal,
                TraceOrdinal = snapshotTraceOrdinal,
                EntityId = session.EntityId,
                SnapshotMaterializeStartTimestamp = materializeStartTimestamp,
                TimingReplyEmitted = null,
            });
            snapshot = commandServer.WithSynchronizedSession(session.Id, () => {
                RequireCohortHealthy();
                try {
                    tick = AdvanceCurrentTickCore(tick);
                    var result = snapshotProvider.CreateSnapshot(session.Id, session.EntityId, tick).Clone();
                    result.ContentManifestHash = activeIdentity.ContentManifestHash;
                    result.RulesetIdentityHash = activeIdentity.RulesetIdentityHash;
                    AuthoritativeSnapshotValidator.Validate(result);
                    PublishCohortCore();
                    return result;
                } catch (Exception failure) {
                    FenceCohort(failure);
                    throw;
                }
            });
            tick = snapshot.Tick;
            var materializeEndTimestamp = CommandTimingRecorder is null
                ? (long?)null
                : Stopwatch.GetTimestamp();
            snapshotCapture = new StrictSnapshotCapture(
                materialization,
                materializeStartTimestamp,
                materializeEndTimestamp,
                snapshotTraceOrdinal);
        }
        return new StrictSessionAdmission(session, handshakeResult, serverHello, snapshot, tick, snapshotCapture);
    }

    private AuthoritativeSessionDocument? LoadRulesetBoundSessionForAdmission(
        string resumeToken,
        string activeRulesetIdentityHash)
    {
        var sessionId = SessionIdForToken(resumeToken);
        try {
            var document = sessionStore!.LoadForRuleset(
                sessionId,
                activeRulesetIdentityHash);
            if (document is null
                && (saveStore?.Exists(sessionId) == true
                    || commandReceiptStore?.Exists(sessionId) == true))
                throw new StrictSessionRestoreRejectedException();
            return document;
        } catch (StrictSessionRestoreRejectedException) {
            throw;
        } catch (Exception exception) when (
            exception is InvalidDataException or IOException or UnauthorizedAccessException) {
            throw new StrictSessionRestoreRejectedException();
        }
    }

    private void PrepareSession(
        SessionState session,
        ClientHello hello,
        AuthoritativeSessionDocument? rulesetBoundDocument = null)
    {
        lock (session) {
            commandServer.WithSynchronizedSession(session.Id, () => {
                if (commandScheduler is not null) {
                    if (SessionRestorePolicy == AuthoritativeSessionRestorePolicy.CurrentRulesetOnly
                        && !session.SaveLoaded
                        && rulesetBoundDocument is not null
                        && commandScheduler.PendingCommandCount != 0)
                        throw new InvalidDataException("Cold session restore is unavailable while commands are queued.");
                    AdvanceCurrentTickCore();
                    // Import may replace shared world state, not just this player.
                    // Fence before ledger import, initialization or provider mutation.
                    if (!session.SaveLoaded && saveProvider is not null
                        && commandScheduler.PendingCommandCount != 0) {
                        var hasRestorableState = SessionRestorePolicy
                            == AuthoritativeSessionRestorePolicy.CurrentRulesetOnly
                            ? rulesetBoundDocument is not null
                            : (sessionStore?.Load(session.Id, handshake.Identity.RulesetIdentityHash)?.SerializedState
                                ?? saveStore?.Load(session.Id)) is not null;
                        if (hasRestorableState)
                            throw new InvalidDataException("Cold session restore is unavailable while commands are queued.");
                    }
                }
                if (SessionRestorePolicy == AuthoritativeSessionRestorePolicy.CurrentRulesetOnly
                    && !session.SessionDocumentLoaded) {
                    session.SessionDocument = rulesetBoundDocument;
                    session.SessionDocumentLoaded = true;
                }
                EnsureCommandReceiptsLoaded(session);
                if (!RestoreSessionIfAvailable(session, rulesetBoundDocument))
                    InitializeParticipantIfNeeded(session, hello);
            });
        }
    }

    private SessionState GetOrCreateSession(string resumeToken)
    {
        lock (sessionCreationSynchronization) {
            if (!string.IsNullOrWhiteSpace(resumeToken)
                && sessionsByToken.TryGetValue(resumeToken, out var resumed))
                return resumed;

            var token = string.IsNullOrWhiteSpace(resumeToken)
                ? Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant()
                : resumeToken;
            var sessionId = SessionIdForToken(token);
            var session = new SessionState(sessionId, token, entityIds.Allocate());
            sessionsByToken.TryAdd(session.Token, session);
            return session;
        }
    }

    private void InitializeParticipantIfNeeded(SessionState session, ClientHello hello)
    {
        lock (session) {
            if (session.ParticipantInitialized)
                return;
            var requestedClassId = hello.HasRequestedCharacterClassId
                ? hello.RequestedCharacterClassId
                : (uint?)null;
            var requestedCharacterName = hello.HasRequestedCharacterName
                ? hello.RequestedCharacterName
                : null;
            commandServer.WithSynchronizedSession(session.Id, () => {
                participantInitializer?.InitializeParticipant(
                    session.Id,
                    new AuthoritativeParticipantSelection(requestedClassId) {
                        CharacterName = requestedCharacterName,
                    });
                session.ParticipantInitialized = true;
            });
        }
    }

    private bool RestoreSessionIfAvailable(
        SessionState session,
        AuthoritativeSessionDocument? rulesetBoundDocument = null)
    {
        lock (session) {
            if (session.SaveLoaded || saveProvider is null)
                return false;
            var serializedSave = SessionRestorePolicy == AuthoritativeSessionRestorePolicy.CurrentRulesetOnly
                ? rulesetBoundDocument?.SerializedState
                : LoadSessionDocument(session)?.SerializedState ?? saveStore?.Load(session.Id);
            if (serializedSave is null) {
                session.SaveLoaded = true;
                return false;
            }
            commandServer.WithSynchronizedSession(session.Id, () => {
                saveProvider.ImportSessionState(session.Id, serializedSave);
                var restoredTick = saveProvider.GetSessionStateTick(session.Id);
                if (clock is Devilution.Engine.IResumableAuthoritativeClock resumableClock)
                    resumableClock.RestoreAtTick(restoredTick);
                if (commandScheduler is not null)
                    commandScheduler.DrainAndAdvanceTo(Math.Max(restoredTick, clock.CurrentTick));
                else
                    commandServer.AdvanceTo(restoredTick);
                session.SaveLoaded = true;
                session.ParticipantInitialized = true;
            });
            return true;
        }
    }

    private void SaveSession(
        SessionState session,
        IAuthoritativeSaveProvider? stateProvider = null,
        string? rulesetIdentityHash = null)
    {
        if (CohortStore is not null) {
            WithCohortBarrier(PublishCohortCore);
            return;
        }
        var lockWait = PerformanceRecorder?.Begin() ?? 0;
        commandServer.WithSynchronizedSession(session.Id, () => {
            PerformanceRecorder?.Complete(HostPerformanceMetric.SessionCheckpointLockWaitMilliseconds, lockWait);
            if (!session.ParticipantInitialized)
                return;
            var capture = PerformanceRecorder?.Begin() ?? 0;
            var provider = stateProvider ?? saveProvider;
            var serializedState = provider is null
                ? null
                : provider.ExportSessionState(session.Id);
            var completedCommands = commandServer.ExportCompletedCommands(session.Id);
            PerformanceRecorder?.Complete(HostPerformanceMetric.SessionStateAndLedgerCaptureMilliseconds, capture);
            sessionPersistence.Save(
                session.Id,
                serializedState,
                completedCommands,
                rulesetIdentityHash ?? handshake.Identity.RulesetIdentityHash,
                PerformanceRecorder);
        });
    }

    private SessionState? FindExistingSession(string resumeToken)
    {
        if (string.IsNullOrWhiteSpace(resumeToken))
            return null;
        lock (sessionCreationSynchronization) {
            return sessionsByToken.TryGetValue(resumeToken, out var session)
                && session.ParticipantInitialized
                ? session
                : null;
        }
    }

    private void SaveSessions(
        IEnumerable<SessionState> sessions,
        IAuthoritativeSaveProvider stateProvider,
        string rulesetIdentityHash)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(stateProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(rulesetIdentityHash);

        var prepared = sessions
            .Where(session => session.ParticipantInitialized)
            .OrderBy(session => session.Id, StringComparer.Ordinal)
            .Select(session => commandServer.WithSynchronizedSession(
                session.Id,
                () => new PreparedSessionSave(
                    session.Id,
                    stateProvider.ExportSessionState(session.Id),
                    commandServer.ExportCompletedCommands(session.Id))))
            .ToArray();
        sessionPersistence.SaveBatch(prepared.Select(save => new AuthoritativeSessionWrite(
            save.SessionId,
            save.SerializedState,
            save.CommandResults,
            rulesetIdentityHash)));
    }

    private void EnsureCommandReceiptsLoaded(SessionState session)
    {
        if (commandReceiptStore is null && sessionStore is null)
            return;
        lock (session) {
            if (session.CommandReceiptsLoaded)
                return;
            var document = LoadSessionDocument(session);
            var receipts = SessionRestorePolicy == AuthoritativeSessionRestorePolicy.CurrentRulesetOnly
                ? document?.CommandReceipts ?? []
                : document is not null
                    ? document.CommandReceipts
                    : commandReceiptStore?.Load(session.Id) ?? [];
            commandServer.ImportCompletedCommands(session.Id, receipts);
            session.CommandReceiptsLoaded = true;
        }
    }

    private AuthoritativeSessionDocument? LoadSessionDocument(SessionState session)
    {
        if (session.SessionDocumentLoaded)
            return session.SessionDocument;
        if (SessionRestorePolicy == AuthoritativeSessionRestorePolicy.CurrentRulesetOnly)
            throw new InvalidOperationException(
                "Ruleset-bound session loading must complete before session preparation.");
        session.SessionDocument = sessionStore?.Load(
            session.Id,
            handshake.Identity.RulesetIdentityHash);
        session.SessionDocumentLoaded = true;
        return session.SessionDocument;
    }

    private static ValueTask SendErrorAsync(Stream stream, ProtocolErrorCode code, string detail, CancellationToken cancellationToken)
    {
        return EnvelopeCodec.WriteAsync(stream, new Envelope {
            Error = new ProtocolError { Code = code, Detail = detail },
        }, cancellationToken);
    }

    private sealed class SessionState(string id, string token, uint entityId)
    {
        private int participantInitialized;

        public string Id { get; } = id;
        public string Token { get; set; } = token;
        public uint EntityId { get; } = entityId;
        public bool SaveLoaded { get; set; }
        public bool CommandReceiptsLoaded { get; set; }
        public bool SessionDocumentLoaded { get; set; }
        public AuthoritativeSessionDocument? SessionDocument { get; set; }
        public bool ParticipantInitialized
        {
            get => Volatile.Read(ref participantInitialized) != 0;
            set => Volatile.Write(ref participantInitialized, value ? 1 : 0);
        }
    }

    private readonly record struct StrictSessionAdmission(
        SessionState? Session,
        HandshakeResult HandshakeResult,
        ServerHello? Hello = null,
        Snapshot? Snapshot = null,
        ulong Tick = 0,
        StrictSnapshotCapture? SnapshotCapture = null);

    private readonly record struct StrictSnapshotCapture(
        long Materialization,
        long? MaterializeStartTimestamp,
        long? MaterializeEndTimestamp,
        long TraceOrdinal);

    private async ValueTask WriteCapturedStrictSnapshotAsync(
        Stream stream,
        StrictSessionAdmission admission,
        int connectionOrdinal,
        CancellationToken cancellationToken)
    {
        var snapshot = admission.Snapshot!;
        var session = admission.Session!;
        var capture = admission.SnapshotCapture!.Value;
        PerformanceRecorder?.Complete(
            HostPerformanceMetric.SnapshotMaterializeMilliseconds,
            capture.Materialization);
        var envelope = new Envelope { Snapshot = snapshot };
        var publication = PerformanceRecorder?.Begin() ?? 0;
        var writeStartTimestamp = CommandTimingRecorder is null
            ? (long?)null
            : Stopwatch.GetTimestamp();
        if (CommandTimingRecorder is not null) {
            CommandTimingRecorder.Record(new HostCommandTimingRecord {
                Kind = "snapshot-write-start",
                ConnectionOrdinal = connectionOrdinal,
                TraceOrdinal = capture.TraceOrdinal,
                EntityId = session.EntityId,
                SnapshotMaterializeStartTimestamp = capture.MaterializeStartTimestamp,
                SnapshotMaterializeEndTimestamp = capture.MaterializeEndTimestamp,
                SnapshotWriteStartTimestamp = writeStartTimestamp,
                SnapshotTick = snapshot.Tick,
                TimingReplyEmitted = null,
            });
            EmitCommandTimingOutsideAuthorityLock();
        }

        await WriteAuthoritativeEnvelopeAsync(stream, envelope, cancellationToken);
        var writeEndTimestamp = CommandTimingRecorder is null
            ? (long?)null
            : Stopwatch.GetTimestamp();
        PerformanceRecorder?.Complete(HostPerformanceMetric.SnapshotWriteMilliseconds, publication);
        if (publication != 0)
            PerformanceRecorder!.Record(HostPerformanceMetric.SnapshotEnvelopePayloadBytes, envelope.CalculateSize());
        if (CommandTimingRecorder is not null) {
            CommandTimingRecorder.Record(new HostCommandTimingRecord {
                Kind = "snapshot",
                ConnectionOrdinal = connectionOrdinal,
                TraceOrdinal = capture.TraceOrdinal,
                EntityId = session.EntityId,
                SnapshotMaterializeStartTimestamp = capture.MaterializeStartTimestamp,
                SnapshotMaterializeEndTimestamp = capture.MaterializeEndTimestamp,
                SnapshotWriteStartTimestamp = writeStartTimestamp,
                SnapshotWriteEndTimestamp = writeEndTimestamp,
                SnapshotTick = snapshot.Tick,
                TimingReplyEmitted = null,
            });
            EmitCommandTimingOutsideAuthorityLock();
        }
    }

    private sealed class StrictSessionRestoreRejectedException : Exception
    {
    }

    /**
     * Allows a persisted resumed session to adopt the active identity after a
     * process restart, but only when its checkpoint was already published for
     * that exact identity. Unknown tokens and checkpoints from another
     * generation still take the strict handshake path.
     */
    private bool HasPersistedSessionForActiveIdentity(string resumeToken)
    {
        if (string.IsNullOrWhiteSpace(resumeToken)
            || sessionStore is null
            || saveProvider is null)
            return false;

        var sessionId = SessionIdForToken(resumeToken);
        var document = sessionStore.Load(sessionId);
        return document is not null
            && string.Equals(
                document.RulesetIdentityHash,
                handshake.Identity.RulesetIdentityHash,
                StringComparison.Ordinal);
    }

    private static string SessionIdForToken(string resumeToken)
        => Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(resumeToken)))
            .ToLowerInvariant();

    private sealed record PreparedSessionSave(
        string SessionId,
        string SerializedState,
        IReadOnlyDictionary<ulong, CommandResult> CommandResults);

    private IReadOnlyList<long> RecordCommandTiming(
        int connectionOrdinal,
        uint entityId,
        long? envelopeReadCompletedTimestamp,
        long lockWaitStopwatchTicks,
        ulong sampledClockCurrentTick,
        ulong sampledCommandServerCurrentTick,
        ulong actualAdmissionTick,
        CommandBatch batch,
        CommandAck acknowledgement,
        IReadOnlyList<long> admissionTraceOrdinals)
    {
        var recorder = CommandTimingRecorder;
        if (recorder is null)
            return [];

        var traceOrdinals = new List<long>(acknowledgement.Results.Count);
        for (var index = 0; index < acknowledgement.Results.Count; index++) {
            var result = acknowledgement.Results[index];
            var traceOrdinal = index < admissionTraceOrdinals.Count
                ? admissionTraceOrdinals[index]
                : Interlocked.Increment(ref nextTimingOrdinal);
            traceOrdinals.Add(traceOrdinal);
            var requestedTick = index < batch.Commands.Count
                ? batch.Commands[index].RequestedTick
                : (ulong?)null;
            recorder.Record(new HostCommandTimingRecord {
                Kind = "command",
                ConnectionOrdinal = connectionOrdinal,
                TraceOrdinal = traceOrdinal,
                EntityId = entityId,
                ClientSequence = result.ClientSequence,
                EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
                AuthorityLockWaitStopwatchTicks = lockWaitStopwatchTicks,
                SampledClockCurrentTick = sampledClockCurrentTick,
                SampledCommandServerCurrentTick = sampledCommandServerCurrentTick,
                ActualAdmissionTick = actualAdmissionTick,
                RequestedTick = requestedTick,
                AckStatus = result.Status.ToString(),
                AckReason = result.RejectReason.ToString(),
                ReceiptSequence = result.ServerReceiptSequence,
                Status = result.Status.ToString(),
                RejectReason = result.RejectReason.ToString(),
                ServerReceiptSequence = result.ServerReceiptSequence,
                AppliedTick = result.AppliedTick,
            });
        }
        return traceOrdinals;
    }

    private void RecordAckWriteTiming(
        int connectionOrdinal,
        uint entityId,
        long? envelopeReadCompletedTimestamp,
        CommandAck acknowledgement,
        IReadOnlyList<long> commandTraceOrdinals,
        long? writeStartTimestamp,
        long? writeEndTimestamp,
        bool writeCompleted)
    {
        var recorder = CommandTimingRecorder;
        if (recorder is null)
            return;
        for (var index = 0; index < acknowledgement.Results.Count; index++) {
            var result = acknowledgement.Results[index];
            recorder.Record(new HostCommandTimingRecord {
                Kind = "command-ack-write",
                ConnectionOrdinal = connectionOrdinal,
                TraceOrdinal = index < commandTraceOrdinals.Count
                    ? commandTraceOrdinals[index]
                    : Interlocked.Increment(ref nextTimingOrdinal),
                EntityId = entityId,
                ClientSequence = result.ClientSequence,
                EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
                AckWriteStartTimestamp = writeStartTimestamp,
                AckWriteEndTimestamp = writeEndTimestamp,
                AckWriteCompleted = writeCompleted,
            });
        }
    }

    private IReadOnlyList<long> RecordCommandAdmissionTiming(
        int connectionOrdinal,
        uint entityId,
        long? envelopeReadCompletedTimestamp,
        long lockWaitStopwatchTicks,
        ulong sampledClockCurrentTick,
        ulong sampledCommandServerCurrentTick,
        ulong actualAdmissionTick,
        CommandBatch batch)
    {
        var recorder = CommandTimingRecorder;
        if (recorder is null)
            return [];
        var traceOrdinals = new List<long>(batch.Commands.Count);
        foreach (var command in batch.Commands) {
            var traceOrdinal = Interlocked.Increment(ref nextTimingOrdinal);
            traceOrdinals.Add(traceOrdinal);
            recorder.Record(new HostCommandTimingRecord {
                Kind = "command-admission",
                ConnectionOrdinal = connectionOrdinal,
                TraceOrdinal = traceOrdinal,
                EntityId = entityId,
                ClientSequence = command.ClientSequence,
                EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
                AuthorityLockWaitStopwatchTicks = lockWaitStopwatchTicks,
                SampledClockCurrentTick = sampledClockCurrentTick,
                SampledCommandServerCurrentTick = sampledCommandServerCurrentTick,
                ActualAdmissionTick = actualAdmissionTick,
                RequestedTick = command.RequestedTick,
            });
        }
        return traceOrdinals;
    }

    private void RecordTimingReply(
        int connectionOrdinal,
        uint entityId,
        long? envelopeReadCompletedTimestamp,
        ulong requestId)
    {
        var recorder = CommandTimingRecorder;
        if (recorder is null)
            return;
        recorder.Record(new HostCommandTimingRecord {
            Kind = "timing-reply",
            ConnectionOrdinal = connectionOrdinal,
            TraceOrdinal = Interlocked.Increment(ref nextTimingOrdinal),
            EntityId = entityId,
            EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
            TimingRequestId = requestId,
            TimingReplyEmitted = true,
        });
    }

    /** Invoked only after the request's authoritative work has left its lock. */
    private void EmitCommandTimingOutsideAuthorityLock()
    {
        var recorder = CommandTimingRecorder;
        var observer = CommandTimingDrainObserver;
        if (recorder is null || observer is null)
            return;
        var drain = recorder.Drain();
        if (drain.Records.Count == 0 && drain.DroppedCount == 0 && !drain.CaptureFaulted)
            return;
        try {
            observer(drain);
        } catch {
            // Diagnostic output must never mask a gameplay or transport result.
            recorder.MarkObserverFailure();
        }
    }

    private async ValueTask SendSnapshotIfAvailableAsync(
        Stream stream,
        string sessionId,
        uint entityId,
        ulong tick,
        int connectionOrdinal,
        long? envelopeReadCompletedTimestamp,
        CancellationToken cancellationToken,
        bool classifyClockedTransportFailure = false,
        CancellationToken hostCancellationToken = default)
    {
        await SendSnapshotIfAvailableAsync(
            stream,
            sessionId,
            entityId,
            tick,
            connectionOrdinal,
            envelopeReadCompletedTimestamp,
            null,
            cancellationToken,
            classifyClockedTransportFailure,
            hostCancellationToken);
    }

    private async ValueTask SendSnapshotIfAvailableAsync(
        Stream stream,
        string sessionId,
        uint entityId,
        ulong tick,
        int connectionOrdinal,
        long? envelopeReadCompletedTimestamp,
        ulong? timingRequestId,
        CancellationToken cancellationToken,
        bool classifyClockedTransportFailure = false,
        CancellationToken hostCancellationToken = default)
    {
        if (snapshotProvider is null)
            return;

        RequireClockedTransportActive(classifyClockedTransportFailure, cancellationToken);
        var materialization = PerformanceRecorder?.Begin() ?? 0;
        var materializeStartTimestamp = CommandTimingRecorder is null
            ? (long?)null
            : Stopwatch.GetTimestamp();
        var snapshotTraceOrdinal = CommandTimingRecorder is null
            ? 0L
            : Interlocked.Increment(ref nextTimingOrdinal);
        if (CommandTimingRecorder is not null) {
            // Publish the start before entering the authority monitor so a
            // forced cleanup still retains evidence of an incomplete capture.
            CommandTimingRecorder.Record(new HostCommandTimingRecord {
                Kind = "snapshot-start",
                ConnectionOrdinal = connectionOrdinal,
                TraceOrdinal = snapshotTraceOrdinal,
                EntityId = entityId,
                EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
                SnapshotMaterializeStartTimestamp = materializeStartTimestamp,
                TimingRequestId = timingRequestId,
                TimingReplyEmitted = timingRequestId.HasValue ? false : null,
            });
            EmitCommandTimingOutsideAuthorityLock();
        }

        // Snapshot materialization is part of the authoritative state
        // boundary. Do not allow a concurrent command or autonomous tick to
        // mutate the module while the projection is being assembled.
        var snapshot = commandServer.WithSynchronizedSession(sessionId, () => {
            RequireClockedTransportActive(classifyClockedTransportFailure, cancellationToken);
            RequireCohortHealthy();
            try {
            // An autonomous tick may advance while the caller awaits network
            // writes. Never label newer action state with that stale timestamp.
            tick = AdvanceCurrentTickCore(tick);
            var result = snapshotProvider.CreateSnapshot(sessionId, entityId, tick);
            var currentIdentity = handshake.Identity;
            result.ContentManifestHash = currentIdentity.ContentManifestHash;
            result.RulesetIdentityHash = currentIdentity.RulesetIdentityHash;
            AuthoritativeSnapshotValidator.Validate(result);
            PublishCohortCore();
            return result;
            } catch (Exception failure) {
                FenceCohort(failure);
                throw;
            }
        });
        var materializeEndTimestamp = CommandTimingRecorder is null
            ? (long?)null
            : Stopwatch.GetTimestamp();
        PerformanceRecorder?.Complete(HostPerformanceMetric.SnapshotMaterializeMilliseconds, materialization);
        await WriteSnapshotEnvelopeAsync(
            stream,
            snapshot,
            connectionOrdinal,
            entityId,
            envelopeReadCompletedTimestamp,
            timingRequestId,
            materializeStartTimestamp,
            materializeEndTimestamp,
            snapshotTraceOrdinal,
            cancellationToken,
            classifyClockedTransportFailure);
    }

    private async ValueTask WriteSnapshotEnvelopeAsync(
        Stream stream,
        Snapshot snapshot,
        int connectionOrdinal,
        uint entityId,
        long? envelopeReadCompletedTimestamp,
        ulong? timingRequestId,
        long? materializeStartTimestamp,
        long? materializeEndTimestamp,
        long snapshotTraceOrdinal,
        CancellationToken cancellationToken,
        bool classifyClockedTransportFailure)
    {
        var envelope = new Envelope { Snapshot = snapshot };
        var publication = PerformanceRecorder?.Begin() ?? 0;
        var writeStartTimestamp = CommandTimingRecorder is null
            ? (long?)null
            : Stopwatch.GetTimestamp();
        if (CommandTimingRecorder is not null) {
            CommandTimingRecorder.Record(new HostCommandTimingRecord {
                Kind = "snapshot-write-start",
                ConnectionOrdinal = connectionOrdinal,
                TraceOrdinal = snapshotTraceOrdinal,
                EntityId = entityId,
                EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
                SnapshotMaterializeStartTimestamp = materializeStartTimestamp,
                SnapshotMaterializeEndTimestamp = materializeEndTimestamp,
                SnapshotWriteStartTimestamp = writeStartTimestamp,
                SnapshotTick = snapshot.Tick,
                TimingRequestId = timingRequestId,
                TimingReplyEmitted = timingRequestId.HasValue ? false : null,
            });
            EmitCommandTimingOutsideAuthorityLock();
        }
        try {
            await WriteAuthoritativeEnvelopeAsync(stream, envelope, cancellationToken);
        } catch (Exception exception)
            when (classifyClockedTransportFailure
                && (exception is IOException or SocketException or ObjectDisposedException)
                && exception is not ClockGatedTransportException) {
            throw new ClockGatedTransportException(exception);
        } catch (OperationCanceledException exception)
            when (classifyClockedTransportFailure
                && cancellationToken.IsCancellationRequested) {
            throw new ClockGatedTransportException(exception);
        }
        var writeEndTimestamp = CommandTimingRecorder is null
            ? (long?)null
            : Stopwatch.GetTimestamp();
        PerformanceRecorder?.Complete(HostPerformanceMetric.SnapshotWriteMilliseconds, publication);
        if (publication != 0)
            PerformanceRecorder!.Record(HostPerformanceMetric.SnapshotEnvelopePayloadBytes, envelope.CalculateSize());
        if (CommandTimingRecorder is not null) {
            CommandTimingRecorder.Record(new HostCommandTimingRecord {
                Kind = "snapshot",
                ConnectionOrdinal = connectionOrdinal,
                TraceOrdinal = snapshotTraceOrdinal,
                EntityId = entityId,
                EnvelopeReadCompletedTimestamp = envelopeReadCompletedTimestamp,
                SnapshotMaterializeStartTimestamp = materializeStartTimestamp,
                SnapshotMaterializeEndTimestamp = materializeEndTimestamp,
                SnapshotWriteStartTimestamp = writeStartTimestamp,
                SnapshotWriteEndTimestamp = writeEndTimestamp,
                SnapshotTick = snapshot.Tick,
                TimingRequestId = timingRequestId,
                TimingReplyEmitted = timingRequestId.HasValue ? false : null,
            });
            EmitCommandTimingOutsideAuthorityLock();
        }
    }

    private ValueTask SendEventsIfAvailable(
        Stream stream,
        string sessionId,
        uint entityId,
        ulong tick,
        CancellationToken cancellationToken,
        bool classifyClockedTransportFailure = false,
        CancellationToken hostCancellationToken = default)
    {
        // Drain at a current authoritative boundary before the next snapshot.
        // Refresh and drain under the authoritative lock, then
        // serialize outside the lock so network backpressure cannot stall the
        // simulation.
        var events = eventProvider is null
            ? null
            : commandServer.WithSynchronizedSession(
                sessionId,
                () => {
                    RequireClockedTransportActive(classifyClockedTransportFailure, cancellationToken);
                    RequireCohortHealthy();
                    try {
                    tick = AdvanceCurrentTickCore(tick);
                    var result = eventProvider.DrainEvents(sessionId, entityId, tick);
                    if (result is not null)
                        AuthoritativeEventBatchValidator.Validate(result, tick);
                    PublishCohortCore();
                    return result;
                    } catch (Exception failure) {
                        FenceCohort(failure);
                        throw;
                    }
                });
        return events is null || events.Events.Count == 0
            ? ValueTask.CompletedTask
            : WriteEventBatchAsync(
                stream,
                events,
                tick,
                cancellationToken,
                classifyClockedTransportFailure,
                hostCancellationToken);
    }

    private ValueTask WriteEventBatchAsync(
        Stream stream,
        EventBatch events,
        ulong expectedTick,
        CancellationToken cancellationToken,
        bool classifyClockedTransportFailure = false,
        CancellationToken hostCancellationToken = default)
    {
        AuthoritativeEventBatchValidator.Validate(events, expectedTick);
        return WriteClockedEventBatchAsync(
            stream,
            events,
            cancellationToken,
            classifyClockedTransportFailure,
            hostCancellationToken);
    }

    private async ValueTask WriteClockedEventBatchAsync(
        Stream stream,
        EventBatch events,
        CancellationToken cancellationToken,
        bool classifyClockedTransportFailure,
        CancellationToken hostCancellationToken)
    {
        try {
            await WriteAuthoritativeEnvelopeAsync(
                stream,
                new Envelope { EventBatch = events },
                cancellationToken);
        } catch (Exception exception)
            when (classifyClockedTransportFailure
                && (exception is IOException or SocketException or ObjectDisposedException)
                && exception is not ClockGatedTransportException) {
            throw new ClockGatedTransportException(exception);
        } catch (OperationCanceledException exception)
            when (classifyClockedTransportFailure
                && cancellationToken.IsCancellationRequested) {
            throw new ClockGatedTransportException(exception);
        }
    }

    private ValueTask WriteAuthoritativeEnvelopeAsync(Stream stream, Envelope envelope, CancellationToken cancellationToken)
    {
        WithCohortBarrier(RequireCohortHealthy);
        return EnvelopeCodec.WriteAsync(stream, envelope, cancellationToken);
    }

    private void WithCohortBarrier(Action operation)
        => commandServer.WithSynchronizedState(() => { operation(); return 0; });
}
