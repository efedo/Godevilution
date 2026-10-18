using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;

namespace Devilution.Server.Host;

public sealed partial class AuthoritativeTcpServer
{
    // This roster is authoritative even while every participant is disconnected.
    // Token lookup remains ephemeral; raw bearer tokens are never persisted.
    private readonly Dictionary<string, SessionState> cohortSessionsById = new(StringComparer.Ordinal);
    private AuthoritativeCohortDocument? committedCohort;
    private Exception? cohortFailure;

    /** Refuses implicit migration before seed or legacy save-root writes. */
    public static void ValidateCohortRootForStartup(AuthoritativeCohortStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        store.PrepareHostPublicationLayout();
    }

    private IAuthoritativeGameCohortAdapter RequireCohortAdapter(
        IAuthoritativeGameHostAdapter? candidate = null)
    {
        var provider = (object?)candidate ?? gameAdapter ?? (object?)saveProvider;
        return provider is IAuthoritativeGameCohortAdapter cohort && cohort.SupportsCohortPersistence
            ? cohort
            : throw new NotSupportedException("Complete-cohort persistence requires a compatible game adapter.");
    }

    private AuthoritativeCohortIdentity CohortIdentity(
        IAuthoritativeGameCohortAdapter adapter,
        ProtocolServerIdentity? identity = null)
    {
        var active = identity ?? handshake.Identity;
        return new AuthoritativeCohortIdentity(
            adapter.CohortModuleId,
            adapter.CohortFormatVersion,
            active.ProtocolSchemaVersion,
            active.ContentManifestHash,
            active.RulesetIdentityHash,
            active.TickRateHz,
            active.GameMode);
    }

    /** Caller owns the command monitor; no listener or runtime advancement yet. */
    private void InitializeCohortHostCore()
    {
        if (CohortStore is null)
            return;
        try {
            RequireCohortHealthy();
            ValidateCohortRootForStartup(CohortStore);
            var adapter = RequireCohortAdapter();
            if (participantInitializer is null)
                throw new NotSupportedException("Complete-cohort admission requires explicit participant initialization.");
            var existingCommands = commandServer.CaptureCohortState();
            if (existingCommands.CurrentTick != 0 || existingCommands.NextServerReceiptSequence != 1
                || existingCommands.Sessions.Count != 0 || entityIds.NextId != 1
                || sessionsByToken.Count != 0 || cohortSessionsById.Count != 0)
                throw new InvalidOperationException("Cohort startup requires a fresh, unexposed host.");
            var activeState = adapter.CaptureCohortState();
            if (activeState.Tick != 0 || activeState.Participants.Count != 0)
                throw new InvalidOperationException("Cohort startup requires a fresh, unexposed module runtime.");
            var document = CohortStore.Load(CohortIdentity(adapter));
            if (document is not null) {
                var supplied = new AuthoritativeModuleCohortState(
                    document.Identity.ModuleId,
                    document.Identity.ModuleFormatVersion,
                    document.Tick,
                    Array.AsReadOnly(document.Participants.Select(participant =>
                        new AuthoritativeModuleCohortParticipant(participant.SessionId, participant.EntityId)).ToArray()),
                    document.SerializedModuleState);
                using var prepared = adapter.PrepareCohortRestore(supplied);
                ValidateCohortBindings(prepared.CohortState, document.Identity, document.Tick,
                    document.Participants.Select(participant =>
                        new AuthoritativeModuleCohortParticipant(participant.SessionId, participant.EntityId)).ToArray());
                var commands = new AuthoritativeCommandCohortState(
                    document.Tick,
                    document.NextServerReceiptSequence,
                    document.Participants.Select(participant =>
                        new AuthoritativeSessionCommandLedger(participant.SessionId, participant.CommandReceipts)).ToArray());
                commandServer.InitializeCohortState(commands);
                entityIds.RestoreNextId(document.NextEntityId);
                adapter.CommitCohortRestore(prepared);
                foreach (var participant in document.Participants) {
                    cohortSessionsById.Add(participant.SessionId,
                        new SessionState(participant.SessionId, string.Empty, participant.EntityId) {
                            ParticipantInitialized = true,
                            SaveLoaded = true,
                            CommandReceiptsLoaded = true,
                            SessionDocumentLoaded = true,
                        });
                }
                if (clock is IResumableAuthoritativeClock resumableClock)
                    resumableClock.RestoreAtTick(document.Tick);
                committedCohort = document;
            }
            // Settle only the current host clock, never elapsed offline time.
            AdvanceCurrentTickCore();
            lastSimulationTick = commandServer.CurrentTick;
            PublishCohortCore();
        } catch (Exception failure) {
            FenceCohort(failure);
            throw;
        }
    }

