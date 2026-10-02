using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class LeasedAdapterCleanupTests
{
    [Fact]
    public void DisposeRetiredAdaptersAttemptsEveryTargetAndLeavesActiveUsable()
    {
        var initial = new DisposableAdapter("initial");
        var replacement = new DisposableAdapter("replacement");
        var active = new DisposableAdapter("active");
        var candidates = new Queue<DisposableAdapter>([initial, replacement, active]);
        var runtime = CreateRuntime(candidates);
        var cleanupFailures = new Dictionary<string, Exception>(StringComparer.Ordinal) {
            [initial.Name] = new InvalidOperationException("initial retired cleanup failed"),
            [replacement.Name] = new InvalidOperationException("replacement retired cleanup failed"),
        };
        var cleanupOrder = new List<string>();
        var leased = new LeasedAuthoritativeGameHostAdapter(
            runtime,
            adapter => DisposeAdapter(adapter, cleanupFailures, cleanupOrder));
        try {
            Assert.True(Reload(leased, "2", replacement.Name));
            Assert.True(Reload(leased, "3", active.Name));
            Assert.Equal(2, leased.RetiredRulesetCount);

            var failure = Assert.Throws<InvalidOperationException>(
                () => leased.DisposeRetiredAdapters());
            Assert.Same(cleanupFailures[initial.Name], failure);
            Assert.Equal(
                [initial.Name, replacement.Name],
                cleanupOrder);
            Assert.True(initial.DisposeAttempted);
            Assert.True(replacement.DisposeAttempted);
            Assert.False(active.DisposeAttempted);
            Assert.Equal(0, leased.RetiredRulesetCount);
            Assert.Same(
                cleanupFailures[replacement.Name],
                failure.Data["cohort-cleanup-retired-adapter-1"]);
            Assert.False(failure.Data.Contains("cohort-cleanup-retired-adapter-0"));
            Assert.Equal(active.Name, leased.ExportSessionState("session"));
        } finally {
            leased.Dispose();
        }

        Assert.True(active.DisposeAttempted);
    }

    [Fact]
    public void DisposeAttemptsAllRetiredAndActiveTargetsAndIsIdempotent()
    {
        var initial = new DisposableAdapter("initial");
        var replacement = new DisposableAdapter("replacement");
        var active = new DisposableAdapter("active");
        var candidates = new Queue<DisposableAdapter>([initial, replacement, active]);
        var runtime = CreateRuntime(candidates);
        var cleanupFailures = new Dictionary<string, Exception>(StringComparer.Ordinal) {
            [initial.Name] = new InvalidOperationException("initial retired cleanup failed"),
            [replacement.Name] = new InvalidOperationException("replacement retired cleanup failed"),
            [active.Name] = new InvalidOperationException("active cleanup failed"),
        };
        var cleanupOrder = new List<string>();
        var leased = new LeasedAuthoritativeGameHostAdapter(
            runtime,
            adapter => DisposeAdapter(adapter, cleanupFailures, cleanupOrder));

        Assert.True(Reload(leased, "2", replacement.Name));
        Assert.True(Reload(leased, "3", active.Name));

        var failure = Assert.Throws<InvalidOperationException>(() => leased.Dispose());
        Assert.Equal(
            [initial.Name, replacement.Name, active.Name],
            cleanupOrder);
        Assert.Same(cleanupFailures[initial.Name], failure);
        Assert.Same(
            cleanupFailures[replacement.Name],
            failure.Data["cohort-cleanup-retired-adapter-1"]);
        Assert.Same(
            cleanupFailures[active.Name],
            failure.Data["cohort-cleanup-active-adapter"]);
        Assert.True(initial.DisposeAttempted);
        Assert.True(replacement.DisposeAttempted);
        Assert.True(active.DisposeAttempted);

        leased.Dispose();
        Assert.Equal(
            [initial.Name, replacement.Name, active.Name],
            cleanupOrder);
        Assert.Throws<ObjectDisposedException>(() => leased.ExportSessionState("session"));
    }

    private static bool Reload(
        LeasedAuthoritativeGameHostAdapter leased,
        string version,
        string expectedName)
    {
        var result = leased.TryReload(
            CreateManifest(version, expectedName),
            [new TestModule("rules", $"{version}.0.0")],
            ["session"]);
        Assert.True(result.Succeeded);
        Assert.Equal(expectedName, leased.ExportSessionState("session"));
        return result.Succeeded;
    }

    private static GameplayRulesetRuntime<IAuthoritativeGameHostAdapter> CreateRuntime(
        Queue<DisposableAdapter> candidates)
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

    private static void DisposeAdapter(
        IAuthoritativeGameHostAdapter adapter,
        IReadOnlyDictionary<string, Exception> failures,
        ICollection<string> cleanupOrder)
    {
        var disposable = Assert.IsType<DisposableAdapter>(adapter);
        cleanupOrder.Add(disposable.Name);
        disposable.Dispose();
        if (failures.TryGetValue(disposable.Name, out var failure))
            throw failure;
    }

    private sealed class DisposableAdapter(string name) : IAuthoritativeGameHostAdapter, IDisposable
    {
        public string Name { get; } = name;

        public bool DisposeAttempted { get; private set; }

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
            => CommandExecutionResult.Accepted;

        public void AdvanceTo(ulong tick)
        {
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
            => new();

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
            => null;

        public string ExportPlayerSave(string sessionId)
            => Name;

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
        }

        public string ExportSessionState(string sessionId)
            => Name;

        public void ImportSessionState(string sessionId, string serializedState)
        {
        }

        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
        }

        public void Dispose()
        {
            DisposeAttempted = true;
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
