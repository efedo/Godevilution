using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class RulesetLeaseHostAdapterTests
{
    [Fact]
    public async Task HostOperationPinsTheAdapterUntilTheOperationCompletes()
    {
        using var operationStarted = new ManualResetEventSlim();
        using var releaseOperation = new ManualResetEventSlim();
        var initial = new FakeAdapter(
            "initial",
            () => {
                operationStarted.Set();
                releaseOperation.Wait(TestContext.Current.CancellationToken);
            });
        var replacement = new FakeAdapter("replacement");
        var candidates = new Queue<FakeAdapter>([initial, replacement]);
        var runtime = CreateRuntime(candidates);
        var adapter = new LeasedAuthoritativeGameHostAdapter(runtime);

        // The operation deliberately blocks; do not depend on a test-runner
        // pool slot becoming available while this test waits for its signal.
        var operation = Task.Factory.StartNew(
            () => adapter.Execute("session", new Command(), 1),
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        try {
            Assert.True(operationStarted.Wait(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken));

            var reload = runtime.TryReload(
                CreateManifest("2", "replacement"),
                [new TestModule("rules", "2.0.0")]);

            Assert.True(reload.Succeeded);
            Assert.Equal(1, runtime.RetiredActivationCount);
            Assert.Empty(runtime.DrainRetired());
        } finally {
            releaseOperation.Set();
        }

        var operationResult = await operation;
        Assert.True(operationResult.Succeeded);
        var retired = runtime.DrainRetired();
        Assert.Single(retired);
        Assert.Same(initial, retired[0].Target);
        Assert.Equal("replacement", adapter.ExportPlayerSave("session"));
    }

    [Fact]
    public void ReloadMigratesSessionsBeforePublishingAndDisposesRetiredAdapters()
    {
        var initial = new FakeAdapter("initial");
        var replacement = new FakeAdapter("replacement");
        var candidates = new Queue<FakeAdapter>([initial, replacement]);
        var runtime = CreateRuntime(candidates);
        var adapter = new LeasedAuthoritativeGameHostAdapter(runtime);

        var result = adapter.TryReload(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0")],
            ["session"]);

        Assert.True(result.Succeeded);
        Assert.Equal("initial", replacement.ExportPlayerSave("session"));
        Assert.True(replacement.SessionStateImported);
        Assert.Equal(1, adapter.DisposeRetiredAdapters());
        Assert.True(initial.Disposed);
        Assert.False(replacement.Disposed);
        Assert.Empty(runtime.DrainRetired());
    }

    [Fact]
    public void RepeatedReloadsPreserveSessionStateAndRetireEachPreviousGenerationAcross64Generations()
    {
        const int replacementGenerations = 64;
        var names = Enumerable.Range(0, replacementGenerations + 1)
            .Select(index => index == 0 ? "initial" : $"replacement-{index}")
            .ToArray();
        var adapters = Enumerable.Range(0, replacementGenerations + 1)
            .Select(index => new FakeAdapter(names[index]))
            .ToArray();
        var runtime = CreateRuntime(new Queue<FakeAdapter>(adapters));
        var adapter = new LeasedAuthoritativeGameHostAdapter(runtime);

        for (var generation = 1; generation <= replacementGenerations; generation++) {
            var result = adapter.TryReload(
                CreateManifest(generation.ToString(), names[generation]),
                [new TestModule("rules", $"{generation + 1}.0.0")],
                ["session-a", "session-b"]);

            Assert.True(result.Succeeded);
            Assert.Same(adapters[generation], runtime.Active.Target);
            Assert.Equal("initial", adapter.ExportSessionState("session-a"));
            Assert.Equal("initial", adapter.ExportSessionState("session-b"));
            Assert.True(adapters[generation].SessionStateImported);
            Assert.Equal(1, runtime.RetiredActivationCount);

            Assert.Equal(1, adapter.DisposeRetiredAdapters());
            Assert.True(adapters[generation - 1].Disposed);
            Assert.False(adapters[generation].Disposed);
            Assert.Empty(runtime.DrainRetired());
        }
    }

    [Fact]
    public void FailedSessionMigrationLeavesThePreviousGenerationActive()
    {
        var initial = new FakeAdapter("initial");
        var replacement = new FakeAdapter("replacement") { ThrowOnImport = true };
        var candidates = new Queue<FakeAdapter>([initial, replacement]);
        var runtime = CreateRuntime(candidates);
        var adapter = new LeasedAuthoritativeGameHostAdapter(runtime);

        var result = adapter.TryReload(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0")],
            ["session"]);

        Assert.False(result.Succeeded);
        Assert.Equal(
            "ruleset-target-preparation-failed",
            Assert.Single(result.Diagnostics).Code);
        Assert.Same(initial, runtime.Active.Target);
        Assert.Equal(0, runtime.RetiredActivationCount);
        Assert.True(replacement.Disposed);
        Assert.Equal("initial", adapter.ExportPlayerSave("session"));
    }

    [Fact]
    public async Task ReloadWaitsForInFlightHostOperations()
    {
        using var operationStarted = new ManualResetEventSlim();
        using var releaseOperation = new ManualResetEventSlim();
        var initial = new FakeAdapter(
            "initial",
            () => {
                operationStarted.Set();
                releaseOperation.Wait(TestContext.Current.CancellationToken);
            });
        var replacement = new FakeAdapter("replacement");
        var candidates = new Queue<FakeAdapter>([initial, replacement]);
        var runtime = CreateRuntime(candidates);
        var adapter = new LeasedAuthoritativeGameHostAdapter(runtime);

        var operation = Task.Run(
            () => adapter.Execute("session", new Command(), 1),
            TestContext.Current.CancellationToken);
        Assert.True(operationStarted.Wait(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken));

        var reload = Task.Run(
            () => adapter.TryReload(
                CreateManifest("2", "replacement"),
                [new TestModule("rules", "2.0.0")],
                ["session"]),
            TestContext.Current.CancellationToken);
        try {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(reload.IsCompleted);
        } finally {
            releaseOperation.Set();
        }

        Assert.True((await operation).Succeeded);
        Assert.True((await reload).Succeeded);
        Assert.Equal("initial", adapter.ExportPlayerSave("session"));
        Assert.True(replacement.SessionStateImported);
    }

    [Fact]
    public async Task TcpHostPublishesTheReplacementHandshakeIdentity()
    {
        var initial = new FakeAdapter("initial");
        var replacement = new FakeAdapter("replacement");
        var runtime = CreateRuntime(new Queue<FakeAdapter>([initial, replacement]));
        var adapter = new LeasedAuthoritativeGameHostAdapter(runtime);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            "initial-content",
            20) {
            RulesetIdentityHash = "initial-ruleset",
        });
        await using var server = new AuthoritativeTcpServer(
            new AuthoritativeCommandServer(adapter),
            handshake,
            new DelegateAuthoritativeClock(() => 0),
            adapter);

        var result = server.TryReloadRuleset(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0")]);

        Assert.True(result.Succeeded);
        Assert.True(initial.Disposed);
        Assert.Equal(result.Active.Identity.CombinedSha256, adapter.ActiveRulesetIdentityHash);
        Assert.Equal(0, adapter.RetiredRulesetCount);
        Assert.Equal(result.Active.Identity.CombinedSha256, handshake.Identity.ContentManifestHash);
        Assert.Equal(result.Active.Identity.CombinedSha256, handshake.Identity.RulesetIdentityHash);
    }

    [Fact]
    public async Task TcpHostDisposesAnUnstartedLeasedAdapter()
    {
        var initial = new FakeAdapter("initial");
        var runtime = CreateRuntime(new Queue<FakeAdapter>([initial]));
        var adapter = new LeasedAuthoritativeGameHostAdapter(runtime);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity(
            "server-build",
            "0.1.0",
            "initial-content",
            20));
        await using (var server = new AuthoritativeTcpServer(
                         new AuthoritativeCommandServer(adapter),
                         handshake,
                         new DelegateAuthoritativeClock(() => 0),
                         adapter)) {
        }

        Assert.True(initial.Disposed);
    }

    [Fact]
    public void DisposingTheLeasedAdapterDisposesRetiredTargetsAndRejectsNewOperations()
    {
        var initial = new FakeAdapter("initial");
        var replacement = new FakeAdapter("replacement");
        var runtime = CreateRuntime(new Queue<FakeAdapter>([initial, replacement]));
        var adapter = new LeasedAuthoritativeGameHostAdapter(runtime);
        Assert.True(adapter.TryReload(
            CreateManifest("2", "replacement"),
            [new TestModule("rules", "2.0.0")],
            []).Succeeded);

        adapter.Dispose();
        adapter.Dispose();

        Assert.True(initial.Disposed);
        Assert.True(replacement.Disposed);
        Assert.Throws<ObjectDisposedException>(
            () => adapter.ExportSessionState("session"));
    }

    private static GameplayRulesetRuntime<IAuthoritativeGameHostAdapter> CreateRuntime(
        Queue<FakeAdapter> candidates)
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

    private sealed class FakeAdapter(
        string name,
        Action? onExecute = null) : IAuthoritativeGameHostAdapter, IDisposable
    {
        private readonly Dictionary<string, string> saves = new(StringComparer.Ordinal);

        public bool ThrowOnImport { get; init; }

        public bool Disposed { get; private set; }

        public bool SessionStateImported { get; private set; }

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            onExecute?.Invoke();
            return CommandExecutionResult.Accepted;
        }

        public void AdvanceTo(ulong tick)
        {
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            return new Snapshot();
        }

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
        {
            return null;
        }

        public string ExportPlayerSave(string sessionId)
        {
            return saves.TryGetValue(sessionId, out var save)
                ? save
                : name;
        }

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
            if (ThrowOnImport)
                throw new InvalidDataException("session migration rejected");
            saves[sessionId] = serializedSave;
        }

        public string ExportSessionState(string sessionId)
        {
            return ExportPlayerSave(sessionId);
        }

        public void ImportSessionState(string sessionId, string serializedState)
        {
            SessionStateImported = true;
            ImportPlayerSave(sessionId, serializedState);
        }

        public void Dispose()
        {
            Disposed = true;
        }

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
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
