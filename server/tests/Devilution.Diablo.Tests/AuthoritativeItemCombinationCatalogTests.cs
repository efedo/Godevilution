using Devilution.Diablo;
using Devilution.Domain.Inventory;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeItemCombinationCatalogTests
{
    [Fact]
    public void LoadsAndValidatesContentOwnedCombination()
    {
        var catalog = AuthoritativeItemCombinationCatalog.LoadTsv(
            "item_combinations.tsv",
            "result_item_id\trequired_item_ids\n"
                + "2004\t2001, 2002,2003\n");
        var items = new AuthoritativeItemCatalog([
            CreateDefinition(2001, 42),
            CreateDefinition(2002, 43),
            CreateDefinition(2003, 44),
            CreateDefinition(2004, 45),
        ]);

        catalog.ValidateItems(items);

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal(2004U, definition.ResultItemId);
        Assert.Equal([2001U, 2002U, 2003U], definition.RequiredItemIds);
    }

    [Fact]
    public void RejectsCombinationReferencingUnknownItem()
    {
        var catalog = new AuthoritativeItemCombinationCatalog([
            new AuthoritativeItemCombinationDefinition(2004, [2001, 9999]),
        ]);
        var items = new AuthoritativeItemCatalog([
            CreateDefinition(2001, 42),
            CreateDefinition(2004, 45),
        ]);

        Assert.Throws<InvalidDataException>(() => catalog.ValidateItems(items));
    }

    [Fact]
    public void PickupCombinesNativeHellfireNotesIntoTheReconstructedNote()
    {
        var items = new AuthoritativeItemCatalog([
            CreateDefinition(2001, 42),
            CreateDefinition(2002, 43),
            CreateDefinition(2003, 44),
            CreateDefinition(2004, 45),
        ]);
        var combinations = new AuthoritativeItemCombinationCatalog([
            new AuthoritativeItemCombinationDefinition(2004, [2001, 2002, 2003]),
        ]);
        var world = new DiabloWorldState(worldItems: [
            CreateWorldItem(20, 1, 0, 42, 101),
            CreateWorldItem(21, 1, 1, 43, 102),
            CreateWorldItem(22, 0, 1, 44, 103),
        ]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: items,
            itemCombinations: combinations);
        var player = CreatePlayer();

        foreach (var entityId in new uint[] { 20, 21, 22 }) {
            var result = processor.Execute(
                player,
                new DiabloPickupWorldItemIntent(entityId),
                entityId,
                [player]);
            Assert.True(result.Succeeded);
        }

        var item = Assert.Single(player.Inventory);
        Assert.Equal(45, item.State.ItemIndex);
        Assert.NotEqual(0U, item.ItemSeed);
        Assert.NotEqual(0U, item.State.CreateInfo);
        Assert.Equal(0, InventoryGridLayout.FindAnchor(player.InventoryGrid, 0));
        Assert.Empty(world.WorldItems);
    }

    private static AuthoritativeItemDefinition CreateDefinition(
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
            Name = $"Item {itemId}",
            IdentifiedName = $"Item {itemId}",
        };
    }

    private static AuthoritativeWorldItem CreateWorldItem(
        uint entityId,
        int positionX,
        int positionY,
        int itemIndex,
        uint itemSeed)
    {
        return new AuthoritativeWorldItem(
            entityId,
            0,
            positionX,
            positionY,
            itemSeed,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = itemIndex,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
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
