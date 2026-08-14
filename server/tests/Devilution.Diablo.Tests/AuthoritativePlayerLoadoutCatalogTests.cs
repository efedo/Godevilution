using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativePlayerLoadoutCatalogTests
{
    [Fact]
    public void ShippedLoadoutsPreserveNativeClassCreationState()
    {
        var catalog = LoadShippedCatalog();

        Assert.Equal(6, catalog.Definitions.Count);
        Assert.All(catalog.Definitions, definition =>
            Assert.Equal(100U, definition.StartingGold));
        Assert.Equal(
            [26U, 28U, 27U, 46U, 5U, 22U],
            catalog.Definitions.Select(definition => definition.AbilitySpellId));
        Assert.Equal(
            [
                DiabloPlayerAbilityKind.Repair,
                DiabloPlayerAbilityKind.TrapDisarm,
                DiabloPlayerAbilityKind.Recharge,
                DiabloPlayerAbilityKind.Search,
                DiabloPlayerAbilityKind.Identify,
                DiabloPlayerAbilityKind.Rage,
            ],
            catalog.Definitions.Select(definition => definition.AbilityKind));

        var warrior = catalog.CreateTemplate("warrior-session", 0, null, null, null, 1)
            .CreatePlayer();
        Assert.Equal(100U, warrior.Gold);
        Assert.Equal(26U, warrior.AbilitySpellId);
        Assert.Equal(DiabloPlayerAbilityKind.Repair, warrior.AbilityKind);
        Assert.Empty(warrior.MemorizedSpells);
        Assert.Equal([4U, 5U], warrior.Equipment.Select(item => item.Slot));
        Assert.Equal([0U, 1U], warrior.Belt.Select(item => item.Slot));
        var club = Assert.Single(warrior.Inventory, item => item.State.ItemType != 11);
        Assert.Equal(3, club.State.ItemIndex);
        Assert.Equal(1, club.State.InventoryWidth);
        Assert.Equal(3, club.State.InventoryHeight);
        Assert.True(club.State.StatFlag);
        Assert.Equal(0, warrior.InventoryGrid[0]);
        Assert.Equal(0, warrior.InventoryGrid[10]);
        Assert.Equal(0, warrior.InventoryGrid[20]);
        var warriorGold = Assert.Single(warrior.Inventory, item => item.State.ItemType == 11);
        Assert.Equal(100U, warriorGold.Price);
        Assert.Equal(100, warriorGold.State.Value);
        Assert.Equal(0, warriorGold.State.ItemIndex);
        Assert.True(warriorGold.State.StatFlag);
        Assert.NotEqual(0U, warriorGold.ItemSeed);
        Assert.Equal(1, warrior.InventoryGrid[30]);

        var sorcerer = catalog.CreateTemplate("sorcerer-session", 2, null, null, null, 1)
            .CreatePlayer();
        Assert.Equal(27U, sorcerer.AbilitySpellId);
        Assert.Equal(DiabloPlayerAbilityKind.Recharge, sorcerer.AbilityKind);
        Assert.Equal(new MemorizedSpellState(1, 2), Assert.Single(sorcerer.MemorizedSpells));
        var staff = Assert.Single(sorcerer.Equipment);
        Assert.Equal(166, staff.State.ItemIndex);
        Assert.Equal(30, staff.State.SpellId);
        Assert.Equal(40, staff.State.Charges);
        Assert.Equal(40, staff.State.MaxCharges);
        Assert.True(staff.State.StatFlag);
        Assert.All(sorcerer.Belt, item => Assert.Equal(6, item.State.MiscId));

        var bard = catalog.CreateTemplate("bard-session", 4, null, null, null, 1)
            .CreatePlayer();
        Assert.Equal([37, 38], bard.Equipment.Select(item => item.State.ItemIndex));
        var barbarian = catalog.CreateTemplate("barbarian-session", 5, null, null, null, 1)
            .CreatePlayer();
        Assert.Equal([139, 2], barbarian.Equipment.Select(item => item.State.ItemIndex));

        foreach (var definition in catalog.Definitions) {
            var player = catalog.CreateTemplate(
                    $"resource-{definition.ClassId}",
                    definition.ClassId,
                    null,
                    null,
                    null,
                    1)
                .CreatePlayer();
            var expected = AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
                player.BaseLifeMaximum,
                player.BaseManaMaximum,
                player.Equipment,
                player.PlayerClass);

            Assert.NotNull(player.PlayerClass);
            Assert.Equal(expected.LifeQ6, player.LifeMaximum);
            Assert.Equal(expected.ManaQ6, player.ManaMaximum);
        }
    }

    [Fact]
    public void SessionOwnedSeedsAreStableUniqueAndSessionSpecific()
    {
        var catalog = LoadShippedCatalog();

        var first = catalog.CreateTemplate("same-session", 0, null, null, null, 1)
            .CreatePlayer();
        var repeated = catalog.CreateTemplate("same-session", 0, null, null, null, 1)
            .CreatePlayer();
        var other = catalog.CreateTemplate("other-session", 0, null, null, null, 1)
            .CreatePlayer();
        var firstSeeds = GetSeeds(first);

        Assert.Equal(firstSeeds, GetSeeds(repeated));
        Assert.False(firstSeeds.SequenceEqual(GetSeeds(other)));
        Assert.DoesNotContain(0U, firstSeeds);
        Assert.Equal(firstSeeds.Length, firstSeeds.Distinct().Count());
    }

    [Fact]
    public void ExplicitHostOverridesDoNotReplaceContentLoadoutItemsOrSpells()
    {
        var catalog = LoadShippedCatalog();

        var player = catalog.CreateTemplate("override", 2, 900, 77, 66, 1)
            .CreatePlayer();

        Assert.Equal(900U, player.Gold);
        Assert.Equal(77, player.Life);
        Assert.Equal(66, player.Mana);
        Assert.NotEmpty(player.Equipment);
        Assert.NotEmpty(player.Belt);
        Assert.Equal(27U, player.AbilitySpellId);
        Assert.Equal(1U, Assert.Single(player.MemorizedSpells).SpellId);
        var gold = Assert.Single(player.Inventory);
        Assert.Equal(900U, gold.Price);
        Assert.Equal(0, player.InventoryGrid[30]);
    }

    [Fact]
    public void SnapshotRestoreRebindsClassScaledEquipmentResources()
    {
        var root = FindRepositoryRoot();
        var classes = LoadClasses(root);
        var catalog = LoadShippedCatalog();
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                PlayerClasses = classes,
            });
        var source = catalog.CreateTemplate("source", 4, null, null, null, 1).CreatePlayer();
        var destination = catalog.CreateTemplate("destination", 4, null, null, null, 1).CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(source, 1);

        simulation.RestoreSnapshot(destination, snapshot);

        Assert.Equal(4U, destination.CharacterClassId);
        Assert.Equal(4U, destination.PlayerClass?.ClassId);
        Assert.Equal(source.Attributes, destination.Attributes);
        Assert.Equal(source.LifeMaximum, destination.LifeMaximum);
        Assert.Equal(source.ManaMaximum, destination.ManaMaximum);
        Assert.Equal(source.Life, destination.Life);
        Assert.Equal(source.Mana, destination.Mana);
    }

    [Fact]
    public void ClassScaledEquipmentResourcesRecalculateAcrossTransfers()
    {
        var catalog = LoadShippedCatalog();
        var player = catalog.CreateTemplate("transfer", 4, null, null, null, 1).CreatePlayer();
        var baselineLife = player.LifeMaximum;
        var baselineMana = player.ManaMaximum;
        player.Inventory.Add(new OwnedStoreItem(
            0,
            0,
            900,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                PlusMagic = 5,
                PlusVitality = 7,
            }));
        player.InventoryGrid[0] = player.Inventory.Count - 1;

        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());
        var equip = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, checked((uint)(player.Inventory.Count - 1))),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 0)),
            1);

        Assert.True(equip.Succeeded);
        Assert.Equal(
            baselineLife + ((7 * player.PlayerClass!.ItemLifePerVitalityQ6 >> 6) << 6),
            player.LifeMaximum);
        Assert.Equal(
            baselineMana + ((5 * player.PlayerClass.ItemManaPerMagicQ6 >> 6) << 6),
            player.ManaMaximum);

        var unequip = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            2);

        Assert.True(unequip.Succeeded);
        Assert.Equal(baselineLife, player.LifeMaximum);
        Assert.Equal(baselineMana, player.ManaMaximum);
    }

    [Fact]
    public void ExplicitLargeGoldOverrideUsesNativeStartingAndOverflowCells()
    {
        var player = LoadShippedCatalog()
            .CreateTemplate("large-gold", 2, 6000, null, null, 1)
            .CreatePlayer();

        Assert.Equal(6000U, player.Gold);
        Assert.Equal([5000U, 1000U], player.Inventory.Select(item => item.Price));
        Assert.Equal(0, player.InventoryGrid[30]);
        Assert.Equal(1, player.InventoryGrid[39]);
        Assert.Equal(2, player.Inventory.Select(item => item.ItemSeed).Distinct().Count());
    }

    [Fact]
    public void DirectStartingCastSourcesMustResolveInTheActivatedSpellCatalog()
    {
        var catalog = LoadShippedCatalog();
        var onlyFirebolt = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 6, 0, 0, 0, 0) {
                DamageAmount = 6,
            },
        ]);
        var complete = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            ReadBase(FindRepositoryRoot(), "spells.tsv"));

        var missing = Assert.Throws<InvalidDataException>(() =>
            catalog.ValidateCastSources(onlyFirebolt));

        Assert.Contains("charged item 104 spell 30", missing.Message, StringComparison.Ordinal);
        catalog.ValidateCastSources(complete);
    }

    [Fact]
    public void CatalogRejectsMissingClassesSchemaDriftAndInvalidTopology()
    {
        var root = FindRepositoryRoot();
        var classes = LoadClasses(root);
        var items = LoadItems(root);
        var combatRules = LoadCombatRules(root);
        var loadouts = ReadBase(root, "class_loadouts.tsv");
        var loadoutItems = ReadBase(root, "class_loadout_items.tsv");
        var missingClass = string.Join(
            "\r\n",
            loadouts.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("5\t", StringComparison.Ordinal))) + "\r\n";
        var schemaDrift = loadouts.Replace(
            "starting_gold",
            "gold",
            StringComparison.Ordinal);
        var overlapping = loadoutItems.Replace(
            "0\t4\t105\tBelt\t1",
            "0\t4\t105\tBelt\t0",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => AuthoritativePlayerLoadoutCatalog.LoadTsv(
            classes, items, combatRules, "class_loadouts.tsv", missingClass,
            "class_loadout_items.tsv", loadoutItems));
        Assert.Throws<InvalidDataException>(() => AuthoritativePlayerLoadoutCatalog.LoadTsv(
            classes, items, combatRules, "class_loadouts.tsv", schemaDrift,
            "class_loadout_items.tsv", loadoutItems));
        Assert.Throws<InvalidDataException>(() => AuthoritativePlayerLoadoutCatalog.LoadTsv(
            classes, items, combatRules, "class_loadouts.tsv", loadouts,
            "class_loadout_items.tsv", overlapping));
    }

    [Fact]
    public void ShieldTagAllowsOffHandLoadoutDespiteConflictingLegacyItemType()
    {
        var root = FindRepositoryRoot();
        var classes = LoadClasses(root);
        var source = ReadBase(root, "items.tsv");
        var conflictingItems = string.Join(
            "\r\n",
            source.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => {
                    if (!line.StartsWith("101\t", StringComparison.Ordinal))
                        return line;
                    var fields = line.Split('\t');
                    fields[1] = "1";
                    fields[14] = "1";
                    return string.Join('\t', fields);
                })) + "\r\n";
        var items = AuthoritativeItemCatalog.LoadTsv("items.tsv", conflictingItems);
        var catalog = AuthoritativePlayerLoadoutCatalog.LoadTsv(
            classes,
            items,
            LoadCombatRules(root),
            "class_loadouts.tsv",
            ReadBase(root, "class_loadouts.tsv"),
            "class_loadout_items.tsv",
            ReadBase(root, "class_loadout_items.tsv"));

        var barbarian = catalog.CreateTemplate(
                "tag-shield",
                5,
                null,
                null,
                null,
                1)
            .CreatePlayer();

        Assert.Equal(2, Assert.Single(barbarian.Equipment, item => item.Slot == 5).State.ItemIndex);
    }

    [Fact]
    public void BeltTagAllowsBeltLoadoutDespiteConflictingLegacyPlacementMetadata()
    {
        var root = FindRepositoryRoot();
        var classes = LoadClasses(root);
        var source = ReadBase(root, "items.tsv");
        var conflictingItems = string.Join(
            "\r\n",
            source.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => {
                    if (!line.StartsWith("105\t", StringComparison.Ordinal))
                        return line;
                    var fields = line.Split('\t');
                    fields[12] = "Misc,Belt";
                    fields[13] = "0";
                    fields[14] = "0";
                    return string.Join('\t', fields);
                })) + "\r\n";
        var items = AuthoritativeItemCatalog.LoadTsv("items.tsv", conflictingItems);
        var catalog = AuthoritativePlayerLoadoutCatalog.LoadTsv(
            classes,
            items,
            LoadCombatRules(root),
            "class_loadouts.tsv",
            ReadBase(root, "class_loadouts.tsv"),
            "class_loadout_items.tsv",
            ReadBase(root, "class_loadout_items.tsv"));

        var warrior = catalog.CreateTemplate(
                "tag-belt",
                0,
                null,
                null,
                null,
                1)
            .CreatePlayer();

        Assert.Equal([24, 24], warrior.Belt.Select(item => item.State.ItemIndex));
    }

    [Fact]
    public void TwoHandedTagRejectsOffHandLoadoutDespiteConflictingLegacyEquipmentLocation()
    {
        var root = FindRepositoryRoot();
        var classes = LoadClasses(root);
        var source = ReadBase(root, "items.tsv");
        var conflictingItems = string.Join(
            "\r\n",
            source.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => {
                    if (!line.StartsWith("103\t", StringComparison.Ordinal))
                        return line;
                    var fields = line.Split('\t');
                    fields[12] = "Bow,TwoHanded";
                    fields[13] = "1";
                    return string.Join('\t', fields);
                })) + "\r\n";
        var items = AuthoritativeItemCatalog.LoadTsv("items.tsv", conflictingItems);
        var loadoutItems = ReadBase(root, "class_loadout_items.tsv") +
            "1\t1\t101\tEquipment\t5\r\n";

        Assert.Throws<InvalidDataException>(() => AuthoritativePlayerLoadoutCatalog.LoadTsv(
            classes,
            items,
            LoadCombatRules(root),
            "class_loadouts.tsv",
            ReadBase(root, "class_loadouts.tsv"),
            "class_loadout_items.tsv",
            loadoutItems));
    }

    [Fact]
    public void BodyArmorTagAdmitsChestSlotDespiteConflictingLegacyEquipmentLocation()
    {
        var root = FindRepositoryRoot();
        var classes = LoadClasses(root);
        var source = ReadBase(root, "items.tsv");
        var customItems = string.Join(
            "\r\n",
            source.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => {
                    if (!line.StartsWith("107\t", StringComparison.Ordinal))
                        return line;
                    var fields = line.Split('\t');
                    fields[0] = "999";
                    fields[1] = "7";
                    fields[12] = "Armor,BodyArmor";
                    fields[13] = "1";
                    fields[14] = "2";
                    fields[21] = "199";
                    return string.Join('\t', fields);
                })) + "\r\n";
        var items = AuthoritativeItemCatalog.LoadTsv("items.tsv", customItems);
        var loadoutItems = ReadBase(root, "class_loadout_items.tsv").Replace(
            "3\t0\t107\tEquipment\t4",
            "3\t0\t999\tEquipment\t6",
            StringComparison.Ordinal);
        var catalog = AuthoritativePlayerLoadoutCatalog.LoadTsv(
            classes,
            items,
            LoadCombatRules(root),
            "class_loadouts.tsv",
            ReadBase(root, "class_loadouts.tsv"),
            "class_loadout_items.tsv",
            loadoutItems);

        var monk = catalog.CreateTemplate(
                "tag-body-armor",
                3,
                null,
                null,
                null,
                1)
            .CreatePlayer();

        Assert.Equal(199, Assert.Single(monk.Equipment, item => item.Slot == 6).State.ItemIndex);
    }

    [Fact]
    public void LegacyLoadoutSchemaDefaultsSemanticAbilityKind()
    {
        var root = FindRepositoryRoot();
        var classes = LoadClasses(root);
        var items = LoadItems(root);
        var combatRules = LoadCombatRules(root);
        var current = ReadBase(root, "class_loadouts.tsv")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => string.Join(
                '\t',
                line.Split('\t').Where((_, index) => index != 3)))
            .ToArray();
        var legacy = string.Join("\r\n", current) + "\r\n";

        var catalog = AuthoritativePlayerLoadoutCatalog.LoadTsv(
            classes,
            items,
            combatRules,
            "class_loadouts.tsv",
            legacy,
            "class_loadout_items.tsv",
            ReadBase(root, "class_loadout_items.tsv"));

        Assert.All(
            catalog.Definitions,
            definition => Assert.Equal(DiabloPlayerAbilityKind.None, definition.AbilityKind));
    }

    private static uint[] GetSeeds(AuthoritativePlayerState player)
    {
        return player.Equipment.Select(item => item.ItemSeed)
            .Concat(player.Inventory.Select(item => item.ItemSeed))
            .Concat(player.Belt.Select(item => item.ItemSeed))
            .Order()
            .ToArray();
    }

    private static AuthoritativePlayerLoadoutCatalog LoadShippedCatalog()
    {
        var root = FindRepositoryRoot();
        return AuthoritativePlayerLoadoutCatalog.LoadTsv(
            LoadClasses(root),
            LoadItems(root),
            LoadCombatRules(root),
            "class_loadouts.tsv",
            ReadBase(root, "class_loadouts.tsv"),
            "class_loadout_items.tsv",
            ReadBase(root, "class_loadout_items.tsv"));
    }

    private static AuthoritativePlayerClassCatalog LoadClasses(string root)
    {
        return AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            ReadBase(root, "classes.tsv"));
    }

    private static AuthoritativeItemCatalog LoadItems(string root)
    {
        return AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            ReadBase(root, "items.tsv"));
    }

    private static AuthoritativeCombatRules LoadCombatRules(string root)
    {
        return AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            ReadBase(root, "combat.tsv"));
    }

    private static string ReadBase(string root, string fileName)
    {
        return File.ReadAllText(Path.Combine(root, "server", "content", "base", fileName));
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
