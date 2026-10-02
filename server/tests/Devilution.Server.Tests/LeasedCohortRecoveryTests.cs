using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Leased complete-cohort publication coverage over real Diablo adapter graphs. */
public sealed class LeasedCohortRecoveryTests
{
    [Fact]
    public void CohortReloadForwardsOpaqueGraphWithoutPerSessionReplay()
    {
        var initialExecutor = CreateExecutor();
        initialExecutor.InitializeParticipant(
            "connected",
            new AuthoritativeParticipantSelection(null) { EntityId = 7 });
        initialExecutor.InitializeParticipant(
            "disconnected",
            new AuthoritativeParticipantSelection(null) { EntityId = 8 });
        var initial = new ForwardingAdapter(initialExecutor);
        var replacementExecutor = CreateExecutor();
        var replacement = new ForwardingAdapter(replacementExecutor);
        var runtime = CreateRuntime(new Queue<ForwardingAdapter>([initial, replacement]));
        using var leased = new LeasedAuthoritativeGameHostAdapter(runtime);

        var source = leased.CaptureCohortState();
        Assert.NotEmpty(source.SerializedState);

        var result = leased.TryReloadCohort(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0")],
            _ => { });

        Assert.True(result.Succeeded);
        Assert.Same(replacement, runtime.Active.Target);
        Assert.Equal(1, replacement.PrepareCalls);
        Assert.Equal(1, replacement.CommitCalls);
        Assert.Equal(0, replacement.ImportSessionStateCalls);
        Assert.Equal(0, replacement.ImportPlayerSaveCalls);
        Assert.Equal(7U, replacementExecutor.GetPlayerState("connected").EntityId);
        Assert.Equal(8U, replacementExecutor.GetPlayerState("disconnected").EntityId);
        Assert.Equal(source.SerializedState, replacementExecutor.CaptureCohortState().SerializedState);
        Assert.Equal(result.Active.Identity.CombinedSha256, leased.ActiveRulesetIdentityHash);
        Assert.Equal(1, leased.RetiredRulesetCount);
    }

