using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
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

    /** Native multiplayer (batch 1.5a): two players of any supported classes share one run and walk into L1. */
    [Theory(Explicit = true)]
    [InlineData(0U, 0U)]
    [InlineData(0U, 1U)]
    [InlineData(2U, 1U)]
    public void TwoMultiplayerParticipantsEnterCathedralL1(uint firstClassId, uint secondClassId)
    {
        var multiplayer = EnterCathedralL1(AuthoritativeGameMode.Multiplayer,
            [("first", PlayerEntityId, firstClassId), ("second", PlayerEntityId + 1, secondClassId)]);
        Assert.Equal(new[] { 1U, 1U }, multiplayer.Players.OrderBy(player => player.EntityId).Select(player => player.LevelId));
        var singlePlayer = EnterCathedralL1(AuthoritativeGameMode.SinglePlayer, [("first", PlayerEntityId, firstClassId)]);
        // Same seed and generating class: multiplayer places 150% scatter monsters at full life.
        var multiplayerL1 = multiplayer.Monsters.Where(monster => monster.LevelId == 1).ToArray();
        var singlePlayerL1 = singlePlayer.Monsters.Where(monster => monster.LevelId == 1).ToArray();
        Assert.NotEmpty(singlePlayerL1);
        Assert.True(multiplayerL1.Length > singlePlayerL1.Length,
            $"Multiplayer L1 has {multiplayerL1.Length} monsters, single-player {singlePlayerL1.Length}.");
        Assert.True(multiplayerL1.Max(monster => monster.MaxHitPointsQ6) > singlePlayerL1.Max(monster => monster.MaxHitPointsQ6));
    }

    private static Snapshot EnterCathedralL1(AuthoritativeGameMode gameMode, (string Session, uint EntityId, uint ClassId)[] participants)
    {
        var (factory, registry, content) = Load();
        var adapter = factory.Activate(registry, content, new AuthoritativePlayerStartConfiguration(null, null, null) {
            RandomSeed = 2588, StartingLevelId = 0, GameMode = gameMode,
        });
        ulong tick = 4;
        adapter.AdvanceTo(tick);
        foreach (var (session, entityId, classId) in participants)
            adapter.InitializeParticipant(session, new AuthoritativeParticipantSelection(classId) { EntityId = entityId });
        tick = 30;
        adapter.AdvanceTo(tick);
        foreach (var (session, entityId, classId) in participants) {
            var snapshot = adapter.CreateSnapshot(session, entityId, tick);
            var player = snapshot.Players.Single(candidate => candidate.EntityId == entityId);
            Assert.Equal((classId, 0U), (player.CharacterClassId, player.LevelId));
            var stairs = snapshot.LevelGeometry.Single(level => level.LevelId == 0).WarpTriggers
                .First(trigger => trigger.TargetLevelId == 1);
            var result = adapter.Execute(session, new Command {
                MoveRequested = new MoveRequested { TargetCell = true, FollowPath = true, TargetX = stairs.TriggerX, TargetY = stairs.TriggerY },
            }, tick + 1);
            Assert.True(result.Succeeded, $"{session} could not path to the L1 stairs: {result.RejectReason}.");
            for (var step = 0; step < 4000; step++) {
                adapter.AdvanceTo(++tick);
                if (adapter.CreateSnapshot(session, entityId, tick).Players.Single(candidate => candidate.EntityId == entityId).LevelId == 1)
                    break;
            }
        }
        return adapter.CreateSnapshot(participants[0].Session, participants[0].EntityId, tick);
    }
}
