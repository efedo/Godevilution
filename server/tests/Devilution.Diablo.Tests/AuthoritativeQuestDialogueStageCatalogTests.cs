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
            "quest_id\tquest_symbol\trequired_variable1\trequired_variable2\tmessage_id\tnext_variable1\tnext_variable2\n"
            + "3003\tgharbads_weak\t1\t-1\t145\t3\t0\n"
            + "3003\tgharbads_weak\t0\t-1\t144\t1\t0\n");

        Assert.Equal([3003U, 3003U], catalog.Definitions.Select(stage => stage.QuestId));
        Assert.Equal([0, 1], catalog.Definitions.Select(stage => stage.RequiredVariable1));
        Assert.True(catalog.ContainsQuest(3003));
        Assert.False(catalog.ContainsQuest(3012));
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