    private static void ValidateCohortBindings(
        AuthoritativeModuleCohortState state,
        AuthoritativeCohortIdentity identity,
        ulong tick,
        IReadOnlyList<AuthoritativeModuleCohortParticipant> expected)
    {
        if (state is null || state.ModuleId != identity.ModuleId
            || state.FormatVersion != identity.ModuleFormatVersion || state.Tick != tick
            || string.IsNullOrWhiteSpace(state.SerializedState) || state.Participants is null
            || state.Participants.Count != expected.Count)
            throw new InvalidDataException("The module cohort does not match the complete host identity, clock or roster.");
        var entityBindings = new HashSet<uint>();
        string? previous = null;
        for (var index = 0; index < expected.Count; index++) {
            var participant = state.Participants[index];
            if (participant is null || participant != expected[index]
                || participant.EntityId == 0 || !entityBindings.Add(participant.EntityId)
                || string.IsNullOrWhiteSpace(participant.SessionId)
                || participant.SessionId.Length != 64
                || participant.SessionId.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                || previous is not null && StringComparer.Ordinal.Compare(previous, participant.SessionId) >= 0)
                throw new InvalidDataException("The module cohort contains an invalid stable participant binding.");
            previous = participant.SessionId;
        }
    }

    private AuthoritativeCohortDocument CaptureCohortDocumentCore(
        IAuthoritativeGameCohortAdapter adapter,
        AuthoritativeCohortIdentity identity)
    {
        var module = adapter.CaptureCohortState();
        var commands = commandServer.CaptureCohortState();
        var roster = cohortSessionsById.Values.Where(session => session.ParticipantInitialized)
            .OrderBy(session => session.Id, StringComparer.Ordinal).ToArray();
        var bindings = roster.Select(session =>
            new AuthoritativeModuleCohortParticipant(session.Id, session.EntityId)).ToArray();
        ValidateCohortBindings(module, identity, commands.CurrentTick, bindings);
        var ledgers = commands.Sessions.ToDictionary(session => session.SessionId, StringComparer.Ordinal);
        if (ledgers.Keys.Any(sessionId => !cohortSessionsById.TryGetValue(sessionId, out var owner)
                || !owner.ParticipantInitialized))
            throw new InvalidDataException("The command cohort contains a ledger without an initialized owner.");
        var generation = committedCohort is null ? 1UL : checked(committedCohort.Generation + 1);
        return new AuthoritativeCohortDocument(
            AuthoritativeCohortStore.CurrentFormatVersion,
            generation,
            identity,
            commands.CurrentTick,
            entityIds.NextId,
            commands.NextServerReceiptSequence,
            roster.Select(session => new AuthoritativeCohortParticipantCheckpoint(
                session.Id,
                session.EntityId,
                ledgers.TryGetValue(session.Id, out var ledger) ? ledger.CommandResults : [])).ToArray(),
            module.SerializedState);
    }

    private void PublishCohortCore()
    {
        if (CohortStore is null)
            return;
        RequireCohortHealthy();
        try {
            var adapter = RequireCohortAdapter();
            var document = CaptureCohortDocumentCore(adapter, CohortIdentity(adapter));
            CohortStore.Save(document);
            committedCohort = document;
        } catch (Exception failure) {
            // Mutation has already happened. Never continue or publish this
            // state later as if the failed command had been acknowledged.
            FenceCohort(failure);
            throw;
        }
    }

    private void RequireCohortHealthy()
    {
        if (CohortStore is not null && Volatile.Read(ref cohortFailure) is Exception failure)
            throw new InvalidOperationException("The complete-cohort host is fenced after publication failure.", failure);
    }

    private void FenceCohort(Exception failure)
    {
        if (CohortStore is null || Interlocked.CompareExchange(ref cohortFailure, failure, null) is not null)
            return;
        Volatile.Write(ref lastClientFailure, failure);
        completion.TrySetException(failure);
        // Cancel is explicitly legal inside a durable scheduler callback.
        // Do it synchronously so no queued peer or final AdvanceTo can run.
        try { commandScheduler?.Cancel(); } catch { }
        try { cancellation?.Cancel(); } catch { }
        try { listener.Stop(); } catch { }
        foreach (var client in clients.Keys) {
            try { client.Dispose(); } catch { }
        }
    }

