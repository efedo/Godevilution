using Devilution.Data;
using Devilution.Engine;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Xunit;

namespace Devilution.Server.Tests;

/**
 * Local-only: drives the all-class adapter on a setup-built profile
 * (GODEVILUTION_NATIVE_ALL_CLASS_TOWN_PROFILE, retail-derived, never committed).
 */
public sealed class NativeAllClassTownProfileTests
{
    private const uint PlayerEntityId = 900_001;

    private static (DiabloAuthoritativeGameHostAdapterFactory Factory, GameplayModuleRegistry Registry, ComposedContent Content) Load()
    {
        var root = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_ALL_CLASS_TOWN_PROFILE");
        Assert.False(string.IsNullOrWhiteSpace(root), "Set GODEVILUTION_NATIVE_ALL_CLASS_TOWN_PROFILE to a setup-built all-class profile.");
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime", "native_town_geometry" }
            .Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(root!, name))).ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        var factory = new DiabloAuthoritativeGameHostAdapterFactory(DiabloAuthoritativeGameHostAdapterFactory.CathedralNativeAllClassTownId);
        var registry = new GameplayModuleRegistry();
        registry.Load(factory.CreateBuiltInModules(), new GameplayModuleContext(manifest, new DelegateAuthoritativeClock(() => 0)));
        return (factory, registry, ComposedContent.Compose(manifest));
    }

    private static NativeClassSelectingGameHostAdapter Activate()
    {
        var (factory, registry, content) = Load();
        return Assert.IsType<NativeClassSelectingGameHostAdapter>(factory.Activate(registry, content,
            new AuthoritativePlayerStartConfiguration(null, null, null) { RandomSeed = 2588, StartingLevelId = 0 }));
    }

    [Theory(Explicit = true)]
    [InlineData(0U, 4480)]
    [InlineData(1U, 2880)]
    [InlineData(2U, 1920)]
    public void EachClassStartsInTownAndItsSavedCohortRestoresOnlyIntoThatClass(uint classId, int baseLife)
    {
        var adapter = Activate();
        adapter.AdvanceTo(4);
        adapter.InitializeParticipant("player", new AuthoritativeParticipantSelection(classId) { EntityId = PlayerEntityId });
        adapter.AdvanceTo(30);
        var player = adapter.CreateSnapshot("player", PlayerEntityId, 30).Players.Single();
        Assert.Equal((classId, 0U, baseLife), (player.CharacterClassId, player.LevelId, player.BaseLifeMaximum));
        var saved = adapter.CaptureCohortState();

        var resumed = Activate();
        using (var prepared = resumed.PrepareCohortRestore(saved))
            resumed.CommitCohortRestore(prepared);
        Assert.Equal(classId, resumed.SelectedClassId);
        Assert.Equal(saved.SerializedState, resumed.CaptureCohortState().SerializedState);
    }
}
