using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeItemQuestEffectCatalogTests
{
    [Fact]
    public void ResolvesStableItemSymbolForItemEffect()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("item", "cathedral_map", 43),
            new StableContentId("quest", "grave_matters", 3017),
        ]);
        var catalog = AuthoritativeItemQuestEffectCatalog.LoadTsv(
            "item_quest_effects.tsv",
            "item_id\titem_symbol\tquest_id\tquest_symbol\teffect_kind\teffect_amount\n"
                + "43\tcathedral_map\t3017\tgrave_matters\tActivateQuest\t1\n",
            stableIds);

        Assert.Equal(43U, Assert.Single(catalog.Definitions).ItemId);
    }

    [Fact]
    public void RejectsStableItemSymbolNumericMismatchForItemEffect()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("item", "cathedral_map", 43),
            new StableContentId("quest", "grave_matters", 3017),
        ]);

        Assert.Throws<InvalidDataException>(() => AuthoritativeItemQuestEffectCatalog.LoadTsv(
            "item_quest_effects.tsv",
            "item_id\titem_symbol\tquest_id\tquest_symbol\teffect_kind\teffect_amount\n"
                + "44\tcathedral_map\t3017\tgrave_matters\tActivateQuest\t1\n",
            stableIds));
    }

    [Fact]
    public void ResolvesStableQuestSymbolForItemEffect()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("quest", "grave_matters", 3017),
        ]);
        var catalog = AuthoritativeItemQuestEffectCatalog.LoadTsv(
            "item_quest_effects.tsv",
            "item_id\tquest_id\tquest_symbol\teffect_kind\teffect_amount\n"
                + "43\t3017\tgrave_matters\tActivateQuest\t1\n",
            stableIds);

        Assert.Equal(3017U, Assert.Single(catalog.Definitions).QuestId);
    }

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

    [Fact]
    public void GraveObjectConsumesCathedralMapAndCompletesQuest()
    {
        var items = new AuthoritativeItemCatalog([CreateItem(43, 22)]);
        // Grave Matters is scoped to the crypt (level 17), but its offering
        // object is placed in town (level 0), matching the native OpenGrave flow.
        var quest = new AuthoritativeQuestState(
            3017,
            17,
            1,
            active: true,
            logged: true,
            completionVariable1: 7,
            completionVariable2: 3,
            completionActivateQuestId: 3018);
        var followOnQuest = new AuthoritativeQuestState(3018, 17, 1, active: false);
        var mapState = AuthoritativeItemState.Empty with {
            ItemIndex = 22,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var inventory = new OwnedStoreItem(0, 0, 99, 0, 0, mapState);
        var worldObject = new AuthoritativeWorldObject(
            40,
            6004,
            0,
            1,
            0,
            questId: 3017,
            effectKind: AuthoritativeObjectEffectKind.QuestItemCompletion,
            effectAmount: 43);
        var world = new DiabloWorldState(
            objects: [worldObject],
            quests: [quest, followOnQuest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayerWithInventory(inventory);

        var result = processor.Execute(
            player,
            new DiabloOperateObjectIntent(40),
            8,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(worldObject.Activated);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal(7, quest.Variable1);
        Assert.Equal(3, quest.Variable2);
        Assert.True(followOnQuest.Active);
        Assert.Empty(player.Inventory);
        Assert.All(player.InventoryGrid, cell => Assert.Equal(-1, cell));
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

    private static AuthoritativePlayerState CreatePlayerWithInventory(OwnedStoreItem item)
    {
        var player = CreatePlayer();
        player.Inventory.Add(item);
        player.InventoryGrid[0] = 0;
        return player;
    }
}
