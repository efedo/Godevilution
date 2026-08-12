using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
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

        var operation = Task.Run(
            () => adapter.Execute("session", new Command(), 1),
            TestContext.Current.CancellationToken);
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
        Action? onExecute = null) : IAuthoritativeGameHostAdapter
    {
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
            return name;
        }

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
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
