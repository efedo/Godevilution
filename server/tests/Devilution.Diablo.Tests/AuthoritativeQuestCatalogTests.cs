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

    [Fact]
    public void QuestStateCanLoadDialogueVariableTransitions()
    {
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            "quest_id\tlevel_id\trequired_progress\tdialogue_variable1\tdialogue_variable2\n"
                + "3001\t1\t1\t3\t4\n");

        var quest = Assert.Single(quests);
        Assert.Equal((3, 4), (quest.DialogueVariable1, quest.DialogueVariable2));
    }

    [Fact]
    public void QuestStateCanLoadCompletionVariableTransitions()
    {
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            "quest_id\tlevel_id\trequired_progress\tcompletion_variable1\tcompletion_variable2\n"
                + "3001\t1\t1\t5\t6\n");

        var quest = Assert.Single(quests);
        Assert.Equal((5, 6), (quest.CompletionVariable1, quest.CompletionVariable2));
    }

    [Fact]
    public void QuestStateCanLoadFollowOnActivationAndRoutePolicy()
    {
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            "quest_id\tlevel_id\trequired_progress\tcompletion_activate_quest_id\troute_activation\n"
                + "3001\t1\t1\t3002\t0\n",
            new AuthoritativeQuestTypeCatalog([
                new AuthoritativeQuestTypeDefinition(
                    3001, 0, "First", 1, -1, string.Empty, 0, false),
            ]));

        var quest = Assert.Single(quests);
        Assert.Equal(3002U, quest.CompletionActivateQuestId);
        Assert.False(quest.ActivateOnRoute);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeQuestCatalog.ValidateActivationTargets(quests));
    }

    [Fact]
    public void QuestStateCanLoadDiabloCompletionTrigger()
    {
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            "quest_id\tlevel_id\trequired_progress\tcompletion_diablo\n"
                + "3006\t15\t1\t1\n");

        Assert.True(Assert.Single(quests).CompletionDiablo);
    }

    [Fact]
    public void QuestStateCanLoadAnOptionalMonsterCompletionTrigger()
    {
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            "quest_id\tlevel_id\trequired_progress\tprogress\tcompleted\tcompletion_monster_id\n"
                + "3001\t1\t1\t0\t0\t12\n");

        Assert.Equal(12U, Assert.Single(quests).CompletionMonsterId);
    }

    [Fact]
    public void QuestCompletionTargetsResolveAgainstMonsterTypes()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                12,
                12,
                "Test Monster",
                1,
                16,
                1,
                1,
                1,
                1,
                1,
                1,
                1,
                1,
                false,
                false,
                false),
        ]);
        var quest = new AuthoritativeQuestState(3001, 1, 1, completionMonsterId: 12);

        AuthoritativeQuestCatalog.ValidateMonsterCompletionTargets([quest], monsterTypes);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeQuestCatalog.ValidateMonsterCompletionTargets(
                [new AuthoritativeQuestState(3001, 1, 1, completionMonsterId: 13)],
                monsterTypes));
        AuthoritativeQuestCatalog.ValidateMonsterCompletionTargets(
            [new AuthoritativeQuestState(3006, 15, 1, completionDiablo: true)],
            monsterTypes);
    }

    [Fact]
    public void QuestStateTablesMergeAdditivelyAndRejectDuplicateIds()
    {
        var baseQuests = AuthoritativeQuestCatalog.LoadTsv(
            "quests.tsv",
            "quest_id\tlevel_id\trequired_progress\tprogress\tcompleted\n"
                + "3001\t1\t3\t0\t0\n");
        var additionalQuests = AuthoritativeQuestCatalog.LoadTsv(
            "quest_states.tsv",
            "quest_id\tlevel_id\trequired_progress\tprogress\tcompleted\n"
                + "3017\t17\t1\t0\t0\n");

        var merged = AuthoritativeQuestCatalog.Merge(
            baseQuests,
            additionalQuests,
            "hellfire/quest_states.tsv");
        Assert.Equal([3001U, 3017U], merged.Select(quest => quest.QuestId));
        Assert.Throws<InvalidDataException>(() => AuthoritativeQuestCatalog.Merge(
            baseQuests,
            AuthoritativeQuestCatalog.LoadTsv(
                "quest_states.tsv",
                "quest_id\tlevel_id\trequired_progress\tprogress\tcompleted\n"
                    + "3001\t17\t1\t0\t0\n"),
            "hellfire/quest_states.tsv"));
    }

    [Fact]
    public void QuestStateCanLoadAUniqueMonsterCompletionTrigger()
    {
        var quests = AuthoritativeQuestCatalog.LoadTsv(
            "quest_states.tsv",
            "quest_id\tlevel_id\trequired_progress\tcompletion_unique_identity\n"
                + "3021\t17\t1\tDefiler\n");

        Assert.Equal("Defiler", Assert.Single(quests).CompletionUniqueIdentity);
    }

    [Fact]
    public void QuestStateCanLoadACompletionRewardItem()
    {
        var quest = Assert.Single(AuthoritativeQuestCatalog.LoadTsv(
            "quest_states.tsv",
            "quest_id\tlevel_id\trequired_progress\tcompletion_reward_item_id\tcompletion_reward_item_level\n"
                + "3021\t17\t1\t43\t7\n"));

        Assert.Equal((43U, 7), (quest.CompletionRewardItemId, quest.CompletionRewardItemLevel));
    }

    [Fact]
    public void CompletionRewardItemsMustResolveInTheActiveItemCatalog()
    {
        var quest = new AuthoritativeQuestState(
            3021,
            17,
            1,
            completionRewardItemId: 43);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                1,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                1,
                1,
                true),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeQuestCatalog.ValidateRewardItems([quest], items));

        Assert.Contains("unknown completion reward item 43", exception.Message, StringComparison.Ordinal);
    }
}
