using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloCastSourceRulesTests
{
    [Fact]
    public void ClassifiesChargedAndCatalogMappedConsumableItems()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 5, 0, 0, 0, 0) {
                ConsumableMiscId = 47,
            },
        ]);
        var staff = AuthoritativeItemState.Empty with {
            MiscId = 23,
            SpellId = 7,
            MaxCharges = 40,
        };
        var rune = AuthoritativeItemState.Empty with {
            MiscId = 47,
            SpellId = 0,
        };

        Assert.Equal(
            DiabloItemSpellSourceKind.ChargedItem,
            DiabloCastSourceRules.ClassifyItem(staff, spells));
        Assert.Equal(
            DiabloItemSpellSourceKind.ConsumableItem,
            DiabloCastSourceRules.ClassifyItem(rune, spells));
    }

    [Fact]
    public void UnmappedOrUnchargedItemsRemainUnspecified()
    {
        var item = AuthoritativeItemState.Empty with {
            MiscId = 99,
            SpellId = 7,
            MaxCharges = 40,
        };

        Assert.Equal(
            DiabloItemSpellSourceKind.None,
            DiabloCastSourceRules.ClassifyItem(item));
    }
}