    private SessionState? FindCohortSessionCore(string resumeToken)
    {
        RequireCohortHealthy();
        if (string.IsNullOrWhiteSpace(resumeToken)
            || !cohortSessionsById.TryGetValue(SessionIdForToken(resumeToken), out var session)
            || !session.ParticipantInitialized)
            return null;
        BindCohortToken(session, resumeToken);
        return session;
    }

    private void BindCohortToken(SessionState session, string token)
    {
        if (session.Token.Length != 0 && session.Token != token)
            throw new InvalidDataException("The resume token does not match the existing owner binding.");
        session.Token = token;
        sessionsByToken.TryAdd(token, session);
    }

    private SessionState GetOrCreateCohortSessionCore(string resumeToken)
    {
        RequireCohortHealthy();
        var token = string.IsNullOrWhiteSpace(resumeToken)
            ? Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant() : resumeToken;
        var sessionId = SessionIdForToken(token);
        if (cohortSessionsById.TryGetValue(sessionId, out var prior)) {
            BindCohortToken(prior, token);
            return prior;
        }
        var adapter = RequireCohortAdapter();
        uint entityId;
        do { entityId = entityIds.Allocate(); }
        while (adapter.IsEntityIdOccupied(entityId));
        var session = new SessionState(sessionId, token, entityId) {
            SaveLoaded = true,
            CommandReceiptsLoaded = true,
            SessionDocumentLoaded = true,
        };
        cohortSessionsById.Add(sessionId, session);
        sessionsByToken.TryAdd(token, session);
        return session;
    }

    private ProtocolError? PrepareCohortSessionCore(SessionState session, ClientHello hello)
    {
        RequireCohortHealthy();
        if (!session.ParticipantInitialized) {
            var initializer = participantInitializer
                ?? throw new NotSupportedException("Complete-cohort admission requires explicit participant initialization.");
            var adapter = RequireCohortAdapter();
            var before = adapter.CaptureCohortState();
            try {
                initializer.InitializeParticipant(session.Id,
                    new AuthoritativeParticipantSelection(hello.HasRequestedCharacterClassId
                        ? hello.RequestedCharacterClassId : null) {
                        CharacterName = hello.HasRequestedCharacterName ? hello.RequestedCharacterName : null,
                        EntityId = session.EntityId,
                    });
                session.ParticipantInitialized = true;
            } catch (Exception failure) {
                // Only a proven non-mutating rejection is recoverable. Never
                // roll the allocator backwards or retain a stale reservation.
                try {
                    var after = adapter.CaptureCohortState();
                    if (before.ModuleId != after.ModuleId || before.FormatVersion != after.FormatVersion
                        || before.Tick != after.Tick || before.SerializedState != after.SerializedState
                        || !before.Participants.SequenceEqual(after.Participants))
                        throw new InvalidDataException("Failed participant admission changed the authoritative cohort.");
                    cohortSessionsById.Remove(session.Id);
                    sessionsByToken.TryRemove(session.Token, out _);
                    PublishCohortCore();
                } catch (Exception cleanupFailure) {
                    FenceCohort(failure);
                    try { failure.Data["CohortAdmissionCleanupFailure"] = cleanupFailure; } catch { }
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                }
                return new ProtocolError { Code = ProtocolErrorCode.InvalidMessage, Detail = failure.Message };
            }
        }
        AdvanceCurrentTickCore();
        return null;
    }

    private sealed record CohortHelloPublication(
        SessionState? Session, ServerHello? Hello, ProtocolError? Error, Snapshot? Snapshot, ulong Tick);

