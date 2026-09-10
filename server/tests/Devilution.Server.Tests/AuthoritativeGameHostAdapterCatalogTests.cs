using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeGameHostAdapterCatalogTests
{
    [Fact]
    public void SelectsExactlyOneCompatibleFactoryAndForwardsStartConfiguration()
    {
        var adapter = new TestAdapter();
        var skipped = new TestFactory("skipped", false, new TestAdapter());
        var selected = new TestFactory("selected", true, adapter);
        var catalog = new AuthoritativeGameHostAdapterCatalog([skipped, selected]);
        var registry = new GameplayModuleRegistry();
        var content = CreateContent();
        var playerStart = new AuthoritativePlayerStartConfiguration(100, 40, 20);

        var result = catalog.Activate(registry, content, playerStart);

        Assert.Same(adapter, result);
        Assert.Same(registry, selected.ReceivedRegistry);
        Assert.Same(content, selected.ReceivedContent);
        Assert.Same(playerStart, selected.ReceivedPlayerStart);
        Assert.Null(skipped.ReceivedRegistry);
    }

    [Fact]
    public void ConfiguredFactorySelectsOneCompatibleFactoryWhenSeveralMatch()
    {
        var firstAdapter = new TestAdapter();
        var secondAdapter = new TestAdapter();
        var catalog = new AuthoritativeGameHostAdapterCatalog(
            [
                new TestFactory("first", true, firstAdapter),
                new TestFactory("second", true, secondAdapter),
            ],
            "second");

        var result = catalog.Activate(
            new GameplayModuleRegistry(),
            CreateContent(),
            new AuthoritativePlayerStartConfiguration(0, 0, 0));

        Assert.Equal("second", catalog.SelectedFactoryId);
        Assert.Same(secondAdapter, result);
    }

    [Fact]
    public void RejectsMissingAmbiguousAndDuplicateFactories()
    {
        var adapter = new TestAdapter();
        Assert.Throws<ArgumentException>(() =>
            new AuthoritativeGameHostAdapterCatalog([]));
        Assert.Throws<ArgumentException>(() =>
            new AuthoritativeGameHostAdapterCatalog([
                new TestFactory("duplicate", true, adapter),
                new TestFactory("duplicate", false, adapter),
            ]));
        Assert.Throws<ArgumentException>(() =>
            new AuthoritativeGameHostAdapterCatalog(
                [new TestFactory("one", true, adapter)],
                " "));

        var none = new AuthoritativeGameHostAdapterCatalog([
            new TestFactory("none", false, adapter),
        ]);
        var ambiguous = new AuthoritativeGameHostAdapterCatalog([
            new TestFactory("first", true, adapter),
            new TestFactory("second", true, adapter),
        ]);

        Assert.Throws<InvalidOperationException>(() =>
            none.Activate(
                new GameplayModuleRegistry(),
                CreateContent(),
                new AuthoritativePlayerStartConfiguration(0, 0, 0)));
        Assert.Throws<InvalidOperationException>(() =>
            ambiguous.Activate(
                new GameplayModuleRegistry(),
                CreateContent(),
                new AuthoritativePlayerStartConfiguration(0, 0, 0)));
        var unknown = new AuthoritativeGameHostAdapterCatalog(
            [new TestFactory("one", true, adapter)],
            "missing");
        Assert.Throws<InvalidOperationException>(() =>
            unknown.Activate(
                new GameplayModuleRegistry(),
                CreateContent(),
                new AuthoritativePlayerStartConfiguration(0, 0, 0)));
        var incompatible = new AuthoritativeGameHostAdapterCatalog(
            [new TestFactory("one", false, adapter)],
            "one");
        Assert.Throws<InvalidOperationException>(() =>
            incompatible.Activate(
                new GameplayModuleRegistry(),
                CreateContent(),
                new AuthoritativePlayerStartConfiguration(0, 0, 0)));
    }

    [Fact]
    public void CollectsBuiltInModuleCandidatesWithoutLoadingThem()
    {
        var module = new TestModule();
        var catalog = new AuthoritativeGameHostAdapterCatalog([
            new TestFactory("test", true, new TestAdapter(), [module]),
        ]);

        var modules = catalog.CreateBuiltInModules();

        Assert.Same(module, Assert.Single(modules));
    }

    [Fact]
    public void ConfiguredFactoryOwnsBuiltInModuleSelection()
    {
        var firstModule = new TestModule();
        var secondModule = new TestModule();
        var catalog = new AuthoritativeGameHostAdapterCatalog(
            [
                new TestFactory("first", true, new TestAdapter(), [firstModule]),
                new TestFactory("second", true, new TestAdapter(), [secondModule]),
            ],
            "second");

        var modules = catalog.CreateBuiltInModules();

        Assert.Same(secondModule, Assert.Single(modules));
    }

    private static ComposedContent CreateContent()
    {
        return ComposedContent.Compose(new ContentManifest(
            "test",
            "1",
            [new ContentPack(
                "test",
                "1",
                [TsvTable.Parse("empty.tsv", "id\nvalue\n")])]));
    }

    private sealed class TestFactory : IAuthoritativeGameHostAdapterFactory
    {
        private readonly bool compatible;
        private readonly IAuthoritativeGameHostAdapter adapter;
        private readonly IReadOnlyList<IGameplayModule> modules;

        public TestFactory(
            string id,
            bool compatible,
            IAuthoritativeGameHostAdapter adapter,
            IReadOnlyList<IGameplayModule>? modules = null)
        {
            Id = id;
            this.compatible = compatible;
            this.adapter = adapter;
            this.modules = modules ?? [];
        }

        public string Id { get; }

        public GameplayModuleRegistry? ReceivedRegistry { get; private set; }

        public ComposedContent? ReceivedContent { get; private set; }

        public AuthoritativePlayerStartConfiguration? ReceivedPlayerStart { get; private set; }

        public IReadOnlyList<IGameplayModule> CreateBuiltInModules() => modules;

        public bool CanActivate(GameplayModuleRegistry registry) => compatible;

        public IAuthoritativeGameHostAdapter Activate(
            GameplayModuleRegistry registry,
            ComposedContent content,
            AuthoritativePlayerStartConfiguration playerStart)
        {
            ReceivedRegistry = registry;
            ReceivedContent = content;
            ReceivedPlayerStart = playerStart;
            return adapter;
        }
    }

    private sealed class TestModule : IGameplayModule
    {
        public GameplayModuleIdentity Identity { get; } = new("test", "1", "hash");

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
        }
    }

    private sealed class TestAdapter : IAuthoritativeGameHostAdapter
    {
        public void InitializeParticipant(
            string sessionId,
            AuthoritativeParticipantSelection selection)
        {
        }

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            return CommandExecutionResult.Accepted;
        }

        public void AdvanceTo(ulong tick)
        {
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            return new Snapshot { Tick = tick };
        }

        public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick) => null;

        public string ExportPlayerSave(string sessionId) => "save";

        public void ImportPlayerSave(string sessionId, string serializedSave)
        {
        }
    }
}
