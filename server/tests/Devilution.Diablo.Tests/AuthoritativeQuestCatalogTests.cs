using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeQuestCatalogTests
{
    [Fact]
    public void LoadsNativeQuestTypesInStableIdentityOrder()
    {
        var catalog = AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            "quest_id\tnative_quest_id\tname\tsingle_player_level\tmultiplayer_level\tlevel_type\tset_level\tsingle_player_only\n"
                + "3001\t0\tThe Magic Rock\t5\t-1\t\t0\t1\n"
                + "3013\t12\tThe Curse of King Leoric\t3\t3\tDTYPE_CATHEDRAL\t1\t0\n");

        Assert.Equal([3001U, 3013U], catalog.Definitions.Select(definition => definition.QuestId));
        var king = catalog.Definitions[1];
        Assert.Equal(12U, king.NativeQuestId);
        Assert.Equal("The Curse of King Leoric", king.Name);
        Assert.Equal((3, 3, "DTYPE_CATHEDRAL", 1, false), (
            king.SinglePlayerLevel,
            king.MultiplayerLevel,
            king.LevelType,
            king.SetLevel,
            king.SinglePlayerOnly));
    }

    [Fact]
    public void QuestStateRowsCanRequireAnExternalQuestType()
    {
        var types = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3001, 0, "The Magic Rock", 5, -1, string.Empty, 0, true),
        ]);
        const string header = "quest_id\tlevel_id\trequired_progress\tprogress\tcompleted\tunavailable\n";
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            header + "3001\t1\t3\t1\t0\t0\n",
            types);

        Assert.Equal(3001U, Assert.Single(quests).QuestId);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeQuestCatalog.LoadTsv(
                "quests.tsv",
                header + "3002\t1\t3\t1\t0\t0\n",
                types));
    }

    [Fact]
    public void QuestStateCanLoadAnUnavailablePoolExclusion()
    {
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            "quest_id\tlevel_id\trequired_progress\tprogress\tcompleted\tunavailable\n"
                + "3001\t1\t3\t0\t0\t1\n");

        var quest = Assert.Single(quests);
        Assert.True(quest.Unavailable);
        Assert.False(quest.Completed);
    }

    [Fact]
    public void QuestStateCanLoadNativeInitialAndLoggedFlags()
    {
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            "quest_id\tlevel_id\trequired_progress\tprogress\tcompleted\tinitial\tlogged\n"
                + "3001\t1\t3\t0\t0\t1\t1\n");

        var quest = Assert.Single(quests);
        Assert.False(quest.Active);
        Assert.True(quest.Logged);
    }

    [Fact]
    public void QuestStateCanLoadNativeVariablesAndMessageIdentity()
    {
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            "quest_id\tlevel_id\trequired_progress\tprogress\tcompleted\tvariable1\tvariable2\tmessage_id\n"
                + "3001\t1\t3\t0\t0\t2\t7\t123\n");

        var quest = Assert.Single(quests);
        Assert.Equal((2, 7, 123), (quest.Variable1, quest.Variable2, quest.MessageId));
    }
}