    /** Identity, admission, projection and durability share one authority barrier. */
    private CohortHelloPublication PrepareCohortHelloCore(ClientHello hello)
    {
        RequireCohortHealthy();
        var existing = FindCohortSessionCore(hello.ResumeToken);
        var result = handshake.Validate(hello, allowResumedIdentityRefresh: existing is not null);
        if (!result.Accepted)
            return new(null, null, result.Error, null, commandServer.CurrentTick);
        try {
            var session = existing ?? GetOrCreateCohortSessionCore(hello.ResumeToken);
            var rejection = PrepareCohortSessionCore(session, hello);
            if (rejection is not null)
                return new(null, null, rejection, null, commandServer.CurrentTick);
            var serverHello = result.ServerHello!.Clone();
            serverHello.SessionToken = session.Token;
            serverHello.LastClientSequence = commandScheduler?.LastAdmittedClientSequence(session.Id)
                ?? commandServer.LastClientSequence(session.Id);
            serverHello.CommandSchedulingMode = UseClockGatedCommandScheduling
                ? CommandSchedulingMode.ClockGated : CommandSchedulingMode.Immediate;
            serverHello.SupportsClockOnlyProbes = UseClockGatedCommandScheduling;
            serverHello.SupportsTargetCellPathing = SupportsTargetCellPathing;

            var tick = commandServer.CurrentTick;
            var snapshot = snapshotProvider?.CreateSnapshot(session.Id, session.EntityId, tick);
            if (snapshot is not null) {
                snapshot.ContentManifestHash = handshake.Identity.ContentManifestHash;
                snapshot.RulesetIdentityHash = handshake.Identity.RulesetIdentityHash;
                AuthoritativeSnapshotValidator.Validate(snapshot);
            }
            PublishCohortCore();
            return new(session, serverHello, null, snapshot, tick);
        } catch (Exception failure) {
            FenceCohort(failure);
            throw;
        }
    }

    private ulong AdvanceAndPublishCohortCore(ulong legacyMinimumTick = 0)
    {
        var tick = AdvanceCurrentTickCore(legacyMinimumTick);
        PublishCohortCore();
        return tick;
    }

    private GameplayRulesetReloadResult<IAuthoritativeGameHostAdapter> TryReloadCohortCore(
        ContentManifest manifest,
        IEnumerable<IGameplayModule> modules)
    {
        RequireCohortHealthy();
        AdvanceCurrentTickCore();
        PublishCohortCore();
        if (commandScheduler is { PendingCommandCount: not 0 })
            throw new InvalidOperationException("Ruleset reload is unavailable while commands are queued.");
        if (gameAdapter is not LeasedAuthoritativeGameHostAdapter leased)
            throw new InvalidOperationException("Live cohort reload requires the leased host adapter.");
        var previous = committedCohort!;
        var attemptedPublication = false;
        var activated = false;
        try {
            var result = leased.TryReloadCohort(manifest, modules, candidate => {
                var identity = ProtocolServerIdentity.FromRuleset(
                    handshake.Identity.BuildId,
                    handshake.Identity.ProtocolSchemaVersion,
                    handshake.Identity.TickRateHz,
                    candidate.Identity,
                    handshake.Identity.GameMode);
                var adapter = RequireCohortAdapter(candidate.Target);
                var document = CaptureCohortDocumentCore(adapter, CohortIdentity(adapter, identity));
                attemptedPublication = true;
                CohortStore!.Save(document, previous.Identity);
                committedCohort = document;
            });
            if (!result.Succeeded) {
                if (attemptedPublication && !DurableCohortMatches(previous)) {
                    var failure = new IOException("Ruleset activation failed after a changed or uncertain durable cohort publication: "
                        + string.Join("; ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
                    FenceCohort(failure);
                    throw failure;
                }
                return result;
            }
            activated = true;
            if (!attemptedPublication || committedCohort == previous)
                throw new InvalidOperationException("Ruleset activation did not publish its complete candidate cohort.");
            handshake.UpdateIdentity(ProtocolServerIdentity.FromRuleset(
                handshake.Identity.BuildId,
                handshake.Identity.ProtocolSchemaVersion,
                handshake.Identity.TickRateHz,
                result.Active.Identity,
                handshake.Identity.GameMode));
            leased.DisposeRetiredAdapters();
            return result;
        } catch (Exception failure) {
            if (activated || attemptedPublication && !DurableCohortMatches(previous))
                FenceCohort(failure);
            throw;
        }
    }

    private bool DurableCohortMatches(AuthoritativeCohortDocument expected)
    {
        try {
            var actual = CohortStore!.Load();
            return actual is not null && actual.FormatVersion == expected.FormatVersion
                && actual.Generation == expected.Generation && actual.Identity == expected.Identity
                && actual.Tick == expected.Tick && actual.NextEntityId == expected.NextEntityId
                && actual.NextServerReceiptSequence == expected.NextServerReceiptSequence
                && actual.SerializedModuleState == expected.SerializedModuleState
                && actual.Participants.Count == expected.Participants.Count
                && actual.Participants.Zip(expected.Participants).All(pair =>
                    pair.First.SessionId == pair.Second.SessionId
                    && pair.First.EntityId == pair.Second.EntityId
                    && pair.First.CommandReceipts.SequenceEqual(pair.Second.CommandReceipts));
        } catch {
            return false;
        }
    }
}
