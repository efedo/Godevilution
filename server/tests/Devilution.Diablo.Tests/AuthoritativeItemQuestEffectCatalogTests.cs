using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeItemQuestEffectCatalogTests
{
    [Fact]
    public void LoadsAndValidatesItemQuestEffect()
    {
        var catalog = AuthoritativeItemQuestEffectCatalog.LoadTsv(
            "item_quest_effects.tsv",
            "item_id\tquest_id\teffect_kind\teffect_amount\n"
                + "43\t3017\tActivateQuest\t1\n");
        var items = new AuthoritativeItemCatalog([CreateItem(43, 22)]);
        AuthoritativeQuestState[] quests = [
            new AuthoritativeQuestState(3017, 0, 1, active: false),
        ];

        catalog.ValidateItems(items);
        catalog.ValidateQuests(quests);

        var effect = Assert.Single(catalog.Definitions);
        Assert.Equal(43U, effect.ItemId);
        Assert.Equal(3017U, effect.QuestId);
        Assert.Equal(AuthoritativeItemQuestEffectKind.ActivateQuest, effect.EffectKind);
        Assert.Equal(1, effect.EffectAmount);
    }

    [Fact]
    public void RejectsUnknownQuestIdentity()
    {
        var catalog = new AuthoritativeItemQuestEffectCatalog([
            new AuthoritativeItemQuestEffectDefinition(
                43,
                3017,
                AuthoritativeItemQuestEffectKind.ActivateQuest,
                1),
        ]);

        Assert.Throws<InvalidDataException>(() => catalog.ValidateQuests([]));
    }

    [Fact]
    public void CathedralMapPickupActivatesGraveQuestAndSetsNativeVariable()
    {
        var items = new AuthoritativeItemCatalog([CreateItem(43, 22)]);
        var effects = new AuthoritativeItemQuestEffectCatalog([
            new AuthoritativeItemQuestEffectDefinition(
                43,
                3017,
                AuthoritativeItemQuestEffectKind.ActivateQuest,
                1),
        ]);
        var quest = new AuthoritativeQuestState(3017, 0, 1, active: false);
        var item = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            99,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 22,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
        var world = new DiabloWorldState(worldItems: [item], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            itemQuestEffects: effects);
        var player = CreatePlayer();

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            7,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.Equal(1, quest.Variable1);
        Assert.False(quest.Completed);
        Assert.Single(player.Inventory);
    }

    private static AuthoritativeItemDefinition CreateItem(
        uint itemId,
        int nativeItemIndex)
    {
        return new AuthoritativeItemDefinition(
            itemId,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            false) {
            NativeItemIndex = nativeItemIndex,
            Name = "Cathedral Map",
            IdentifiedName = "Cathedral Map",
        };
    }

    private static AuthoritativePlayerState CreatePlayer()
    {
        return new AuthoritativePlayerState(
            0,
            0,
            50,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            50,
            1,
            0,
            0,
            inventory: []) {
            EntityId = 1,
        };
    }
}
