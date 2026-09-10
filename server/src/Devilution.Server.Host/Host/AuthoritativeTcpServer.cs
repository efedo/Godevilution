using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
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
public sealed class AuthoritativeTcpServer : IAsyncDisposable
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
    private readonly ConcurrentDictionary<string, SessionState> sessionsByToken = new(StringComparer.Ordinal);
    private readonly object sessionCreationSynchronization = new();
    private CancellationTokenSource? cancellation;
    private Task? acceptLoop;
    private Task? simulationLoop;
    private ulong lastSimulationTick;

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

    /** Combined identity hash advertised by the current handshake. */
    public string ActiveRulesetIdentityHash => handshake.Identity.RulesetIdentityHash;

    /** Replaced generations awaiting host disposal, if live reload is enabled. */
    public int RetiredRulesetCount => gameAdapter is LeasedAuthoritativeGameHostAdapter leasedAdapter
        ? leasedAdapter.RetiredRulesetCount
        : 0;

    /** Returns initialized sessions in deterministic identity order. */
    public IReadOnlyList<string> ActiveSessionIds => sessionsByToken.Values
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
    {
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
        if (cancellation is not null)
            throw new InvalidOperationException("The server has already started.");

        var startupCancellation = new CancellationTokenSource();
        try {
            listener.Start();
            cancellation = startupCancellation;
            acceptLoop = AcceptLoopAsync(startupCancellation.Token);
            simulationLoop = SimulationLoopAsync(startupCancellation.Token);
        } catch {
            listener.Stop();
            startupCancellation.Dispose();
            cancellation = null;
            acceptLoop = null;
            simulationLoop = null;
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (cancellation is null) {
            if (gameAdapter is LeasedAuthoritativeGameHostAdapter unstartedLeasedAdapter)
                unstartedLeasedAdapter.Dispose();
            return;
        }

        cancellation.Cancel();
        listener.Stop();
        foreach (var client in clients.Keys)
            client.Dispose();

        if (acceptLoop is not null) {
            try {
                await acceptLoop;
            } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
            }
        }
        if (simulationLoop is not null) {
            try {
                await simulationLoop;
            } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
            }
        }

        cancellation.Dispose();
        cancellation = null;
        acceptLoop = null;
        simulationLoop = null;
        if (gameAdapter is LeasedAuthoritativeGameHostAdapter leasedAdapter)
            leasedAdapter.Dispose();
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
            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task SimulationLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
        while (await timer.WaitForNextTickAsync(cancellationToken)) {
            var tick = Math.Max(clock.CurrentTick, commandServer.CurrentTick);
            if (tick == lastSimulationTick)
                continue;
            commandServer.AdvanceTo(tick);
            lastSimulationTick = tick;
            foreach (var session in sessionsByToken.Values)
                SaveSession(session);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        NetworkStream? stream = null;
        try {
            stream = client.GetStream();
            var helloEnvelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
            if (helloEnvelope is null || helloEnvelope.PayloadCase != Envelope.PayloadOneofCase.ClientHello) {
                await SendErrorAsync(stream, ProtocolErrorCode.InvalidMessage, "The first envelope must be a client hello.", cancellationToken);
                return;
            }

            var handshakeResult = handshake.Validate(helloEnvelope.ClientHello);
            if (!handshakeResult.Accepted) {
                await EnvelopeCodec.WriteAsync(stream, new Envelope { Error = handshakeResult.Error! }, cancellationToken);
                return;
            }

            var session = GetOrCreateSession(helloEnvelope.ClientHello.ResumeToken);
            EnsureCommandReceiptsLoaded(session);
            var restored = RestoreSessionIfAvailable(session);
            if (!restored)
                InitializeParticipantIfNeeded(session, helloEnvelope.ClientHello);
            var serverHello = handshakeResult.ServerHello!.Clone();
            serverHello.SessionToken = session.Token;
            await EnvelopeCodec.WriteAsync(stream, new Envelope { ServerHello = serverHello }, cancellationToken);
            var sessionId = session.Id;
            var entityId = session.EntityId;
            var currentTick = Math.Max(clock.CurrentTick, commandServer.CurrentTick);
            commandServer.AdvanceTo(currentTick);
            SaveSession(session);
            await SendSnapshotIfAvailableAsync(stream, sessionId, entityId, currentTick, cancellationToken);
            while (!cancellationToken.IsCancellationRequested) {
                var envelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
                if (envelope is null)
                    return;

                if (envelope.PayloadCase == Envelope.PayloadOneofCase.CommandBatch) {
                    AuthoritativeCommandBatchValidator.Validate(envelope.CommandBatch);
                    currentTick = Math.Max(clock.CurrentTick, commandServer.CurrentTick);
                    var acknowledgement = commandServer.ProcessBatch(sessionId, envelope.CommandBatch, currentTick);
                    AuthoritativeCommandAckValidator.Validate(acknowledgement);
                    // A client may submit a command for the next deterministic
                    // tick. Publish the snapshot at that applied boundary so a
                    // newly started action is never serialized as future state.
                    if (acknowledgement.Results.Count != 0)
                        currentTick = Math.Max(
                            Math.Max(currentTick, commandServer.CurrentTick),
                            acknowledgement.Results.Max(result => result.AppliedTick));
                    commandServer.AdvanceTo(currentTick);
                    SaveSession(session);
                    await EnvelopeCodec.WriteAsync(stream, new Envelope { CommandAck = acknowledgement }, cancellationToken);
                    await SendEventsIfAvailable(stream, sessionId, entityId, currentTick, cancellationToken);
                    await SendSnapshotIfAvailableAsync(stream, sessionId, entityId, currentTick, cancellationToken);
                } else if (envelope.PayloadCase == Envelope.PayloadOneofCase.SnapshotRequest) {
                    currentTick = Math.Max(
                        currentTick,
                        Math.Max(clock.CurrentTick, commandServer.CurrentTick));
                    commandServer.AdvanceTo(currentTick);
                    await SendEventsIfAvailable(stream, sessionId, entityId, currentTick, cancellationToken);
                    await SendSnapshotIfAvailableAsync(stream, sessionId, entityId, currentTick, cancellationToken);
                } else {
                    await SendErrorAsync(stream, ProtocolErrorCode.InvalidMessage, "The session only accepts command batches after the handshake.", cancellationToken);
                    return;
                }
            }
        } catch (EndOfStreamException) {
        } catch (InvalidDataException exception) {
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
        } finally {
            clients.TryRemove(client, out _);
            client.Dispose();
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
            var sessionId = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(token)))
                .ToLowerInvariant();
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
            participantInitializer?.InitializeParticipant(
                session.Id,
                new AuthoritativeParticipantSelection(requestedClassId) {
                    CharacterName = requestedCharacterName,
                });
            session.ParticipantInitialized = true;
        }
    }

    private bool RestoreSessionIfAvailable(SessionState session)
    {
        lock (session) {
            if (session.SaveLoaded || saveProvider is null)
                return false;
            var serializedSave = LoadSessionDocument(session)?.SerializedState
                ?? saveStore?.Load(session.Id);
            if (serializedSave is null) {
                session.SaveLoaded = true;
                return false;
            }
            saveProvider.ImportSessionState(session.Id, serializedSave);
            session.SaveLoaded = true;
            session.ParticipantInitialized = true;
            return true;
        }
    }

    private void SaveSession(
        SessionState session,
        IAuthoritativeSaveProvider? stateProvider = null,
        string? rulesetIdentityHash = null)
    {
        commandServer.WithSynchronizedSession(session.Id, () => {
            if (!session.ParticipantInitialized)
                return;
            var provider = stateProvider ?? saveProvider;
            var serializedState = provider is null
                ? null
                : provider.ExportSessionState(session.Id);
            var completedCommands = commandServer.ExportCompletedCommands(session.Id);
            sessionPersistence.Save(
                session.Id,
                serializedState,
                completedCommands,
                rulesetIdentityHash ?? handshake.Identity.RulesetIdentityHash);
        });
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
            var receipts = document is not null
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
        public string Token { get; } = token;
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

    private sealed record PreparedSessionSave(
        string SessionId,
        string SerializedState,
        IReadOnlyDictionary<ulong, CommandResult> CommandResults);

    private async ValueTask SendSnapshotIfAvailableAsync(
        Stream stream,
        string sessionId,
        uint entityId,
        ulong tick,
        CancellationToken cancellationToken)
    {
        if (snapshotProvider is null)
            return;

        var snapshot = snapshotProvider.CreateSnapshot(sessionId, entityId, tick);
        var currentIdentity = handshake.Identity;
        snapshot.ContentManifestHash = currentIdentity.ContentManifestHash;
        snapshot.RulesetIdentityHash = currentIdentity.RulesetIdentityHash;
        AuthoritativeSnapshotValidator.Validate(snapshot);
        await EnvelopeCodec.WriteAsync(stream, new Envelope { Snapshot = snapshot }, cancellationToken);
    }

    private ValueTask SendEventsIfAvailable(
        Stream stream,
        string sessionId,
        uint entityId,
        ulong tick,
        CancellationToken cancellationToken)
    {
        var events = eventProvider?.DrainEvents(sessionId, entityId, tick);
        return events is null || events.Events.Count == 0
            ? ValueTask.CompletedTask
            : WriteEventBatchAsync(stream, events, tick, cancellationToken);
    }

    private static ValueTask WriteEventBatchAsync(
        Stream stream,
        EventBatch events,
        ulong expectedTick,
        CancellationToken cancellationToken)
    {
        AuthoritativeEventBatchValidator.Validate(events, expectedTick);
        return EnvelopeCodec.WriteAsync(stream, new Envelope { EventBatch = events }, cancellationToken);
    }
}
