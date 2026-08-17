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

    [Fact]
    public void ExplicitSourceKindOverridesNativeMiscellaneousIdentity()
    {
        var charged = AuthoritativeItemState.Empty with {
            MiscId = 99,
            SpellId = 7,
            MaxCharges = 40,
            SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
        };
        var suppressed = charged with {
            MiscId = 23,
            SpellSourceKind = DiabloItemSpellSourceKind.None,
        };

        Assert.Equal(
            DiabloItemSpellSourceKind.ChargedItem,
            DiabloCastSourceRules.ClassifyItem(charged));
        Assert.True(DiabloCastSourceRules.IsMatchingChargedItem(charged, 7));
        Assert.Equal(
            DiabloItemSpellSourceKind.None,
            DiabloCastSourceRules.ClassifyItem(suppressed));
        Assert.False(DiabloCastSourceRules.IsMatchingChargedItem(suppressed, 7));
    }

    [Fact]
    public void ActiveItemSourceMetadataOverridesLegacyStateFields()
    {
        var definition = new AuthoritativeItemDefinition(
            7,
            99,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
                NativeItemIndex = 101,
                MiscId = 99,
                SpellId = 7,
                MaxCharges = 40,
                SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
            };
        var items = new AuthoritativeItemCatalog([definition]);
        var legacyState = AuthoritativeItemState.Empty with {
            ItemIndex = 101,
            ItemType = 1,
            MiscId = 99,
            SpellId = 7,
            MaxCharges = 40,
        };

        Assert.Equal(
            DiabloItemSpellSourceKind.ChargedItem,
            DiabloCastSourceRules.ClassifyItem(legacyState, items: items));
        Assert.Equal(
            7U,
            DiabloCastSourceRules.GetItemSpellId(legacyState, items: items));
        Assert.True(DiabloCastSourceRules.IsMatchingChargedItem(
            legacyState,
            7,
            items));
    }

    [Fact]
    public void ExternalItemSourceMetadataCanResolveWithoutANativeItemIndex()
    {
        var chargedDefinition = new AuthoritativeItemDefinition(
            7,
            99,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
                NativeItemIndex = 101,
                MiscId = 121,
                SpellId = 7,
                MaxCharges = 40,
                SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
            };
        var consumableDefinition = chargedDefinition with {
            ItemId = 8,
            NativeItemIndex = 102,
            MiscId = 122,
            SpellSourceKind = DiabloItemSpellSourceKind.ConsumableItem,
        };
        var items = new AuthoritativeItemCatalog([
            chargedDefinition,
            consumableDefinition,
        ]);
        var charged = AuthoritativeItemState.Empty with {
            MiscId = 121,
            SpellId = 7,
            MaxCharges = 40,
        };
        var consumable = AuthoritativeItemState.Empty with {
            MiscId = 122,
            SpellId = 7,
        };

        Assert.Equal(
            DiabloItemSpellSourceKind.ChargedItem,
            DiabloCastSourceRules.ClassifyItem(charged, items: items));
        Assert.Equal(
            DiabloItemSpellSourceKind.ConsumableItem,
            DiabloCastSourceRules.ClassifyItem(consumable, items: items));
        Assert.Equal(7U, DiabloCastSourceRules.GetItemSpellId(charged, items: items));
        Assert.Equal(7U, DiabloCastSourceRules.GetItemSpellId(consumable, items: items));
        Assert.True(DiabloCastSourceRules.IsMatchingChargedItem(charged, 7, items));
        Assert.True(DiabloCastSourceRules.IsMatchingConsumableItem(
            consumable,
            7,
            spells: null,
            items));
    }

    [Fact]
    public void ExplicitlyUnusableConsumableItemsCannotBeCast()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 5, 0, 0, 0, 0) {
                ConsumableMiscId = 47,
            },
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 0, 1, 1, 0, 0, 0, 0, 0, 1, 1, true) {
                NativeItemIndex = 101,
                MiscId = 47,
                SpellId = 7,
                SpellSourceKind = DiabloItemSpellSourceKind.ConsumableItem,
                Usable = false,
            },
        ]);
        var state = AuthoritativeItemState.Empty with {
            ItemIndex = 101,
            MiscId = 47,
            SpellId = 7,
            SpellSourceKind = DiabloItemSpellSourceKind.ConsumableItem,
        };

        Assert.False(DiabloCastSourceRules.IsMatchingConsumableItem(
            state,
            7,
            spells,
            items));
    }
}
