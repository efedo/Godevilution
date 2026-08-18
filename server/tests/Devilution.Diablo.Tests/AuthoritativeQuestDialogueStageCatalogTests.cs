using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeQuestDialogueStageCatalogTests
{
    [Fact]
    public void ExactVariableTwoGateTakesPrecedenceOverWildcard()
    {
        var catalog = new AuthoritativeQuestDialogueStageCatalog([
            new(3003, 1, -1, 145, 3, 0),
            new(3003, 1, 2, 146, 4, 2),
        ]);

        Assert.True(catalog.TryGet(3003, 1, 2, out var exact));
        Assert.Equal(146, exact.MessageId);
        Assert.True(catalog.TryGet(3003, 1, 7, out var wildcard));
        Assert.Equal(145, wildcard.MessageId);
    }

    [Fact]
    public void TsvLoaderResolvesStableQuestIdentityAndOrdersStages()
    {
        var catalog = AuthoritativeQuestDialogueStageCatalog.LoadTsv(
            "quest_dialogue_stages.tsv",
            "quest_id\tquest_symbol\trequired_variable1\trequired_variable2\tmessage_id\tnext_variable1\tnext_variable2\treward_kind\treward_item_level\treward_offset_x\treward_offset_y\tfollow_up_kind\tfollow_up_amount\n"
            + "3003\tgharbads_weak\t1\t-1\t145\t3\t0\tNone\t0\t0\t0\tWorldMutation\t7\n"
            + "3003\tgharbads_weak\t0\t-1\t144\t1\t0\tRandomUniqueMonsterDrop\t0\t1\t1\tNone\t0\n");

        Assert.Equal([3003U, 3003U], catalog.Definitions.Select(stage => stage.QuestId));
        Assert.Equal([0, 1], catalog.Definitions.Select(stage => stage.RequiredVariable1));
        Assert.True(catalog.ContainsQuest(3003));
        Assert.False(catalog.ContainsQuest(3012));
        var reward = Assert.Single(catalog.Definitions, stage => stage.RequiredVariable1 == 0);
        Assert.Equal(AuthoritativeQuestDialogueRewardKind.RandomUniqueMonsterDrop, reward.RewardKind);
        Assert.Equal((1, 1), (reward.RewardOffsetX, reward.RewardOffsetY));
        var followUp = Assert.Single(catalog.Definitions, stage => stage.RequiredVariable1 == 1);
        Assert.Equal(AuthoritativeQuestDialogueFollowUpKind.WorldMutation, followUp.FollowUpKind);
        Assert.Equal(7U, followUp.FollowUpAmount);
    }

    [Fact]
    public void ValidationRejectsInvalidFollowUpValues()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeQuestDialogueStageCatalog([
            new(3003, 0, -1, 144, 1, 0,
                FollowUpKind: AuthoritativeQuestDialogueFollowUpKind.WorldMutation),
        ]));
    }

    [Fact]
    public void ValidationRequiresUniqueMonsterAndRejectsLegacyDialogueFields()
    {
        var catalog = new AuthoritativeQuestDialogueStageCatalog([
            new(3003, 0, -1, 144, 1, 0),
        ]);
        var withLegacyItem = new AuthoritativeQuestState(
            3003,
            0,
            1,
            completionUniqueIdentity: "Gharbad",
            dialogueItemId: 1);

        Assert.Throws<InvalidDataException>(() => catalog.ValidateQuests([withLegacyItem]));
    }
}
