using Devilution.Data;
using Devilution.Engine;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** The all-class adapter keeps every class run in lockstep until one class is chosen. */
public sealed class NativeClassSelectingGameHostAdapterTests
{
    private const uint PlayerEntityId = 900_001;

    private static StoreSimulationExecutor BaseRun()
    {
        var basePack = ContentPackLoader.LoadManifestDirectory(Path.Combine(FindRepositoryRoot(), "server", "content", "base"));
        var manifest = new ContentManifest("devilution.base", "0.1.0", [basePack]);
        var registry = new GameplayModuleRegistry();
        var factory = new DiabloAuthoritativeGameHostAdapterFactory();
        registry.Load(factory.CreateBuiltInModules(), new GameplayModuleContext(manifest, new DelegateAuthoritativeClock(() => 0)));
        return (StoreSimulationExecutor)factory.Activate(registry, ComposedContent.Compose(manifest),
            new AuthoritativePlayerStartConfiguration(null, null, null) { RandomSeed = 7, StartingLevelId = 0 });
    }

    private static (NativeClassSelectingGameHostAdapter Adapter, IReadOnlyDictionary<uint, StoreSimulationExecutor> Runs) Create()
    {
        var runs = new Dictionary<uint, StoreSimulationExecutor> { [0] = BaseRun(), [1] = BaseRun(), [2] = BaseRun() };
        return (new NativeClassSelectingGameHostAdapter(runs), runs);
    }

    [Fact]
    public void TheFirstParticipantSelectsItsClassRunAndOnlyThatRunAdvancesAfterwards()
    {
        var (adapter, runs) = Create();
        Assert.Null(adapter.SelectedClassId);
        adapter.AdvanceTo(5);
        Assert.All(runs.Values, run => Assert.Equal(5UL, run.CaptureCohortState().Tick));
        Assert.Throws<InvalidOperationException>(() => adapter.CreateSnapshot("rogue", PlayerEntityId, 5));

        adapter.InitializeParticipant("rogue", new AuthoritativeParticipantSelection(1) { EntityId = PlayerEntityId });
        Assert.Equal(1U, adapter.SelectedClassId);
        adapter.AdvanceTo(9);
        Assert.Equal(9UL, runs[1].CaptureCohortState().Tick);
        Assert.Equal(5UL, runs[0].CaptureCohortState().Tick);
        Assert.Equal(1U, adapter.CreateSnapshot("rogue", PlayerEntityId, 9).Players.Single().CharacterClassId);
        Assert.Equal(runs[1].CaptureCohortState().SerializedState, adapter.CaptureCohortState().SerializedState);
    }

    [Fact]
    public void AnUnknownClassLeavesEveryRunOpen()
    {
        var (adapter, _) = Create();
        Assert.Throws<NotSupportedException>(() => adapter.InitializeParticipant("monk", new AuthoritativeParticipantSelection(3)));
        Assert.Null(adapter.SelectedClassId);
        adapter.InitializeParticipant("default", new AuthoritativeParticipantSelection(null) { EntityId = PlayerEntityId });
        Assert.Equal(0U, adapter.SelectedClassId);
    }

    [Fact]
    public void CohortRestoreSelectsOnlyAnUnambiguousRun()
    {
        var (source, _) = Create();
        var empty = source.CaptureCohortState();
        var (fresh, freshRuns) = Create();
        using (var prepared = fresh.PrepareCohortRestore(empty))
            fresh.CommitCohortRestore(prepared);
        Assert.Null(fresh.SelectedClassId);

        source.InitializeParticipant("sorcerer", new AuthoritativeParticipantSelection(2) { EntityId = PlayerEntityId });
        source.AdvanceTo(3);
        var saved = source.CaptureCohortState();
        // Identical base runs all accept the cohort, so the class cannot be told apart.
        Assert.Throws<InvalidDataException>(() => fresh.PrepareCohortRestore(saved));
        Assert.Null(fresh.SelectedClassId);

        var single = new NativeClassSelectingGameHostAdapter(new Dictionary<uint, StoreSimulationExecutor> { [2] = BaseRun() }, 2);
        using (var prepared = single.PrepareCohortRestore(saved))
            single.CommitCohortRestore(prepared);
        Assert.Equal(2U, single.SelectedClassId);
        Assert.Equal(saved.SerializedState, single.CaptureCohortState().SerializedState);
        Assert.Throws<InvalidOperationException>(() => fresh.CommitCohortRestore(single.PrepareCohortRestore(saved)));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "server", "content", "base")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
