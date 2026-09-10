using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeItemCatalogTests
{
    [Fact]
    public void ShippedItemGenerationUsesTheProductionSchema()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "item_generation.tsv");
        var table = TsvTable.Parse("item_generation.tsv", File.ReadAllText(path));

        AuthoritativeItemGenerationRules.ValidateExternalizedProduction(table);
    }

    [Fact]
    public void ProductionItemGenerationValidationRejectsPartialRows()
    {
        var table = TsvTable.Parse(
            "item_generation.tsv",
            "magicChanceBase\tmagicChancePerLevel\tuniqueChanceNormal\tuniqueChanceUnique\tbonusLevelsUnique\tprefixPercent\tsuffixPercent\tonlygoodChance\tnoDropPercent\tgoldPercent\n"
                + "10\t1\t1\t15\t4\t25\t66\t66\t59\t74\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeItemGenerationRules.ValidateExternalizedProduction(table));

        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShippedItemsUseTheProductionSchema()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "items.tsv");
        var table = TsvTable.Parse("items.tsv", File.ReadAllText(path));

        AuthoritativeItemCatalog.ValidateExternalizedProduction(table);
    }

    [Fact]
    public void ProductionItemValidationRejectsNumericOnlyIdentityHeaders()
    {
        var table = TsvTable.Parse(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tnative_item_index\tspell_source_kind\tname\tidentified_name\tcursor_graphic\titem_action_kind\titem_action_amount\n"
                + "1\t1\t75\t75\t1\tNone\tShort Sword\tShort Sword\tSHORT_SWORD\tNone\t1\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeItemCatalog.ValidateExternalizedProduction(table));

        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvesNormalizedItemIdsThroughStableContentIds()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("item", "short_sword", 7),
        ]);
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_symbol\titem_type\tvalue\tidentified_value\n"
                + "7\tshort_sword\t1\t75\t100\n",
            stableIds: stableIds);

        Assert.Equal(7U, catalog.GetRequired(7).ItemId);
        Assert.Equal("short_sword", catalog.GetRequired(7).StableSymbol);
    }

    [Fact]
    public void RejectsNormalizedItemIdNumericMismatch()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("item", "short_sword", 7),
        ]);

        Assert.Throws<InvalidDataException>(() => AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_symbol\titem_type\tvalue\tidentified_value\n"
                + "8\tshort_sword\t1\t75\t100\n",
            stableIds: stableIds));
    }

    [Fact]
    public void ResolvesNormalizedItemSpellIdsThroughStableContentIds()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "charged_bolt", 30),
        ]);
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tspell_id\tspell_symbol\n"
                + "7\t1\t75\t100\t30\tcharged_bolt\n",
            stableIds: stableIds);

        Assert.Equal(30, catalog.GetRequired(7).SpellId);
    }

    [Fact]
    public void RejectsNormalizedItemSpellIdNumericMismatch()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "charged_bolt", 30),
        ]);

        Assert.Throws<InvalidDataException>(() => AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tspell_id\tspell_symbol\n"
                + "7\t1\t75\t100\t3\tcharged_bolt\n",
            stableIds: stableIds));
    }

    [Fact]
    public void GeneratesProtocolItemStateFromExternalDefinition()
    {
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tdurability\tmax_durability\tinventory_width\tinventory_height\tidentified\tmin_armor_class\tmax_armor_class\tmin_monster_level\tdrop_rate\tusable\n"
                + "7\t1\t75\t100\t3\t9\t2\t1\t1\t10\t20\t12\t3\ttrue\n");

        var state = catalog.Generate(7, 42);
        Assert.Equal(42U, state.CreateInfo);
        Assert.Equal(7U, state.StableItemId);
        Assert.Equal(7, state.ItemIndex);
        Assert.Equal(100, state.IdentifiedValue);
        Assert.Equal(2, state.InventoryWidth);
        Assert.Equal(1, state.InventoryHeight);
        Assert.Equal(12, state.ArmorClass);
        Assert.Equal(12, catalog.GetRequired(7).MinimumMonsterLevel);
        Assert.Equal(3, catalog.GetRequired(7).DropRate);
        Assert.True(catalog.GetRequired(7).Usable);
    }

    [Fact]
    public void BoundsGeneratedCreateInfoForLargeAuthoritativeSeeds()
    {
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\n"
                + "7\t1\t75\t100\n");

        var state = catalog.Generate(7, uint.MaxValue);

        Assert.Equal(0U, state.CreateInfo);
    }

    [Fact]
    public void LoadsValidatedItemActionMagnitudeFromContent()
    {
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_action_kind\titem_action_amount\n"
                + "2142\t0\t0\t0\tIncreaseAllAttributes\t3\n");

        var definition = catalog.GetRequired(2142);
        Assert.Equal(DiabloItemActionKind.IncreaseAllAttributes, definition.ItemActionKind);
        Assert.Equal(3, definition.ItemActionAmount);
    }

    [Fact]
    public void LoadsItemActionDialogueIdentityFromContent()
    {
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_action_kind\titem_action_message_id\n"
                + "2004\t0\t0\t0\tReadQuestNote\t322\n");

        Assert.Equal(322, catalog.GetRequired(2004).ItemActionMessageId);
    }

    [Fact]
    public void RejectsItemActionDialogueIdentityWithoutAnAction()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_action_message_id\n"
                + "7\t1\t75\t100\t322\n"));
    }

    [Fact]
    public void RejectsNonPositiveItemActionMagnitude()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_action_amount\n"
                + "7\t1\t75\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidSpellBookTemplateThreshold()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tbook_template_minimum_item_level\n"
                + "2201\t6\t75\t75\t0\n"));
    }

    [Fact]
    public void StateDefinitionMatchingPrefersStableIdentityAndRetainsLegacyFallback()
    {
        var definition = new AuthoritativeItemDefinition(7, 1, 75, 100, 1, 2, 0, 0, 0, 1, 1, true) {
            NativeItemIndex = 99,
        };
        var catalog = new AuthoritativeItemCatalog([definition]);

        Assert.True(catalog.IsStateForDefinition(
            AuthoritativeItemState.Empty with {
                StableItemId = 7,
                ItemIndex = 12,
            },
            definition));
        Assert.False(catalog.IsStateForDefinition(
            AuthoritativeItemState.Empty with {
                StableItemId = 8,
                ItemIndex = 99,
            },
            definition));
        Assert.True(catalog.IsStateForDefinition(
            AuthoritativeItemState.Empty with { ItemIndex = 99 },
            definition));
    }

    [Fact]
    public void AuthoredMetadataHydrationPreservesOmittedCurrentCharges()
    {
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                7,
                1,
                75,
                100,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                true) {
                MiscId = 121,
                SpellId = 3,
                SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
                MaxCharges = 12,
                ItemActionKind = DiabloItemActionKind.IncreaseStrength,
                Name = "Charged Item",
                IdentifiedName = "Identified Charged Item",
                NativeCursorGraphic = 19,
            },
        ]);

        var resolved = catalog.ResolveAuthoredMetadata(
            AuthoritativeItemState.Empty with { ItemIndex = 999, MiscId = 121 });

        Assert.Equal(75, resolved.Value);
        Assert.Equal(100, resolved.IdentifiedValue);
        Assert.Equal(12, resolved.MaxCharges);
        Assert.Equal(0, resolved.Charges);
        Assert.Equal(3, resolved.SpellId);
        Assert.Equal(DiabloItemSpellSourceKind.ChargedItem, resolved.SpellSourceKind);
        Assert.Equal(DiabloItemActionKind.IncreaseStrength, resolved.ItemActionKind);
        Assert.Equal("Charged Item", resolved.BaseName);
        Assert.Equal("Identified Charged Item", resolved.IdentifiedName);
        Assert.Equal(19, resolved.NativeCursorGraphic);
    }

    [Fact]
    public void AuthoredMetadataHydrationRestoresSeededArmorRange()
    {
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 6, 100, 100, 0, 0, 0, 20, 20, 1, 1, true) {
                MinArmorClass = 10,
                MaxArmorClass = 20,
            },
        ]);

        var state = AuthoritativeItemState.Empty with {
            StableItemId = 7,
            ArmorClass = 0,
        };

        var first = catalog.ResolveAuthoredMetadata(state, 42);
        var second = catalog.ResolveAuthoredMetadata(state, 42);

        Assert.Equal(12, first.ArmorClass);
        Assert.Equal(first.ArmorClass, second.ArmorClass);
    }

    [Fact]
    public void AuthoredMetadataHydrationLeavesRandomArmorUnresolvedWithoutSeed()
    {
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 6, 100, 100, 0, 0, 0, 20, 20, 1, 1, true) {
                MinArmorClass = 10,
                MaxArmorClass = 20,
            },
        ]);

        var resolved = catalog.ResolveAuthoredMetadata(
            AuthoritativeItemState.Empty with { StableItemId = 7 });

        Assert.Equal(0, resolved.ArmorClass);
    }

    [Fact]
    public void AuthoredPresentationHydrationPreservesExplicitOverrides()
    {
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 100, 0, 0, 0, 0, 0, 1, 1, true) {
                Name = "Base Item",
                IdentifiedName = "Identified Base Item",
                NativeCursorGraphic = 19,
            },
        ]);

        var resolved = catalog.ResolveAuthoredMetadata(
            AuthoritativeItemState.Empty with {
                StableItemId = 7,
                UniqueId = 4,
                BaseName = "Unique Item",
                IdentifiedName = "Unique Item",
                NativeCursorGraphic = 23,
            });

        Assert.Equal("Unique Item", resolved.BaseName);
        Assert.Equal("Unique Item", resolved.IdentifiedName);
        Assert.Equal(23, resolved.NativeCursorGraphic);
    }

    [Fact]
    public void AuthoredIdentityHydrationFillsOmittedClassificationAndRequirements()
    {
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 2, 75, 100, 1, 3, 0, 0, 0, 1, 1, true) {
                NativeItemIndex = 99,
                EquipLocation = 7,
                ItemClass = 3,
                MinimumStrength = 25,
                MinimumMagic = 12,
                MinimumDexterity = 18,
                Flags = 0x20,
                HellfireDamageArmorFlags = 0x40,
            },
        ]);

        var resolved = catalog.ResolveAuthoredMetadata(
            AuthoritativeItemState.Empty with { StableItemId = 7 });

        Assert.Equal(2, resolved.ItemType);
        Assert.Equal(99, resolved.ItemIndex);
        Assert.Equal(7, resolved.EquipLocation);
        Assert.Equal(3, resolved.ItemClass);
        Assert.Equal(25, resolved.MinimumStrength);
        Assert.Equal(12, resolved.MinimumMagic);
        Assert.Equal(18, resolved.MinimumDexterity);
        Assert.Equal(0x20U, resolved.Flags);
        Assert.Equal(0x40, resolved.HellfireDamageArmorFlags);
    }

    [Fact]
    public void RejectsMalformedNormalizedBooleanMetadata()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tidentified\tusable\n"
                + "7\t1\t75\t100\t1\t2\n"));
    }

    [Fact]
    public void ProjectsExternalPresentationMetadataIntoGeneratedItemState()
    {
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tdurability\tmax_durability\tname\tidentified_name\tnative_cursor_graphic\n"
                + "7\t1\t75\t100\t3\t9\tShort Sword\tSword\t12\n");

        var state = catalog.Generate(7, 42);

        Assert.Equal("Short Sword", state.BaseName);
        Assert.Equal("Sword", state.IdentifiedName);
        Assert.Equal(12, state.NativeCursorGraphic);
    }

    [Fact]
    public void LoadsNormalizedSpecialEffectsIntoAuthoritativeFlags()
    {
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tspecial_effects\tflags\n"
                + "7\t1\t75\t100\tKnockback,NoMana\t0\n");

        var flags = catalog.GetRequired(7).Flags;
        Assert.NotEqual(0U, flags & AuthoritativeEquipmentAttackEffectRules.KnockbackFlag);
        Assert.NotEqual(0U, flags & AuthoritativeEquipmentManaRules.NoManaFlag);
    }

    [Fact]
    public void RejectsUnknownNormalizedSpecialEffects()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tspecial_effects\n"
                + "7\t1\t75\t100\tNotANativeEffect\n"));
    }

    [Fact]
    public void LoadsNormalizedHellfireEffectsIntoGeneratedItemState()
    {
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\thellfire_effects\n"
                + "7\t1\t75\t100\tDoppelganger,ACAgainstDemons\n");

        var state = catalog.Generate(7, 42);

        Assert.Equal(
            AuthoritativeEquipmentIncomingDamageRules.DoppelgangerFlag
                | AuthoritativeEquipmentIncomingDamageRules.AgainstDemonsFlag,
            state.HellfireDamageArmorFlags);
    }

    [Fact]
    public void RejectsUnknownNormalizedHellfireEffects()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\thellfire_effects\n"
                + "7\t1\t75\t100\tNotAHellfireEffect\n"));
    }

    [Fact]
    public void ResolvesNormalizedCursorSymbolsThroughTheExternalCatalog()
    {
        var cursors = AuthoritativeItemCursorCatalog.LoadTsv(
            "item_cursor_graphics.tsv",
            "symbol\tnative_cursor_graphic\nSHORT_SWORD\t64\n");
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tcursor_graphic\n"
                + "7\t1\t75\t100\tSHORT_SWORD\n",
            cursorCatalog: cursors);

        Assert.Equal(64, catalog.Generate(7, 42).NativeCursorGraphic);
    }

    [Fact]
    public void RejectsUnknownNormalizedCursorSymbols()
    {
        var cursors = AuthoritativeItemCursorCatalog.LoadTsv(
            "item_cursor_graphics.tsv",
            "symbol\tnative_cursor_graphic\nSHORT_SWORD\t64\n");

        Assert.Throws<InvalidDataException>(() => AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tcursor_graphic\n"
                + "7\t1\t75\t100\tUNKNOWN\n",
            cursorCatalog: cursors));
    }

    [Fact]
    public void ResolvesLegacyCursorSymbolsWhenCatalogIsAvailable()
    {
        var cursors = AuthoritativeItemCursorCatalog.LoadTsv(
            "item_cursor_graphics.tsv",
            "symbol\tnative_cursor_graphic\nSHORT_SWORD\t64\n");
        var catalog = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\n"
                + "IDI_WARRIOR\t1\tWeapon\tOne-handed\tSHORT_SWORD\tSword\tNONE\tShort Sword\t\n",
            cursorCatalog: cursors);

        Assert.Equal(64, catalog.Generate(1, 42).NativeCursorGraphic);
    }

    [Fact]
    public void RejectsDuplicateCursorGraphicIds()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeItemCursorCatalog.LoadTsv(
            "item_cursor_graphics.tsv",
            "symbol\tnative_cursor_graphic\nSHORT_SWORD\t64\nSWORD\t64\n"));
    }

    [Fact]
    public void RejectsInvalidExternalPresentationMetadata()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, true) {
                Name = new string('x', 64),
                NativeCursorGraphic = 256,
                MinimumMonsterLevel = -1,
                DropRate = 256,
            },
        ]));

        Assert.Throws<InvalidDataException>(() => AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tmin_armor_class\n"
                + "7\t1\t1\t1\t3\n"));
    }

    [Fact]
    public void NativeIndexLookupRejectsConflictingDuplicateTags()
    {
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, true) {
                NativeItemIndex = 7,
                GenerationTags = new HashSet<string>(["Sword"], StringComparer.OrdinalIgnoreCase),
            },
            new AuthoritativeItemDefinition(2, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, true) {
                NativeItemIndex = 7,
                GenerationTags = new HashSet<string>(["Axe"], StringComparer.OrdinalIgnoreCase),
            },
        ]);

        Assert.False(catalog.TryGetByNativeItemIndex(7, out _));
    }

    [Fact]
    public void LoadsAndPropagatesExplicitSpellSourceKind()
    {
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tdurability\tmax_durability\tspell_id\tmax_charges\tspell_source_kind\n"
                + "7\t10\t75\t100\t3\t9\t42\t40\tChargedItem\n");

        var state = catalog.Generate(7, 42);

        Assert.Equal(DiabloItemSpellSourceKind.ChargedItem, state.SpellSourceKind);
        Assert.Equal(
            DiabloItemSpellSourceKind.ChargedItem,
            DiabloCastSourceRules.ClassifyItem(state));
        Assert.True(DiabloCastSourceRules.IsMatchingChargedItem(state, 42));
    }

    [Fact]
    public void LegacyArmorRollUsesTheNativeLcgSequence()
    {
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 2, 75, 100, 0, 0, 0, 3, 9, 1, 1, true) {
                MinArmorClass = 10,
                MaxArmorClass = 20,
            },
        ]);

        var first = catalog.Generate(7, 42);
        var second = catalog.Generate(7, 42);

        Assert.Equal(12, first.ArmorClass);
        Assert.Equal(first.ArmorClass, second.ArmorClass);
    }

    [Fact]
    public void StoreRowsMayReferenceExternalItemDefinitions()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 100, 0, 0, 0, 3, 9, 2, 1, true),
        ]);
        var catalog = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\titem_id\n1\t0\t42\t75\t7\n",
            items);

        var item = Assert.Single(catalog.GetItems(1));
        Assert.Equal(7, item.State.ItemIndex);
        Assert.Equal(2, item.State.InventoryWidth);
    }

    [Fact]
    public void StoreRowsResolveStableItemSymbols()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 100, 0, 0, 0, 3, 9, 1, 1, true),
        ]);
        var stableIds = new StableContentIdCatalog([
            new StableContentId("item", "short_sword", 7),
        ]);
        var catalog = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\titem_id\titem_symbol\n"
                + "1\t0\t42\t75\t7\tshort_sword\n",
            items,
            stableIds: stableIds);

        Assert.Equal(7, Assert.Single(catalog.GetItems(1)).State.ItemIndex);
    }

    [Fact]
    public void StoreRowsRejectStableItemSymbolNumericMismatch()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 100, 0, 0, 0, 3, 9, 1, 1, true),
        ]);
        var stableIds = new StableContentIdCatalog([
            new StableContentId("item", "short_sword", 7),
        ]);

        Assert.Throws<InvalidDataException>(() => StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\titem_id\titem_symbol\n"
                + "1\t0\t42\t75\t8\tshort_sword\n",
            items,
            stableIds: stableIds));
    }

    [Fact]
    public void RejectsDuplicateItemDefinitions()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, true),
            new AuthoritativeItemDefinition(7, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, true),
        ]));
    }

    [Fact]
    public void ShippedItemAffixesUseTheProductionSchema()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "item_affixes.tsv");
        var table = TsvTable.Parse("item_affixes.tsv", File.ReadAllText(path));

        AuthoritativeItemAffixCatalog.ValidateExternalizedProduction(table);
    }

    [Fact]
    public void ProductionItemAffixValidationRejectsPartialHeaders()
    {
        var table = TsvTable.Parse(
            "item_affixes.tsv",
            "name\tpower\tvalue1\tvalue2\tmin_level\titem_tags\tchance\tuseful\tis_suffix\n"
                + "Tin\tTOHIT\t1\t5\t1\tWeapon\t2\ttrue\t0\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeItemAffixCatalog.ValidateExternalizedProduction(table));

        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AppliesSeedStableAffixesOnlyWhenLevelAndTagsMatch()
    {
        var affixes = AuthoritativeItemAffixCatalog.LoadTsv(
            "item_affixes.tsv",
            "name\tpower\tvalue1\tvalue2\tmin_level\titem_tags\tchance\tuseful\tis_suffix\n"
                + "Bronze\tTOHIT\t1\t5\t1\tWeapon\t1\t1\t0\n"
                + "Strength\tSTR\t2\t2\t1\tWeapon\t1\t1\t1\n",
            new AuthoritativeItemGenerationRules(10, 1, 1, 15, 4, 100, 100, 66, 59, 74));
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 100, 0, 0, 0, 3, 9, 1, 1, true) {
                GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
            },
        ], affixes);

        var levelZero = catalog.Generate(7, 42, 0);
        var first = catalog.Generate(7, 42, 10);
        var second = catalog.Generate(7, 42, 10);

        Assert.Equal(0, levelZero.PlusToHit);
        Assert.Equal(first, second);
        Assert.InRange(first.PlusToHit, 1, 5);
        Assert.Equal(2, first.PlusStrength);
    }

    [Fact]
    public void LoadsNormalizedLegacyAffixMetadataAndRandomMode()
    {
        var catalog = AuthoritativeItemAffixCatalog.LoadTsv(
            "item_affixes.tsv",
            "name\tpower\tvalue1\tvalue2\tmin_level\titem_tags\tchance\tuseful\tis_suffix\talignment\tmin_value\tmax_value\tvalue_multiplier\tlegacy_random\tlegacy_value_range\n"
                + "Bronze\tTOHIT\t1\t5\t1\tWeapon\t1\ttrue\t0\tAny\t100\t500\t2\t1\t1\n"
                + "Strength\tSTR\t2\t2\t1\tWeapon\t1\ttrue\t1\tAny\t0\t0\t0\t1\t1\n");

        Assert.True(catalog.UsesLegacyRandom);
        var prefix = Assert.Single(catalog.Prefixes);
        Assert.Equal(100, prefix.MinimumValue);
        Assert.Equal(500, prefix.MaximumValue);
        Assert.Equal(2, prefix.ValueMultiplier);
        Assert.True(prefix.HasLegacyValueRange);
        Assert.Equal("Any", prefix.Alignment);
    }

    [Fact]
    public void LoadsLegacyPrefixAndSuffixColumnsAndGenerationProbabilities()
    {
        var rules = AuthoritativeItemGenerationRules.LoadTsv(
            "item_generation.tsv",
            "magicChanceBase\tmagicChancePerLevel\tuniqueChanceNormal\tuniqueChanceUnique\tbonusLevelsUnique\tprefixPercent\tsuffixPercent\tonlygoodChance\tnoDropPercent\tgoldPercent\n"
                + "10\t1\t1\t15\t4\t0\t100\t66\t59\t74\n");
        var catalog = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n"
                + "Bronze\tTOHIT\t1\t5\t1\tWeapon\tAny\t1\ttrue\t0\t0\t0\n",
            "item_suffixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n"
                + "Strength\tSTR\t2\t2\t1\tWeapon\tAny\t1\ttrue\t0\t0\t0\n",
            rules);

        var definition = new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 1, 1, 1, 1, true) {
            GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
        };
        var item = new AuthoritativeItemCatalog([definition], catalog).Generate(7, 42, 10);

        Assert.Equal(0, item.PlusToHit);
        Assert.Equal(2, item.PlusStrength);
    }

    [Fact]
    public void LegacyAffixGenerationUsesTheNativeRandomStreamAndRecalculatesValue()
    {
        var rules = new AuthoritativeItemGenerationRules(10, 1, 1, 15, 4, 100, 0, 0, 0, 0);
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n"
                + "Bronze\tTOHIT\t1\t5\t1\tWeapon\tAny\t1\ttrue\t1\t5\t1\n",
            "item_suffixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n",
            rules);
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 1, 1, 1, 1, true) {
                GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
            },
        ], affixes);

        var item = catalog.Generate(7, 42, 10);

        Assert.Equal(3, item.PlusToHit);
        Assert.Equal(78, item.IdentifiedValue);
    }

    [Fact]
    public void LegacyMonsterDropQualityUsesTheSharedRandomStream()
    {
        var rules = new AuthoritativeItemGenerationRules(0, 0, 1, 15, 4, 100, 0, 0, 0, 0);
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n"
                + "Bronze\tTOHIT\t1\t1\t1\tWeapon\tAny\t1\ttrue\t0\t0\t0\n",
            "item_suffixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n",
            rules);
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 1, 1, 1, 1, true) {
                GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
            },
        ], affixes);

        var normal = catalog.GenerateDrop(7, 42, 10);
        var forced = catalog.GenerateDrop(7, 42, 10, onlyGood: true);

        Assert.Equal(0, normal.Magical);
        Assert.Equal(1, forced.Magical);
        Assert.Equal(1, forced.PlusToHit);
    }

    [Fact]
    public void RandomUniqueMonsterDropHonorsNativeCandidateRules()
    {
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 1, 1, 1, 1, true) {
                MinimumMonsterLevel = 10,
                DropRate = 1,
            },
            new AuthoritativeItemDefinition(25, 11, 0, 0, 0, 0, 0, 0, 0, 1, 1, true) {
                DropRate = 1,
            },
            new AuthoritativeItemDefinition(90, 0, 1, 1, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 3,
                DropRate = 1,
            },
            new AuthoritativeItemDefinition(114, 0, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                DropRate = 1,
            },
        ]);

        var lowLevel = catalog.GenerateRandomUniqueMonsterDrop(42, 1);
        var first = catalog.GenerateRandomUniqueMonsterDrop(42, 10);
        var second = catalog.GenerateRandomUniqueMonsterDrop(42, 10);

        Assert.Equal(114U, lowLevel.StableItemId);
        Assert.Equal(first, second);
        Assert.Contains(first.StableItemId, new uint[] { 7, 114 });
    }

    [Fact]
    public void RandomUniqueMonsterDropUsesContentBookAdmissionWhenCatalogIsPresent()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 0, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 99,
                DropRate = 1,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);

        var state = items.GenerateRandomUniqueMonsterDrop(
            42,
            monsterLevel: 1,
            spellBooks: books);

        Assert.Equal(114U, state.StableItemId);
        Assert.Equal(7, state.SpellId);
    }

    [Fact]
    public void RandomUniqueMonsterDropUsesContentBookTagsWithoutSpellBookCatalog()
    {
        var definition = new AuthoritativeItemDefinition(
            114,
            99,
            75,
            75,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            MiscId = 99,
            SpellId = 7,
            ItemActionKind = DiabloItemActionKind.ReadBook,
            GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            DropRate = 1,
        };
        var items = new AuthoritativeItemCatalog([definition]);

        var state = items.GenerateRandomUniqueMonsterDrop(42, monsterLevel: 1);

        Assert.Equal(114U, state.StableItemId);
        Assert.Equal(7, state.SpellId);
        Assert.Equal(DiabloItemActionKind.ReadBook, state.ItemActionKind);
    }

    [Fact]
    public void RandomUniqueMonsterDropDoesNotUseLegacyBookMiscFallbackForTaggedNonBooks()
    {
        var definition = new AuthoritativeItemDefinition(
            114,
            0,
            75,
            75,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            MiscId = 25,
            GenerationTags = new HashSet<string>(["Misc"], StringComparer.OrdinalIgnoreCase),
            DropRate = 1,
        };
        var items = new AuthoritativeItemCatalog([definition]);

        Assert.Throws<InvalidDataException>(
            () => items.GenerateRandomUniqueMonsterDrop(42, monsterLevel: 1));
    }

    [Fact]
    public void RandomUniqueMonsterDropUsesContentGoldClassificationWhenTagsArePresent()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(25, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true) {
                DropRate = 1,
                GenerationTags = new HashSet<string>(["Gold"], StringComparer.OrdinalIgnoreCase),
            },
            new AuthoritativeItemDefinition(114, 0, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 99,
                DropRate = 1,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);

        for (uint seed = 1; seed <= 64; seed++) {
            var state = items.GenerateRandomUniqueMonsterDrop(
                seed,
                monsterLevel: 1,
                spellBooks: books);
            Assert.Equal(114U, state.StableItemId);
        }
    }

    [Fact]
    public void GeneratesDynamicSpellBookWhenAWorldDropReferencesABookTemplate()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);

        var first = items.GenerateDrop(114, 42, 1, spellBooks: books);
        var second = items.GenerateDrop(114, 42, 1, spellBooks: books);

        Assert.Equal(first, second);
        Assert.Equal(7, first.SpellId);
        Assert.Equal(DiabloItemActionKind.ReadBook, first.ItemActionKind);
        Assert.Equal("Book of Firebolt", first.BaseName);
        Assert.Equal(100, first.Value);
    }

    [Fact]
    public void CatalogExposesNativeMonsterDropEligibility()
    {
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 1, 1, 1, 1, true) {
                MinimumMonsterLevel = 10,
            },
        ]);

        Assert.False(catalog.CanDropAtMonsterLevel(7, 9));
        Assert.True(catalog.CanDropAtMonsterLevel(7, 10));
        Assert.Equal(7, catalog.GenerateDrop(7, 42, 9).ItemIndex);
    }

    [Fact]
    public void LegacyMonsterDropSelectsTheLastEligibleUniqueAfterQualityResolution()
    {
        var rules = new AuthoritativeItemGenerationRules(100, 0, 100, 15, 4, 100, 0, 0, 0, 0);
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n",
            "item_suffixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n",
            rules);
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1, "First", 7, 1, 100, []),
            new AuthoritativeUniqueItemDefinition(2, "Second", 7, 1, 200, []),
        ], usesLegacyRandom: true);
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 20, 20, 1, 1, true),
        ], affixes, uniques);

        var item = catalog.GenerateDrop(7, 42, 10);

        Assert.Equal(2, item.UniqueId);
        Assert.Equal(2, item.Magical);
        Assert.Equal(200, item.Value);
    }

    [Fact]
    public void GeneratesUniqueItemsFromBaseDefinitionsAndMultiplePowerRows()
    {
        var cursors = AuthoritativeItemCursorCatalog.LoadTsv(
            "item_cursor_graphics.tsv",
            "symbol\tnative_cursor_graphic\nCLEAVER\t106\n");
        var uniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            "unique_id\tname\tbase_item_id\tmin_level\tvalue\tpower\tvalue1\tvalue2\tcursor_graphic\n"
                + "1001\tEmber Blade\t7\t1\t3650\tFIREDAM\t1\t10\tCLEAVER\n"
                + "1001\tEmber Blade\t7\t1\t3650\tTOHIT\t10\t10\tCLEAVER\n",
            cursors);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);

        var item = items.GenerateUnique(1001, 42, 5);

        Assert.Equal(1001, item.UniqueId);
        Assert.Equal("Ember Blade", item.BaseName);
        Assert.Equal("Ember Blade", item.IdentifiedName);
        Assert.Equal(3650, item.Value);
        Assert.Equal(106, item.NativeCursorGraphic);
        Assert.Equal(10, item.PlusToHit);
        Assert.InRange(item.FireMinDamage, 1, 10);
    }

    [Fact]
    public void ShippedUniqueItemsUseTheProductionSchema()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "unique_items.tsv");
        var table = TsvTable.Parse("unique_items.tsv", File.ReadAllText(path));

        AuthoritativeUniqueItemCatalog.ValidateExternalizedProduction(table);
    }

    [Fact]
    public void ProductionUniqueItemValidationRejectsNumericOnlyIdentityHeaders()
    {
        var table = TsvTable.Parse(
            "unique_items.tsv",
            "unique_id\tname\tbase_item_id\tmin_level\tvalue\tpower\tvalue1\tvalue2\tcursor_graphic\n"
                + "1001\tEmber Blade\t7\t1\t3650\tTOHIT\t10\t10\t-\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeUniqueItemCatalog.ValidateExternalizedProduction(table));

        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UniqueItemsResolveStableUniqueSymbols()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("unique", "ember_blade", 1001),
            new StableContentId("item", "short_sword", 7),
        ]);
        var uniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            "unique_id\tunique_symbol\tname\tbase_item_id\tbase_item_symbol\tmin_level\tvalue\tpower\tvalue1\tvalue2\n"
                + "1001\tember_blade\tEmber Blade\t7\tshort_sword\t1\t3650\tTOHIT\t10\t10\n",
            stableIds: stableIds);

        var definition = Assert.Single(uniques.Definitions);
        Assert.Equal(1001U, definition.UniqueId);
        Assert.Equal("ember_blade", definition.StableSymbol);
    }

    [Fact]
    public void UniqueItemsRejectStableUniqueNumericMismatch()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("unique", "ember_blade", 1001),
        ]);

        Assert.Throws<InvalidDataException>(() => AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            "unique_id\tunique_symbol\tname\tbase_item_id\tmin_level\tvalue\tpower\tvalue1\tvalue2\n"
                + "1002\tember_blade\tEmber Blade\t7\t1\t3650\tTOHIT\t10\t10\n",
            stableIds: stableIds));
    }

    [Fact]
    public void UniqueItemsResolveStableBaseItemSymbols()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("item", "short_sword", 7),
        ]);
        var uniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            "unique_id\tname\tbase_item_id\tbase_item_symbol\tmin_level\tvalue\tpower\tvalue1\tvalue2\n"
                + "1001\tEmber Blade\t7\tshort_sword\t1\t3650\tTOHIT\t10\t10\n",
            stableIds: stableIds);

        Assert.Equal(7U, Assert.Single(uniques.Definitions).BaseItemId);
    }

    [Fact]
    public void UniqueItemsRejectStableBaseItemNumericMismatch()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("item", "short_sword", 7),
        ]);

        Assert.Throws<InvalidDataException>(() => AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            "unique_id\tname\tbase_item_id\tbase_item_symbol\tmin_level\tvalue\tpower\tvalue1\tvalue2\n"
                + "1001\tEmber Blade\t8\tshort_sword\t1\t3650\tTOHIT\t10\t10\n",
            stableIds: stableIds));
    }

    [Fact]
    public void RejectsUnknownNormalizedUniqueCursorSymbols()
    {
        var cursors = AuthoritativeItemCursorCatalog.LoadTsv(
            "item_cursor_graphics.tsv",
            "symbol\tnative_cursor_graphic\nSHORT_SWORD\t64\n");

        Assert.Throws<InvalidDataException>(() => AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            "unique_id\tname\tbase_item_id\tmin_level\tvalue\tpower\tvalue1\tvalue2\tcursor_graphic\n"
                + "1001\tEmber Blade\t7\t1\t3650\tTOHIT\t10\t10\tUNKNOWN\n",
            cursors));
    }

    [Fact]
    public void RejectsInvalidUniquePresentationNames()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(
                1001,
                new string('x', 64),
                7,
                1,
                100,
                []),
        ]));
    }

    [Fact]
    public void ProductionUniquePowerValidationRejectsUnsupportedSymbols()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Invalid", 7, 1, 1, [
                new AuthoritativeUniqueItemPower("NOT_A_POWER", 0, 0),
            ]),
        ]);

        var exception = Assert.Throws<InvalidDataException>(uniques.ValidatePowers);

        Assert.Contains("unsupported power 'NOT_A_POWER'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionAffixPowerValidationRejectsUnsupportedSymbols()
    {
        var affixes = new AuthoritativeItemAffixCatalog(
            [new AuthoritativeItemAffix("Invalid", "NOT_A_POWER", 0, 0, 0, new HashSet<string>(), 1, true)],
            []);

        var exception = Assert.Throws<InvalidDataException>(affixes.ValidatePowers);

        Assert.Contains("Affix 'Invalid' uses unsupported power 'NOT_A_POWER'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalHellfireItemPowersMapAllNativeDamageArmorFlags()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                7,
                1,
                75,
                75,
                4,
                8,
                0,
                20,
                20,
                1,
                1,
                true) {
                ItemClass = 1,
            },
        ]);
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(
                1001,
                "Hellfire Effects",
                7,
                1,
                3650,
                [
                    new AuthoritativeUniqueItemPower("DEVASTATION", 0, 0),
                    new AuthoritativeUniqueItemPower("DECAY", 5, 5),
                    new AuthoritativeUniqueItemPower("PERIL", 0, 0),
                    new AuthoritativeUniqueItemPower("JESTERS", 0, 0),
                    new AuthoritativeUniqueItemPower("DOPPELGANGER", 0, 0),
                    new AuthoritativeUniqueItemPower("ACDEMON", 0, 0),
                ]),
            new AuthoritativeUniqueItemDefinition(
                1002,
                "Undead Effects",
                7,
                1,
                3650,
                [new AuthoritativeUniqueItemPower("ACUNDEAD", 0, 0)]),
        ]);
        items.AttachUniqueCatalog(uniques);

        var item = items.GenerateUnique(1001, 42, 1);

        Assert.Equal(
            AuthoritativeEquipmentIncomingDamageRules.DevastationFlag
                | AuthoritativeEquipmentIncomingDamageRules.DecayFlag
                | AuthoritativeEquipmentIncomingDamageRules.PerilFlag
                | AuthoritativeEquipmentIncomingDamageRules.JestersFlag
                | AuthoritativeEquipmentIncomingDamageRules.DoppelgangerFlag
                | AuthoritativeEquipmentIncomingDamageRules.AgainstDemonsFlag,
            item.HellfireDamageArmorFlags);
        Assert.Equal(5, item.PlusDamage);
        Assert.Equal(
            AuthoritativeEquipmentIncomingDamageRules.AgainstUndeadFlag,
            items.GenerateUnique(1002, 43, 1).HellfireDamageArmorFlags);
    }

    [Fact]
    public void ExternalHellfireAffixPowersMapThroughValidatedAffixApplication()
    {
        var affixes = new AuthoritativeItemAffixCatalog(
            [new AuthoritativeItemAffix(
                "Devastating",
                "DEVASTATION",
                0,
                0,
                1,
                new HashSet<string>(),
                1,
                true)],
            [new AuthoritativeItemAffix(
                "Decaying",
                "DECAY",
                5,
                5,
                1,
                new HashSet<string>(),
                1,
                true)],
            new AuthoritativeItemGenerationRules(0, 0, 0, 0, 0, 100, 100, 100, 0, 0));
        var item = new AuthoritativeItemDefinition(
            7,
            1,
            75,
            75,
            4,
            8,
            0,
            20,
            20,
            1,
            1,
            true);
        var state = affixes.Apply(
            item.Generate(42),
            42,
            1,
            new HashSet<string>());

        Assert.Equal(
            AuthoritativeEquipmentIncomingDamageRules.DevastationFlag
                | AuthoritativeEquipmentIncomingDamageRules.DecayFlag,
            state.HellfireDamageArmorFlags);
        Assert.Equal(5, state.PlusDamage);
    }

    [Fact]
    public void AffixSpellPowerMarksTheGeneratedItemAsCharged()
    {
        var affixes = new AuthoritativeItemAffixCatalog(
            [new AuthoritativeItemAffix(
                "Charged",
                "SPELL",
                7,
                7,
                1,
                new HashSet<string>(),
                1,
                true)],
            [new AuthoritativeItemAffix(
                "Charged Suffix",
                "SPELL",
                7,
                7,
                1,
                new HashSet<string>(),
                1,
                true)],
            new AuthoritativeItemGenerationRules(0, 0, 0, 0, 0, 100, 100, 100, 0, 0));
        var definition = new AuthoritativeItemDefinition(
            7,
            10,
            75,
            75,
            0,
            0,
            0,
            20,
            20,
            1,
            1,
            true);

        var state = affixes.Apply(
            definition.Generate(42),
            42,
            1,
            new HashSet<string>());

        Assert.Equal(7, state.SpellId);
        Assert.Equal(7, state.MaxCharges);
        Assert.Equal(DiabloItemSpellSourceKind.ChargedItem, state.SpellSourceKind);
    }

    [Fact]
    public void LoadsLegacyItemAndUniqueTableShapesWithStableAliases()
    {
        const string itemData = "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
            + "IDI_CLEAVER\t0\tWeapon\tTwo-handed\tCLEAVER\tAxe\tCLEAVER\tCleaver\t\t10\t10\t4\t24\t0\t0\t0\t0\t0\t\tUNIQUE\tNull\tfalse\t2000\n";
        var items = AuthoritativeItemCatalog.LoadLegacyTsv("itemdat.tsv", itemData);

        Assert.Equal(1U, items.ResolveLegacySymbol("IDI_CLEAVER"));
        Assert.Equal(1U, items.ResolveLegacySymbol("CLEAVER"));
        var baseItem = items.Generate(1, 42);
        Assert.Equal(24, baseItem.MaxDamage);
        Assert.Equal(2, baseItem.ItemType);
        Assert.Equal(1, baseItem.ItemClass);
        Assert.Equal(2, baseItem.EquipLocation);
        Assert.Equal(28, baseItem.MiscId);
        Assert.Equal("Cleaver", baseItem.BaseName);
        Assert.Equal("Cleaver", baseItem.IdentifiedName);
        Assert.Equal(10, items.GetRequired(1).MinimumMonsterLevel);
        Assert.Equal(0, items.GetRequired(1).DropRate);
        Assert.False(items.GetRequired(1).Usable);

        var uniques = AuthoritativeUniqueItemCatalog.LoadLegacyTsv(
            "unique_itemdat.tsv",
            "name\tcursorGraphic\tuniqueBaseItem\tminLevel\tvalue\tpower0\tpower0.value1\tpower0.value2\tpower1\tpower1.value1\tpower1.value2\tpower2\tpower2.value1\tpower2.value2\tpower3\tpower3.value1\tpower3.value2\tpower4\tpower4.value1\tpower4.value2\tpower5\tpower5.value1\tpower5.value2\n"
                + "The Butcher's Cleaver\tCLEAVER\tCLEAVER\t1\t3650\tSTR\t10\t10\tSETDAM\t4\t24\t\t\t\t\t\t\t\t\t\t\t\t\n",
            items,
            AuthoritativeItemCursorCatalog.LoadTsv(
                "item_cursor_graphics.tsv",
                "symbol\tnative_cursor_graphic\nCLEAVER\t106\n"));
        items.AttachUniqueCatalog(uniques);

        var unique = items.GenerateUnique(1, 42, 1);
        Assert.Equal(1, unique.UniqueId);
        Assert.Equal("The Butcher's Cleaver", unique.BaseName);
        Assert.Equal("The Butcher's Cleaver", unique.IdentifiedName);
        Assert.Equal(10, unique.PlusStrength);
        Assert.Equal(4, unique.MinDamage);
        Assert.Equal(24, unique.MaxDamage);
        Assert.Equal(106, unique.NativeCursorGraphic);
    }

    [Fact]
    public void LegacySpellSourcesResolveThroughTheActiveStableCatalog()
    {
        const string itemData = "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
            + "IDI_GUARDIAN_SCROLL\t1\tMisc\tUnequippable\tSCROLL_OF\tMisc\tNONE\tScroll of Guardian\t\t1\t0\t0\t0\t0\t0\t0\t0\t0\t\tSCROLLT\tGuardian\ttrue\t950\n";
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 50, 0, 0, 0, 0) { NativeSpellId = 13 },
        ]);

        var items = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            itemData,
            spellCatalog: spells);

        Assert.Equal(7, Assert.Single(items.Definitions).SpellId);
    }

    [Fact]
    public void NormalizedItemSpellReferencesMustResolveInTheActiveCatalog()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 50, 0, 0, 0, 0),
        ]);
        var validItems = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                7,
                10,
                75,
                75,
                0,
                0,
                0,
                20,
                20,
                1,
                1,
                true)
            {
                SpellId = 7,
                SpellSourceKind = DiabloItemSpellSourceKind.ConsumableItem,
            },
        ]);

        validItems.ValidateSpellReferences(spells);

        var invalidItems = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                8,
                10,
                75,
                75,
                0,
                0,
                0,
                20,
                20,
                1,
                1,
                true)
            {
                SpellId = 8,
                SpellSourceKind = DiabloItemSpellSourceKind.ConsumableItem,
            },
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            invalidItems.ValidateSpellReferences(spells));
        Assert.Contains("Item 8 references unknown spell 8", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyRuneMiscellaneousIdsPreserveNativeSourceIdentity()
    {
        const string header = "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n";
        var itemData = header
            + "IDI_RUNE_FIRE\t1\tMisc\tUnequippable\tRUNE_OF_FIRE\tMisc\tNONE\tRune of Fire\tRune\t1\t0\t0\t0\t0\t0\t0\t0\t0\t\tRUNEF\tNull\ttrue\t100\n"
            + "IDI_RUNE_LIGHT\t1\tMisc\tUnequippable\tRUNE_OF_LIGHTNING\tMisc\tNONE\tRune of Lightning\tRune\t3\t0\t0\t0\t0\t0\t0\t13\t0\t\tRUNEL\tNull\ttrue\t200\n"
            + "IDI_GREATER_RUNE_FIRE\t1\tMisc\tUnequippable\tGREATER_RUNE_OF_FIRE\tMisc\tNONE\tGreater Rune of Fire\tRune\t7\t0\t0\t0\t0\t0\t0\t42\t0\t\tGR_RUNEF\tNull\ttrue\t400\n"
            + "IDI_GREATER_RUNE_LIGHT\t1\tMisc\tUnequippable\tGREATER_RUNE_OF_LIGHTNING\tMisc\tNONE\tGreater Rune of Lightning\tRune\t7\t0\t0\t0\t0\t0\t0\t42\t0\t\tGR_RUNEL\tNull\ttrue\t500\n"
            + "IDI_RUNE_STONE\t1\tMisc\tUnequippable\tRUNE_OF_STONE\tMisc\tNONE\tRune of Stone\tRune\t7\t0\t0\t0\t0\t0\t0\t25\t0\t\tRUNES\tNull\ttrue\t300\n";

        var items = AuthoritativeItemCatalog.LoadLegacyTsv("itemdat.tsv", itemData);

        Assert.Equal([47, 48, 50, 49, 51], items.Definitions.Select(item => item.MiscId));
    }

    [Fact]
    public void LegacyScrollMiscellaneousIdsPreserveNativeSourceIdentity()
    {
        const string header = "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n";
        var itemData = header
            + "IDI_PORTAL\t0\tMisc\tUnequippable\tSCROLL_OF\tMisc\tNONE\tScroll of Town Portal\t\t4\t0\t0\t0\t0\t0\t0\t0\t0\t\tSCROLL\tTownPortal\ttrue\t200\n"
            + "IDI_RESURRECT\t0\tMisc\tUnequippable\tSCROLL_OF\tMisc\tNONE\tScroll of Resurrect\t\t1\t0\t0\t0\t0\t0\t0\t0\t0\t\tSCROLLT\tResurrect\ttrue\t250\n";
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(39, 35, 0, 0, 0, 0) { NativeSpellId = 7 },
            new AuthoritativeSpellDefinition(32, 20, 0, 0, 0, 0) { NativeSpellId = 32 },
        ]);

        var items = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            itemData,
            spellCatalog: spells);

        Assert.Equal([21, 22], items.Definitions.Select(item => item.MiscId));
        Assert.Equal([39, 32], items.Definitions.Select(item => item.SpellId));
    }

    [Fact]
    public void UnmappedLegacySpellSourcesAreRejectedWhenAStableCatalogIsActive()
    {
        const string itemData = "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
            + "IDI_TOWN_PORTAL_SCROLL\t1\tMisc\tUnequippable\tSCROLL_OF\tMisc\tNONE\tScroll of Town Portal\t\t1\t0\t0\t0\t0\t0\t0\t0\t0\t\tSCROLLT\tTownPortal\ttrue\t950\n";
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 50, 0, 0, 0, 0) { NativeSpellId = 13 },
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeItemCatalog.LoadLegacyTsv(
                "itemdat.tsv",
                itemData,
                spellCatalog: spells));

        Assert.Contains("native spell ID 7", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyUniquePowerUsesTheNativeRandomStream()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 1, 1, 1, 1, true),
        ]);
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1, "Ember Blade", 7, 1, 3650, [
                new AuthoritativeUniqueItemPower("FIREDAM", 1, 10),
            ]),
        ], usesLegacyRandom: true);

        items.AttachUniqueCatalog(uniques);
        var unique = items.GenerateUnique(1, 42, 1);

        Assert.Equal(1, unique.FireMinDamage);
        Assert.Equal(10, unique.FireMaxDamage);
        Assert.Equal(0x00000010U, unique.Flags);
    }

    [Fact]
    public void StoreRowsMayReferenceUniqueDefinitions()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Ember Blade", 7, 1, 3650, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);
        var stores = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\tunique_item_id\titem_level\n1\t0\t42\t3650\t1001\t1\n",
            items,
            uniques);

        Assert.Equal(1001, Assert.Single(stores.GetItems(1)).State.UniqueId);
    }

    [Fact]
    public void StoreRowsResolveStableUniqueItemSymbols()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("unique", "ember_blade", 1001),
        ]);
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Ember Blade", 7, 1, 3650, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);
        var stores = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\tunique_item_id\tunique_item_symbol\titem_level\n"
                + "1\t0\t42\t3650\t\tember_blade\t1\n",
            items,
            uniques,
            stableIds);

        Assert.Equal(1001, Assert.Single(stores.GetItems(1)).State.UniqueId);
    }

    [Fact]
    public void UniqueItemBaseReferencesMustResolveInTheActiveCatalog()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ]);
        var validUniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Ember Blade", 7, 1, 3650, []),
        ]);

        validUniques.ValidateBaseItems(items);

        var invalidUniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1002, "Missing Base", 8, 1, 3650, []),
        ]);
        var exception = Assert.Throws<InvalidDataException>(() =>
            invalidUniques.ValidateBaseItems(items));
        Assert.Contains("Unique item 1002 references unknown base item 8", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AppliesLegacyUniqueFlagsAndSpellPower()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Ward", 7, 1, 3650, [
                new AuthoritativeUniqueItemPower("SPELL", 10, 76),
                new AuthoritativeUniqueItemPower("FASTATTACK", 2, 2),
                new AuthoritativeUniqueItemPower("ALLRESZERO", 0, 0),
                new AuthoritativeUniqueItemPower("NOMINSTR", 0, 0),
            ]),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);

        var item = items.GenerateUnique(1001, 42, 1);

        Assert.Equal(10, item.SpellId);
        Assert.Equal(76, item.Charges);
        Assert.Equal(76, item.MaxCharges);
        Assert.Equal(DiabloItemSpellSourceKind.ChargedItem, item.SpellSourceKind);
        Assert.Equal(0, item.MinimumStrength);
        Assert.Equal(0x80000000U | 0x00040000U, item.Flags);
    }

    [Fact]
    public void AppliesLegacyUniqueDurabilityCurseWithNativeMinimum()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Fragile Ward", 7, 1, 3650, [
                new AuthoritativeUniqueItemPower("DUR_CURSE", 75, 75),
            ]),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);

        var item = items.GenerateUnique(1001, 42, 1);

        Assert.Equal(5, item.MaxDurability);
        Assert.Equal(5, item.Durability);
    }

    [Fact]
    public void AppliesUniqueChargesMultiplierWithoutRevivingDepletedCharges()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Charged Ward", 7, 1, 3650, [
                new AuthoritativeUniqueItemPower("CHARGES", 2, 2),
            ]),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true) {
                Charges = 0,
                MaxCharges = 3,
            },
        ], uniqueItems: uniques);

        var item = items.GenerateUnique(1001, 42, 1);

        Assert.Equal(0, item.Charges);
        Assert.Equal(6, item.MaxCharges);
    }

    [Fact]
    public void AppliesCrystallineUniquePowerWithNativeDamageAndDurabilityRules()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Crystalline Edge", 7, 1, 3650, [
                new AuthoritativeUniqueItemPower("CRYSTALLINE", 40, 40),
            ]),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 50, 1, 1, true),
        ], uniqueItems: uniques);

        var item = items.GenerateUnique(1001, 42, 1);

        Assert.Equal(220, item.PlusDamage);
        Assert.Equal(30, item.MaxDurability);
        Assert.Equal(30, item.Durability);
    }

    [Fact]
    public void AppliesCrystallineAffixPowerWithNativeDamageAndDurabilityRules()
    {
        var affixes = new AuthoritativeItemAffixCatalog(
            [new AuthoritativeItemAffix(
                "Crystalline",
                "CRYSTALLINE",
                40,
                40,
                1,
                new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
                1,
                true)],
            [],
            new AuthoritativeItemGenerationRules(10, 1, 1, 15, 4, 100, 0, 100, 59, 74));
        var item = AuthoritativeItemState.Empty with {
            ItemType = 1,
            Durability = 20,
            MaxDurability = 50,
        };

        var result = affixes.Apply(item, 42, 10, new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase));

        Assert.Equal(220, result.PlusDamage);
        Assert.Equal(30, result.MaxDurability);
        Assert.Equal(30, result.Durability);
    }

    [Fact]
    public void AppliesJestersAffixPowerToHellfireDamageArmorFlags()
    {
        var affixes = new AuthoritativeItemAffixCatalog(
            [new AuthoritativeItemAffix(
                "Jester's",
                "JESTERS",
                1,
                1,
                7,
                new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
                1,
                true)],
            [],
            new AuthoritativeItemGenerationRules(10, 1, 1, 15, 4, 100, 0, 100, 59, 74));

        var result = affixes.Apply(
            AuthoritativeItemState.Empty,
            42,
            10,
            new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase));

        Assert.Equal(AuthoritativeEquipmentIncomingDamageRules.JestersFlag, result.HellfireDamageArmorFlags);
    }

    [Fact]
    public void AppliesDoppelgangersAffixPowerToHellfireDamageArmorFlags()
    {
        var affixes = new AuthoritativeItemAffixCatalog(
            [new AuthoritativeItemAffix(
                "Doppelganger's",
                "DOPPELGANGER",
                81,
                95,
                11,
                new HashSet<string>(["Weapon", "Staff"], StringComparer.OrdinalIgnoreCase),
                1,
                true)],
            [],
            new AuthoritativeItemGenerationRules(10, 1, 1, 15, 4, 100, 0, 100, 59, 74));

        var result = affixes.Apply(
            AuthoritativeItemState.Empty,
            42,
            12,
            new HashSet<string>(["Staff"], StringComparer.OrdinalIgnoreCase));

        Assert.Equal(AuthoritativeEquipmentIncomingDamageRules.DoppelgangerFlag, result.HellfireDamageArmorFlags);
    }

    [Fact]
    public void AppliesSpecialHellfireSuffixPowersToDamageArmorFlags()
    {
        var effects = new[] {
            (Power: "DEVASTATION", Flag: AuthoritativeEquipmentIncomingDamageRules.DevastationFlag),
            (Power: "DECAY", Flag: AuthoritativeEquipmentIncomingDamageRules.DecayFlag),
            (Power: "PERIL", Flag: AuthoritativeEquipmentIncomingDamageRules.PerilFlag),
        };
        foreach (var effect in effects) {
            var affixes = new AuthoritativeItemAffixCatalog(
                [],
                [new AuthoritativeItemAffix(
                    effect.Power,
                    effect.Power,
                    1,
                    1,
                    1,
                    new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
                    1,
                    true)],
                new AuthoritativeItemGenerationRules(10, 1, 1, 15, 4, 0, 100, 100, 59, 74));

            var result = affixes.Apply(
                AuthoritativeItemState.Empty,
                42,
                10,
                new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase));

            Assert.Equal(effect.Flag, result.HellfireDamageArmorFlags);
        }
    }

    [Fact]
    public void LegacyUniquePowersUseNativeFixedPointAndSignConventions()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Legacy", 7, 1, 1, [
                new AuthoritativeUniqueItemPower("LIFE", 2, 2),
                new AuthoritativeUniqueItemPower("GETHIT", 3, 3),
                new AuthoritativeUniqueItemPower("LIGHT", 4, 4),
                new AuthoritativeUniqueItemPower("INDESTRUCTIBLE", 0, 0),
            ]),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 1, 1, 0, 0, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);

        var item = items.GenerateUnique(1001, 42, 1);

        Assert.Equal(128, item.PlusHitPoints);
        Assert.Equal(-3, item.PlusGetHit);
        Assert.Equal(4, item.PlusLight);
        Assert.Equal(255, item.Durability);
        Assert.Equal(255, item.MaxDurability);
    }

    [Fact]
    public void HellfireResourceConversionsUseNativeBaseResourceTruncation()
    {
        var manaToLife = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                ManaToLifePercent = 50,
            });
        var lifeToMana = new EquippedStoreItem(
            5,
            43,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                LifeToManaPercent = 40,
            });

        var converted = AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
            80 << 6,
            124 << 6,
            [manaToLife, lifeToMana]);

        Assert.Equal((80 + 62 - 32) << 6, converted.LifeQ6);
        Assert.Equal((124 + 32 - 62) << 6, converted.ManaQ6);
    }

    [Fact]
    public void EquippedLifeAndManaBonusesRemainQ6ExactAlongsideConversions()
    {
        var item = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                PlusHitPoints = 7 << 6,
                PlusMana = 9 << 6,
                ManaToLifePercent = 50,
            });

        var converted = AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
            80 << 6,
            124 << 6,
            [item]);

        Assert.Equal((80 + 7 + 62) << 6, converted.LifeQ6);
        Assert.Equal((124 + 9 - 62) << 6, converted.ManaQ6);
    }

    [Fact]
    public void EquippedArmorUsesRequirementsAndNativePercentageTruncation()
    {
        var identifiedArmor = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                ArmorClass = 20,
                PlusArmorClass = 50,
            });
        var unidentifiedMagicalArmor = new EquippedStoreItem(
            5,
            43,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                Magical = 1,
                ArmorClass = 7,
                PlusArmorClass = 100,
            });

        var armor = AuthoritativeEquipmentStatRules.CalculateArmorClass(
            5,
            23,
            [identifiedArmor, unidentifiedMagicalArmor]);

        Assert.Equal(5 + (23 / 5) + 20 + 10 + 7, armor);
    }

    [Fact]
    public void EquippedArmorResolvesFixedArmorByMiscIdWhenStateOmitsArmor()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 6, 100, 100, 0, 0, 20, 20, 20, 1, 1, true) {
                NativeItemIndex = 101,
                MiscId = 121,
                GenerationTags = new HashSet<string>(["Armor"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var armorItem = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 121,
                StatFlag = true,
                PlusArmorClass = 50,
            });

        var armor = AuthoritativeEquipmentStatRules.CalculateArmorClass(
            5,
            23,
            [armorItem],
            items: items);

        Assert.Equal(5 + (23 / 5) + 20 + 10, armor);
    }

    [Fact]
    public void EquippedArmorRestoresRandomArmorByMiscIdAndSeed()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 6, 100, 100, 0, 0, 0, 20, 20, 1, 1, true) {
                NativeItemIndex = 101,
                MiscId = 121,
                MinArmorClass = 10,
                MaxArmorClass = 20,
            },
        ]);
        var armorItem = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 121,
                StatFlag = true,
            });

        var armor = AuthoritativeEquipmentStatRules.CalculateArmorClass(
            5,
            23,
            [armorItem],
            items: items);

        Assert.Equal(5 + (23 / 5) + 12, armor);
    }

    [Fact]
    public void IronSkinAddsNativeLevelScaledArmorAfterEquipment()
    {
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "server",
                "content",
                "base",
                "classes.tsv")));
        var barbarian = classes.Resolve("barbarian");
        var rogue = classes.Resolve("rogue");
        var armor = AuthoritativeEquipmentStatRules.CalculateArmorClass(
            5,
            23,
            [],
            barbarian,
            9);
        var nonIronSkinArmor = AuthoritativeEquipmentStatRules.CalculateArmorClass(
            5,
            23,
            [],
            rogue,
            9);
        var player = new AuthoritativePlayerState(
            gold: 0,
            experience: 0,
            life: 0,
            mana: 0,
            manaMaximum: 0,
            attributes: barbarian.CreateStartingAttributes(),
            equipment: [],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 0,
            characterLevel: 9,
            levelId: 0,
            armorClass: 5,
            characterClassId: barbarian.ClassId,
            playerClass: barbarian);

        player.RecalculateEquipmentArmor();

        Assert.Equal(5 + (23 / 5) + (9 / 4), armor);
        Assert.Equal(5 + (23 / 5), nonIronSkinArmor);
        Assert.Equal(5 + (barbarian.BaseDexterity / 5) + (9 / 4), player.ArmorClass);

        var tunedBarbarian = barbarian with { IronSkinLevelDivisor = 3 };
        var tunedArmor = AuthoritativeEquipmentStatRules.CalculateArmorClass(
            5,
            23,
            [],
            tunedBarbarian,
            9);
        Assert.Equal(5 + (23 / 5) + (9 / 3), tunedArmor);
    }

    [Fact]
    public void MonkChestArmorUsesNativeLightMediumAndUniqueArmorBonuses()
    {
        var monk = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "server",
                "content",
                "base",
                "classes.tsv"))).Resolve("monk") with {
                    Symbol = "custom",
                };

        var lightArmor = new EquippedStoreItem(
            3,
            44,
            AuthoritativeItemState.Empty with { StatFlag = true, ItemType = 6 });
        var mediumArmor = lightArmor with {
            ItemSeed = 45,
            State = lightArmor.State with { ItemType = 8 },
        };
        var uniqueMediumArmor = mediumArmor with {
            ItemSeed = 46,
            State = mediumArmor.State with { Magical = 2 },
        };
        var uniqueHeavyArmor = lightArmor with {
            ItemSeed = 47,
            State = lightArmor.State with { ItemType = 9, Magical = 2 },
        };

        Assert.Equal(
            5 + (23 / 5) + 10,
            AuthoritativeEquipmentStatRules.CalculateArmorClass(
                5,
                23,
                [lightArmor],
                monk,
                10));
        Assert.Equal(
            5 + (23 / 5) + 5,
            AuthoritativeEquipmentStatRules.CalculateArmorClass(
                5,
                23,
                [mediumArmor],
                monk,
                10));
        Assert.Equal(
            5 + (23 / 5) + 10,
            AuthoritativeEquipmentStatRules.CalculateArmorClass(
                5,
                23,
                [uniqueMediumArmor],
                monk,
                10));
        Assert.Equal(
            5 + (23 / 5) + 5,
            AuthoritativeEquipmentStatRules.CalculateArmorClass(
                5,
                23,
                [uniqueHeavyArmor],
                monk,
                10));
    }

    [Fact]
    public void MonkChestArmorUsesValidatedSubtypeBeforeNativeItemType()
    {
        var monk = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "server",
                "content",
                "base",
                "classes.tsv"))).Resolve("monk") with {
                    Symbol = "custom",
                };
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(1, 9, 1, 1, 0, 0, 0, 1, 1, 1, 1, true) {
                NativeItemIndex = 44,
                GenerationTags = new HashSet<string>(["Armor", "LightArmor"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var chest = new EquippedStoreItem(
            3,
            44,
            AuthoritativeItemState.Empty with {
                ItemIndex = 44,
                ItemType = 9,
                StatFlag = true,
            });

        Assert.Equal(
            5 + (23 / 5) + 10,
            AuthoritativeEquipmentStatRules.CalculateArmorClass(
                5,
                23,
                [chest],
                monk,
                10,
                items));
        var player = new DiabloPlayerTemplate(
            0,
            0,
            0,
            0,
            0,
            new PlayerAttributesState(
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(23, 23),
                new PlayerAttributeState(0, 0)),
            [chest],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            0,
            10,
            0,
            5,
            playerClass: monk,
            items: items).CreatePlayer();
        Assert.Equal(5 + (23 / 5) + 10, player.ArmorClass);
    }

    [Fact]
    public void EquippedAttributesUseRequirementsAndIdentificationRules()
    {
        var identifiedItem = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                PlusStrength = 4,
                PlusMagic = 5,
                PlusDexterity = 6,
                PlusVitality = 7,
            });
        var unidentifiedMagicalItem = new EquippedStoreItem(
            5,
            43,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                Magical = 1,
                PlusStrength = 100,
                PlusMagic = 100,
                PlusDexterity = 100,
                PlusVitality = 100,
            });
        var baseAttributes = new PlayerAttributesState(
            new PlayerAttributeState(10, 10),
            new PlayerAttributeState(20, 20),
            new PlayerAttributeState(30, 30),
            new PlayerAttributeState(40, 40));

        var attributes = AuthoritativeEquipmentAttributeRules.Apply(
            baseAttributes,
            [identifiedItem, unidentifiedMagicalItem]);

        Assert.Equal(14, attributes.Strength.Current);
        Assert.Equal(25, attributes.Magic.Current);
        Assert.Equal(36, attributes.Dexterity.Current);
        Assert.Equal(47, attributes.Vitality.Current);
    }

    [Fact]
    public void EquippedAttributesUseConfiguredMaximum()
    {
        var item = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                PlusStrength = 100,
                PlusMagic = 200,
                PlusDexterity = 300,
                PlusVitality = 400,
            });
        var attributes = AuthoritativeEquipmentAttributeRules.Apply(
            PlayerAttributesState.Zero,
            [item],
            maximumAttribute: 125);

        Assert.Equal((100, 125, 125, 125), (
            attributes.Strength.Current,
            attributes.Magic.Current,
            attributes.Dexterity.Current,
            attributes.Vitality.Current));
    }

    [Fact]
    public void HellfireResourceConversionsRejectOutOfRangePercentages()
    {
        var invalid = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                ManaToLifePercent = 101,
            });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
                80 << 6,
                124 << 6,
                [invalid]));
    }

    [Fact]
    public void LiveEquipmentResourceRecalculationPreservesNativeResourceDebt()
    {
        var player = new AuthoritativePlayerState(
            0,
            0,
            60 << 6,
            100 << 6,
            124 << 6,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            80 << 6,
            1,
            0,
            0,
            baseLifeMaximum: 80 << 6,
            baseManaMaximum: 124 << 6);
        player.Equipment.Add(new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                ManaToLifePercent = 50,
            }));

        player.RecalculateEquipmentResources();

        Assert.Equal(142 << 6, player.LifeMaximum);
        Assert.Equal(62 << 6, player.ManaMaximum);
        Assert.Equal(122 << 6, player.Life);
        Assert.Equal(38 << 6, player.Mana);

        player.Equipment.Clear();
        player.RecalculateEquipmentResources();

        Assert.Equal(80 << 6, player.LifeMaximum);
        Assert.Equal(124 << 6, player.ManaMaximum);
        Assert.Equal(60 << 6, player.Life);
        Assert.Equal(100 << 6, player.Mana);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] {
                configuredRoot!,
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the Godevilution repository root.");
    }
}