    [Fact]
    public void PreparedWrapperPinsOwnerGenerationAndDetachesBindingMetadata()
    {
        var initial = new ForwardingAdapter(CreateExecutor());
        initial.Executor.InitializeParticipant(
            "hero",
            new AuthoritativeParticipantSelection(null) { EntityId = 11 });
        var replacement = new ForwardingAdapter(CreateExecutor());
        var runtime = CreateRuntime(new Queue<ForwardingAdapter>([initial, replacement]));
        using var leased = new LeasedAuthoritativeGameHostAdapter(runtime);
        var foreignInitial = new ForwardingAdapter(CreateExecutor());
        var foreignReplacement = new ForwardingAdapter(CreateExecutor());
        var foreignRuntime = CreateRuntime(
            new Queue<ForwardingAdapter>([foreignInitial, foreignReplacement]));
        using var foreign = new LeasedAuthoritativeGameHostAdapter(foreignRuntime);

        var source = leased.CaptureCohortState();
        var mutableBindings = source.Participants.ToList();
        var supplied = source with { Participants = mutableBindings };
        using var prepared = leased.PrepareCohortRestore(supplied);
        var preparedBinding = Assert.Single(prepared.CohortState.Participants);
        mutableBindings[0] = new AuthoritativeModuleCohortParticipant("spoof", 999);

        Assert.Equal(preparedBinding, Assert.Single(prepared.CohortState.Participants));
        Assert.Equal("hero", preparedBinding.SessionId);
        Assert.Equal(11U, preparedBinding.EntityId);
        Assert.Throws<InvalidOperationException>(
            () => foreign.CommitCohortRestore(prepared));

        Assert.True(runtime.TryReload(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0")]).Succeeded);
        Assert.Throws<InvalidOperationException>(
            () => leased.CommitCohortRestore(prepared));
        Assert.Same(replacement, runtime.Active.Target);
    }

    [Fact]
    public void SuccessfulCandidateCallbackWithPreparedCleanupFailureFencesCandidateAndKeepsPrimaryDiagnostic()
    {
        var initial = new ForwardingAdapter(CreateExecutor());
        initial.Executor.InitializeParticipant(
            "hero",
            new AuthoritativeParticipantSelection(null) { EntityId = 21 });
        var replacement = new ForwardingAdapter(
            CreateExecutor(),
            preparedDisposeFailure: new InvalidOperationException("prepared-cleanup-primary"));
        var runtime = CreateRuntime(new Queue<ForwardingAdapter>([initial, replacement]));
        var secondaryCleanup = new InvalidOperationException("candidate-cleanup-secondary");
        var callbackRan = false;
        using var leased = new LeasedAuthoritativeGameHostAdapter(
            runtime,
            adapter => {
                if (ReferenceEquals(adapter, replacement)) {
                    replacement.Dispose();
                    throw secondaryCleanup;
                }
                if (adapter is IDisposable disposable)
                    disposable.Dispose();
            });

        var result = leased.TryReloadCohort(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0")],
            _ => callbackRan = true);

        Assert.True(callbackRan);
        Assert.False(result.Succeeded);
        Assert.Equal(
            "ruleset-target-preparation-failed",
            Assert.Single(result.Diagnostics).Code);
        Assert.Equal(
            "prepared-cleanup-primary",
            Assert.Single(result.Diagnostics).Message);
        Assert.Same(initial, runtime.Active.Target);
        Assert.Equal(0, runtime.RetiredActivationCount);
        Assert.True(replacement.Disposed);
        Assert.Same(
            secondaryCleanup,
            replacement.PreparedDisposeFailure!.Data["cohort-cleanup-candidate-adapter"]);
        Assert.Equal(21U, initial.Executor.GetPlayerState("hero").EntityId);
    }

    private static GameplayRulesetRuntime<IAuthoritativeGameHostAdapter> CreateRuntime(
        Queue<ForwardingAdapter> candidates)
    {
        return new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(
            CreateManifest("1", "initial"),
            [new TestModule("rules", "1.0.0")],
            new DelegateAuthoritativeClock(() => 0),
            (_, _) => candidates.Dequeue());
    }

    private static ContentManifest CreateManifest(string version, string value)
    {
        return new ContentManifest(
            "rules",
            version,
            [new ContentPack(
                "base",
                version,
                [TsvTable.Parse("rules.tsv", $"id\tvalue\na\t{value}\n")])]);
    }

    private static StoreSimulationExecutor CreateExecutor()
    {
        return new StoreSimulationExecutor(
            new StoreCatalog(),
            startingGold: 100,
            worldWidth: 8,
            worldHeight: 8,
            startingPositionX: 0,
            startingPositionY: 0);
    }

    private sealed class ForwardingAdapter(
        StoreSimulationExecutor executor,
        InvalidOperationException? preparedDisposeFailure = null)
        : IAuthoritativeGameHostAdapter, IAuthoritativeGameCohortAdapter, IDisposable
    {
        public StoreSimulationExecutor Executor { get; } = executor;

        public InvalidOperationException? PreparedDisposeFailure { get; } = preparedDisposeFailure;

        public int PrepareCalls { get; private set; }

        public int CommitCalls { get; private set; }

        public int ImportSessionStateCalls { get; private set; }

        public int ImportPlayerSaveCalls { get; private set; }

        public bool Disposed { get; private set; }

        private IAuthoritativeGameCohortAdapter Cohort
            => (IAuthoritativeGameCohortAdapter)Executor;

        public bool SupportsCohortPersistence => Cohort.SupportsCohortPersistence;

        public string CohortModuleId => Cohort.CohortModuleId;

        public int CohortFormatVersion => Cohort.CohortFormatVersion;

        public bool IsEntityIdOccupied(uint entityId) => Cohort.IsEntityIdOccupied(entityId);

        public AuthoritativeModuleCohortState CaptureCohortState()
            => Cohort.CaptureCohortState();

        public IAuthoritativePreparedCohortRestore PrepareCohortRestore(
            AuthoritativeModuleCohortState state)
        {
            PrepareCalls++;
            return new ForwardedPrepared(
                Cohort.PrepareCohortRestore(state),
                PreparedDisposeFailure);
        }

        public void CommitCohortRestore(IAuthoritativePreparedCohortRestore prepared)
        {
            CommitCalls++;
            if (prepared is not ForwardedPrepared forwarded)
                throw new InvalidOperationException(
                    "The forwarding decorator received an unknown prepared token.");
            Cohort.CommitCohortRestore(forwarded.Inner);
        }

        public CommandExecutionResult Execute(
            string sessionId,
            Command command,
            ulong appliedTick)
            => Executor.Execute(sessionId, command, appliedTick);

        public void AdvanceTo(ulong tick) => Executor.AdvanceTo(tick);

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
            => Executor.CreateSnapshot(sessionId, entityId, tick);

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
            => Executor.DrainEvents(sessionId, entityId, tick);

        public string ExportPlayerSave(string sessionId)
            => Executor.ExportPlayerSave(sessionId);

        public string ExportSessionState(string sessionId)
            => Executor.ExportSessionState(sessionId);

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
            ImportPlayerSaveCalls++;
            Executor.ImportPlayerSave(sessionId, serializedSave);
        }

        public void ImportSessionState(string sessionId, string serializedState)
        {
            ImportSessionStateCalls++;
            Executor.ImportSessionState(sessionId, serializedState);
        }

        public ulong GetSessionStateTick(string sessionId)
            => Executor.GetSessionStateTick(sessionId);

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
            => Executor.InitializeParticipant(sessionId, selection);

        public void Dispose() => Disposed = true;

        private sealed class ForwardedPrepared(
            IAuthoritativePreparedCohortRestore inner,
            InvalidOperationException? disposeFailure)
            : IAuthoritativePreparedCohortRestore
        {
            public IAuthoritativePreparedCohortRestore Inner { get; } = inner;

            public AuthoritativeModuleCohortState CohortState => Inner.CohortState;

            public void Dispose()
            {
                Inner.Dispose();
                if (disposeFailure is not null)
                    throw disposeFailure;
            }
        }
    }

    private sealed class TestModule(string id, string version) : IGameplayModule
    {
        public GameplayModuleIdentity Identity { get; } =
            new(id, version, $"hash-{version}");

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
        }
    }
}
