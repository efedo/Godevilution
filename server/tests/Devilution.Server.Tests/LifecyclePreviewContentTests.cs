using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Validates the isolated authored profile through production content activation. */
public sealed class LifecyclePreviewContentTests
{
    [Theory]
    [InlineData(0U)]
    [InlineData(1U)]
    [InlineData(2U)]
    [InlineData(3U)]
    [InlineData(4U)]
    [InlineData(5U)]
    public void ProfilePreservesEveryClassStartingResourcesAndLoadout(uint classId)
    {
        var baseline = CreateSnapshot(preview: false, classId);
        var preview = CreateSnapshot(preview: true, classId);
        var expected = baseline.Players.Single().Clone();
        var actual = preview.Players.Single().Clone();
        // Item IDs belong to the activated world's allocator; compare the
        // complete loadout without requiring another world's allocation IDs.
        ClearItemIdentities(expected);
        ClearItemIdentities(actual);

        Assert.Equal(expected, actual);
        Assert.Equal(0U, actual.LevelId);
        Assert.Equal(baseline.WorldItems.ToArray(), preview.WorldItems.ToArray());
        Assert.Equal(baseline.Quests.ToArray(), preview.Quests.ToArray());
    }

    [Fact]
    public void ProfileUsesTheDeclaredDiagnosticEncountersWithoutInitialLoot()
    {
        var snapshot = CreateSnapshot(preview: true, classId: 2, startingLevelId: 1);
        var diagnosticMonsters = snapshot.Monsters.Where(monster => monster.EntityId is 9 or 10).ToArray();
        Assert.Equal(new uint[] { 9, 10 }, diagnosticMonsters.Select(monster => monster.EntityId).Order());
        Assert.All(diagnosticMonsters, monster => {
            Assert.Equal(1U, monster.LevelId);
            Assert.Equal(240, monster.HitPoints);
            Assert.Equal(240, monster.MaxHitPoints);
        });
        var baseline = CreateSnapshot(preview: false, classId: 2, startingLevelId: 1);
        Assert.Equal(baseline.WorldItems.ToArray(), snapshot.WorldItems.ToArray());
        Assert.Equal(baseline.Monsters.Where(monster => monster.EntityId is not (9 or 10)).ToArray(),
            snapshot.Monsters.Where(monster => monster.EntityId is not (9 or 10)).ToArray());
    }

    [Theory]
    [InlineData(0U, false)]
    [InlineData(5U, true)]
    public void LifecycleQuestAvailabilityUsesProductionSeedSelection(uint seed, bool available)
    {
        var baseline = CreateSnapshot(preview: false, classId: 2, randomSeed: seed);
        var snapshot = CreateSnapshot(preview: true, classId: 2, randomSeed: seed);
        Assert.Equal(baseline.Quests.ToArray(), snapshot.Quests.ToArray());
        var quest = Assert.Single(snapshot.Quests, quest => quest.QuestId == 3007);
        Assert.Equal(available, quest.Initial);
        Assert.Equal(!available, quest.Unavailable);
        Assert.False(quest.Completed);
        var interaction = Assert.Single(snapshot.QuestTownerInteractions,
            interaction => interaction.InteractionId == 300701);
        Assert.Equal(3007U, interaction.QuestId);
        Assert.Equal(2, interaction.TownerId);
        Assert.Equal(0U, interaction.LevelId);
        Assert.Equal(24, interaction.PositionX);
        Assert.Equal(32, interaction.PositionY);
        Assert.Equal(0U, interaction.ItemId);
        Assert.Equal(0U, interaction.RequiredItemId);
        Assert.False(interaction.ConsumeItem);
        Assert.Equal((int)Devilution.Diablo.AuthoritativeQuestTownerInteractionKind.Activate, interaction.Kind);
        // AllowInitial applies to completion stages, not this activation stage.
        Assert.False(interaction.AllowInitial);
    }

    private static Snapshot CreateSnapshot(bool preview, uint classId, uint startingLevelId = 0, uint randomSeed = 5)
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content");
        var packs = new List<ContentPack> {
            ContentPackLoader.LoadManifestDirectory(Path.Combine(contentRoot, "base")),
        };
        if (preview)
            packs.Add(ContentPackLoader.LoadManifestDirectory(Path.Combine(contentRoot, "town-lifecycle-preview")));
        var manifest = new ContentManifest("devilution.base", "0.1.0", packs);
        var registry = new GameplayModuleRegistry();
        registry.Load([new DiabloModule()],
            new GameplayModuleContext(manifest, new DelegateAuthoritativeClock(() => 0)));
        var adapter = new DiabloAuthoritativeGameHostAdapterFactory().Activate(
            registry,
            ComposedContent.Compose(manifest),
            new AuthoritativePlayerStartConfiguration(null, null, null) {
                StartingLevelId = startingLevelId,
                RandomSeed = randomSeed,
            });
        adapter.InitializeParticipant("profile-player", new AuthoritativeParticipantSelection(classId) {
            CharacterName = "ProfileHero",
        });
        return adapter.CreateSnapshot("profile-player", 1, 0);
    }

    private static void ClearItemIdentities(PlayerSnapshot player)
    {
        foreach (var item in player.Inventory)
            if (item.State is not null)
                item.State.StableItemId = 0;
        foreach (var item in player.Equipment)
            if (item.State is not null)
                item.State.StableItemId = 0;
        foreach (var item in player.Belt)
            if (item.State is not null)
                item.State.StableItemId = 0;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }
}
