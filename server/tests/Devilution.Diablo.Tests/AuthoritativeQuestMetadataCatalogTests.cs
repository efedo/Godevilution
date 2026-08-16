using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeQuestMetadataCatalogTests
{
    [Fact]
    public void ResolvesStableQuestSymbolsInPoolEntries()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("quest", "magic_rock", 3001),
        ]);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(3001, 0, "The Magic Rock", 5, -1, string.Empty, 0, true),
        ]);
        var pools = AuthoritativeQuestPoolCatalog.LoadTsv(
            "quest_pools.tsv",
            "pool_id\tquest_id\tquest_symbol\n1\t3001\tmagic_rock\n",
            questTypes,
            stableIds);

        Assert.Equal(3001U, Assert.Single(pools.Definitions).QuestId);
    }

    [Fact]
    public void LoadsNativeSetLevelNamesAndQuestPools()
    {
        var setLevels = AuthoritativeSetLevelNameCatalog.LoadTsv(
            "set_level_names.tsv",
            "set_level_id\tsymbol\tdisplay_name\n"
                + "0\tSL_NONE\t\n"
                + "1\tSL_SKELKING\tSkeleton King's Lair\n"
                + "2\tSL_BONECHAMB\tChamber of Bone\n"
                + "3\tSL_MAZE\tMaze\n"
                + "4\tSL_POISONWATER\tPoisoned Water Supply\n"
                + "5\tSL_VILEBETRAYER\tArchbishop Lazarus' Lair\n"
                + "6\tSL_ARENA_CHURCH\tChurch Arena\n"
                + "7\tSL_ARENA_HELL\tHell Arena\n"
                + "8\tSL_ARENA_CIRCLE_OF_LIFE\tCircle of Life Arena\n");
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(3001, 0, "The Magic Rock", 5, -1, string.Empty, 0, true),
            new AuthoritativeQuestTypeDefinition(3013, 12, "The Curse of King Leoric", 3, 3, "DTYPE_CATHEDRAL", 1, false),
        ]);
        var pools = AuthoritativeQuestPoolCatalog.LoadTsv(
            "quest_pools.tsv",
            "pool_id\tquest_id\n3\t3013\n3\t3001\n",
            questTypes);

        Assert.Equal("Skeleton King's Lair", setLevels.GetDisplayName(1));
        Assert.Equal([3001U, 3013U], pools.GetPool(3).Select(entry => entry.QuestId));
        Assert.Throws<InvalidDataException>(() => AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            "quest_id\tnative_quest_id\tname\tsingle_player_level\tmultiplayer_level\tlevel_type\tset_level\tsingle_player_only\n"
                + "3001\t0\tThe Magic Rock\t5\t-1\t\t9\t1\n",
            setLevels));
    }

    [Fact]
    public void ResolvesStableSetLevelSymbolsIncludingNativeZero()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("set_level", "SL_NONE", 0),
            new StableContentId("set_level", "SL_SKELKING", 1),
            new StableContentId("set_level", "SL_BONECHAMB", 2),
            new StableContentId("set_level", "SL_MAZE", 3),
            new StableContentId("set_level", "SL_POISONWATER", 4),
            new StableContentId("set_level", "SL_VILEBETRAYER", 5),
            new StableContentId("set_level", "SL_ARENA_CHURCH", 6),
            new StableContentId("set_level", "SL_ARENA_HELL", 7),
            new StableContentId("set_level", "SL_ARENA_CIRCLE_OF_LIFE", 8),
        ]);
        var setLevels = AuthoritativeSetLevelNameCatalog.LoadTsv(
            "set_level_names.tsv",
            "set_level_id\tsymbol\tdisplay_name\n"
                + "0\tSL_NONE\tnone\n"
                + "1\tSL_SKELKING\tSkeleton King's Lair\n"
                + "2\tSL_BONECHAMB\tChamber of Bone\n"
                + "3\tSL_MAZE\tMaze\n"
                + "4\tSL_POISONWATER\tPoisoned Water Supply\n"
                + "5\tSL_VILEBETRAYER\tArchbishop Lazarus' Lair\n"
                + "6\tSL_ARENA_CHURCH\tChurch Arena\n"
                + "7\tSL_ARENA_HELL\tHell Arena\n"
                + "8\tSL_ARENA_CIRCLE_OF_LIFE\tCircle of Life Arena\n",
            stableIds);
        var questTypes = AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            "quest_id\tquest_symbol\tnative_quest_id\tname\tsingle_player_level\tmultiplayer_level\tlevel_type\tset_level\tset_level_symbol\tsingle_player_only\n"
                + "3001\tmagic_rock\t0\tThe Magic Rock\t5\t-1\t\t0\tSL_NONE\t1\n"
                + "3013\tking_leoric\t12\tThe Curse of King Leoric\t3\t3\tDTYPE_CATHEDRAL\t1\tSL_SKELKING\t0\n",
            setLevels,
            new StableContentIdCatalog([
                new StableContentId("quest", "magic_rock", 3001),
                new StableContentId("quest", "king_leoric", 3013),
                new StableContentId("set_level", "SL_NONE", 0),
                new StableContentId("set_level", "SL_SKELKING", 1),
            ]));

        Assert.Equal(0, questTypes.Definitions[0].SetLevel);
        Assert.Equal(1, questTypes.Definitions[1].SetLevel);
        Assert.Equal(string.Empty, setLevels.GetDisplayName(0));
    }

    [Fact]
    public void RejectsQuestPoolReferencesOutsideTheQuestCatalog()
    {
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(3001, 0, "The Magic Rock", 5, -1, string.Empty, 0, true),
        ]);
        Assert.Throws<InvalidDataException>(() => AuthoritativeQuestPoolCatalog.LoadTsv(
            "quest_pools.tsv",
            "pool_id\tquest_id\n1\t3002\n",
            questTypes));
    }

    [Fact]
    public void SelectsNativeQuestPoolExclusionsDeterministically()
    {
        var pools = new AuthoritativeQuestPoolCatalog([
            new AuthoritativeQuestPoolEntry(1, 3001),
            new AuthoritativeQuestPoolEntry(1, 3002),
            new AuthoritativeQuestPoolEntry(2, 3003),
            new AuthoritativeQuestPoolEntry(2, 3004),
        ]);

        Assert.Equal(
            pools.SelectUnavailableQuestIds(42),
            pools.SelectUnavailableQuestIds(42));
        Assert.Equal(2, pools.SelectUnavailableQuestIds(42).Count);
        Assert.Single(pools.SelectUnavailableQuestIds(988045466), questId => questId is 3003 or 3004);
    }

    [Fact]
    public void AppliesNativePoolExclusionsToServerOwnedQuestState()
    {
        var pools = new AuthoritativeQuestPoolCatalog([
            new AuthoritativeQuestPoolEntry(1, 3001),
            new AuthoritativeQuestPoolEntry(1, 3002),
            new AuthoritativeQuestPoolEntry(2, 3003),
            new AuthoritativeQuestPoolEntry(2, 3004),
        ]);
        var quests = new[] {
            new AuthoritativeQuestState(3001, 1, 1),
            new AuthoritativeQuestState(3002, 1, 1),
            new AuthoritativeQuestState(3003, 1, 1),
            new AuthoritativeQuestState(3004, 1, 1),
            new AuthoritativeQuestState(3999, 1, 1),
        };

        AuthoritativeQuestCatalog.ApplySinglePlayerPoolSelection(quests, pools, 42);

        Assert.Equal(2, quests.Count(quest => quest.Unavailable));
        Assert.False(quests.Single(quest => quest.QuestId == 3999).Unavailable);
        Assert.Equal(
            pools.SelectUnavailableQuestIds(42).OrderBy(id => id),
            quests.Where(quest => quest.Unavailable).Select(quest => quest.QuestId).OrderBy(id => id));
    }
}
