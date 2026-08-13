using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Smoke-tests the checked-in base content through the module-owned bootstrap. */
public sealed class ShippedContentTests
{
    [Fact]
    public void BaseContentPackUsesTheExplicitVersionedManifest()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var pack = ContentPackLoader.LoadManifestDirectory(contentRoot);

        Assert.Equal("devilution.base", pack.Id);
        Assert.Equal("0.1.0", pack.Version);
        Assert.Equal(
            new ContentSchemaIdentity("devilution.diablo", "0.66.0"),
            pack.Schema);
        Assert.Empty(pack.Overlays);
    }

    [Fact]
    public void HellfireOverlaySelectsTheExternalNativeGameVariant()
    {
        var repositoryRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "base"));
        var hellfirePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(repositoryRoot, "server", "content", "hellfire"));
        var manifest = new ContentManifest(
            "hellfire",
            "1",
            [basePack, hellfirePack]);

        var composed = ComposedContent.Compose(manifest);
        var combat = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            composed.GetRequiredTable("combat.tsv").ToCanonicalTsv());

        Assert.Equal("devilution.hellfire", hellfirePack.Id);
        Assert.Equal(
            new ContentSchemaIdentity("devilution.diablo", "0.66.0"),
            hellfirePack.Schema);
        Assert.Equal(DiabloGameVariant.Hellfire, combat.GameVariant);
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(composed);
        Assert.Equal(
            [0U, 1U, 2U, 3U, 4U, 9U, 17U, 18U, 19U, 20U, 21U, 22U, 23U, 24U],
            activation.Simulation.WorldState.World!.Levels.Select(level => level.LevelId));
        Assert.True(activation.Simulation.WorldState.World!.IsWalkable(24, 39, 39));
        Assert.Equal(5, activation.LevelGeneration!.Definitions.Count);
        Assert.Equal(25, activation.LevelGeneration.GetRequired(24).TrapPercent);
        Assert.Equal(24, activation.QuestTypes!.Definitions.Count);
        Assert.Equal(13, activation.QuestPools!.Definitions.Count);
        var baseQuest = Assert.Single(activation.Simulation.WorldState.Quests.Values);
        Assert.Equal(
            activation.QuestPools.SelectUnavailableQuestIds(0).Contains(baseQuest.QuestId),
            baseQuest.Unavailable);
        Assert.Equal(1, activation.QuestTypes.Definitions[12].SetLevel);
        Assert.Equal("Archbishop Lazarus' Lair", activation.SetLevelNames!.GetDisplayName(5));
        var hellfireMonster = activation.Simulation.WorldState.CombatTargets[10];
        Assert.Equal(2U, hellfireMonster.MonsterId);
        Assert.True(hellfireMonster.IsUndead);
        Assert.Equal(11, hellfireMonster.MaxHitPoints);
        Assert.Equal(7, activation.Simulation.WorldState.CombatTargets.Count);
        Assert.Equal(25, activation.Simulation.WorldState.CombatTargets[11].MaxHitPoints);
        Assert.True(activation.Simulation.WorldState.CombatTargets[12].IsUndead);
        Assert.Equal(5, activation.Simulation.WorldState.CombatTargets[15].MaxDamageSpecial);
        var monsterTypes = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            composed.GetRequiredTable("monster_types.tsv").ToCanonicalTsv());
        Assert.Equal(13, monsterTypes.Definitions.Count);
        Assert.Equal("Zombie", monsterTypes.Definitions[1].Name);
        Assert.Equal(0U, monsterTypes.Definitions[1].NativeMonsterId);
        Assert.Equal("Carver", monsterTypes.Definitions[6].Name);
        Assert.Equal("Horror", monsterTypes.Definitions[^1].Name);
        Assert.True(monsterTypes.Definitions[^1].Undead);
        Assert.Equal(11U, monsterTypes.Definitions[^1].NativeMonsterId);
        var questTypes = AuthoritativeQuestTypeCatalog.LoadTsv(
            "quest_types.tsv",
            composed.GetRequiredTable("quest_types.tsv").ToCanonicalTsv());
        Assert.Equal(24, questTypes.Definitions.Count);
        Assert.Equal("The Magic Rock", questTypes.Definitions[0].Name);
        Assert.Equal("The Jersey's Jersey", questTypes.Definitions[^1].Name);
        Assert.Equal(23U, questTypes.Definitions[^1].NativeQuestId);
        var hellfireItems = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            composed.GetRequiredTable("items.tsv").ToCanonicalTsv());
        var hellfireUniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            composed.GetRequiredTable("unique_items.tsv").ToCanonicalTsv());
        hellfireItems.AttachUniqueCatalog(hellfireUniques);
        var crown = hellfireItems.GenerateUnique(1003, 0x12345678, 1);
        Assert.Equal(7, crown.ItemIndex);
        Assert.Equal(16650, crown.Value);
        Assert.Equal(15, crown.ArmorClass);
        Assert.Equal(0x00000002U, crown.Flags);
        var band = hellfireItems.GenerateUnique(1004, 0x12345678, 1);
        Assert.Equal(1004, band.UniqueId);
        Assert.Equal(8000, band.Value);
        Assert.Equal(2, band.PlusStrength);
        Assert.Equal(2, band.PlusMagic);
        Assert.Equal(2, band.PlusDexterity);
        Assert.Equal(2, band.PlusVitality);
        Assert.Equal(2, band.PlusLight);
        Assert.Equal(0x00200000U | 0x10000000U, band.Flags);
        var opticAmulet = hellfireItems.GenerateUnique(1005, 0x12345678, 1);
        Assert.Equal(1005, opticAmulet.UniqueId);
        Assert.Equal(9750, opticAmulet.Value);
        Assert.Equal(2, opticAmulet.PlusLight);
        Assert.Equal(20, opticAmulet.PlusLightningResistance);
        Assert.Equal(-1, opticAmulet.PlusGetHit);
        Assert.Equal(5, opticAmulet.PlusMagic);
        var ringOfTruth = hellfireItems.GenerateUnique(1006, 0x12345678, 1);
        Assert.Equal(1006, ringOfTruth.UniqueId);
        Assert.Equal(9100, ringOfTruth.Value);
        Assert.Equal(10 << 6, ringOfTruth.PlusHitPoints);
        Assert.Equal(-1, ringOfTruth.PlusGetHit);
        Assert.Equal(10, ringOfTruth.PlusMagicResistance);
        var harlequinCrest = hellfireItems.GenerateUnique(1007, 0x12345678, 1);
        Assert.Equal(1007, harlequinCrest.UniqueId);
        Assert.Equal(4000, harlequinCrest.Value);
        Assert.Equal(-3, harlequinCrest.PlusArmorClass);
        Assert.Equal(-1, harlequinCrest.PlusGetHit);
        Assert.Equal(2, harlequinCrest.PlusStrength);
        Assert.Equal(448, harlequinCrest.PlusHitPoints);
        Assert.Equal(448, harlequinCrest.PlusMana);
        var veilOfSteel = hellfireItems.GenerateUnique(1008, 0x12345678, 1);
        Assert.Equal(1008, veilOfSteel.UniqueId);
        Assert.Equal(63800, veilOfSteel.Value);
        Assert.Equal(50, veilOfSteel.PlusFireResistance);
        Assert.Equal(50, veilOfSteel.PlusLightningResistance);
        Assert.Equal(50, veilOfSteel.PlusMagicResistance);
        Assert.Equal(60, veilOfSteel.PlusArmorClass);
        Assert.Equal(-(30 << 6), veilOfSteel.PlusMana);
        Assert.Equal(15, veilOfSteel.PlusStrength);
        Assert.Equal(15, veilOfSteel.PlusVitality);
        var arkainesValor = hellfireItems.GenerateUnique(1009, 0x12345678, 1);
        Assert.Equal(1009, arkainesValor.UniqueId);
        Assert.Equal(42000, arkainesValor.Value);
        Assert.Equal(25, arkainesValor.ArmorClass);
        Assert.Equal(10, arkainesValor.PlusVitality);
        Assert.Equal(-3, arkainesValor.PlusGetHit);
        Assert.NotEqual(0U, arkainesValor.Flags & 0x00800000U);
        var hellfireSorcerer = activation.PlayerLoadouts.CreateTemplate(
            "hellfire-sorcerer",
            2,
            null,
            null,
            null,
            activation.DefaultStartingLevelId);
        var startingStaff = Assert.Single(
            hellfireSorcerer.Equipment,
            item => item.Slot == 4);
        Assert.Equal(1, startingStaff.State.SpellId);
        Assert.Equal(23, startingStaff.State.MiscId);
        Assert.Equal(166, startingStaff.State.ItemIndex);
        Assert.True(startingStaff.State.StatFlag);
        Assert.Equal(2, composed.Manifest.Packs.Count);
    }

    [Fact]
    public void BaseContentManifestAndAuthoritativeCatalogsLoadDeterministically()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var firstManifest = new ContentManifest("base", "1", [ContentPackLoader.LoadDirectory("base", "1", contentRoot)]);
        var secondManifest = new ContentManifest("base", "1", [ContentPackLoader.LoadDirectory("base", "1", contentRoot)]);

        Assert.Equal(firstManifest.Sha256, secondManifest.Sha256);
        Assert.Equal(64, firstManifest.Sha256.Length);
        Assert.True(firstManifest.Packs.Single().Tables.Count >= 16);
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "content_ids.tsv")));
        Assert.Equal(5U, stableIds.Resolve("spell", "identify"));
        Assert.Equal(26U, stableIds.Resolve("spell", "repair"));
        Assert.Equal(27U, stableIds.Resolve("spell", "recharge"));
        Assert.Equal(28U, stableIds.Resolve("spell", "trap_disarm"));
        Assert.Equal(30U, stableIds.Resolve("spell", "charged_bolt"));
        Assert.Equal(46U, stableIds.Resolve("spell", "search"));

        var module = new DiabloModule();
        var registry = new GameplayModuleRegistry();
        registry.Load(
            [module],
            new GameplayModuleContext(
                firstManifest,
                new DelegateAuthoritativeClock(() => 0)));
        var activated = registry.GetRequiredCapability<IDiabloContentBootstrap>()
            .Activate(ComposedContent.Compose(firstManifest));
        var runtime = activated.Simulation.CreateRuntime(
            activated.PlayerLoadouts.CreateTemplate(
                "player",
                activated.PlayerClasses.Default.ClassId,
                null,
                null,
                null,
                activated.DefaultStartingLevelId));
        var initial = runtime.Capture(
            "player",
            0,
            player => activated.Simulation.CaptureSnapshot(player, 0));
        var opened = runtime.Execute(
            "player",
            1,
            (player, _) => new SimulationStepResult<DiabloIntentResult, DiabloGameplayEvent>(
                activated.Simulation.Execute(player, new DiabloOpenStoreIntent(1), 1)));
        var snapshot = runtime.Capture(
            "player",
            1,
            player => activated.Simulation.CaptureSnapshot(player, 1));
        var moved = runtime.Execute(
            "player",
            2,
            (player, participants) =>
                new SimulationStepResult<DiabloWorldIntentResult, DiabloGameplayEvent>(
                    activated.Simulation.Execute(
                        player,
                        new DiabloMoveIntent(0, 1),
                        2,
                        participants)));
        var moving = runtime.Read(
            "player",
            player => activated.Simulation.CaptureSnapshot(player, 2));

        Assert.True(opened.Succeeded);
        Assert.True(moved.Succeeded);
        Assert.Equal((0, 0), (moving.Player.PositionX, moving.Player.PositionY));
        Assert.Equal(DiabloPlayerCombatMode.WalkSouthwards, moving.Player.CombatMode);
        Assert.Equal(10UL, moving.Player.CombatModeEndTick);
        var moveAction = Assert.IsType<AuthoritativePlayerActionState>(
            moving.Player.CurrentAction);
        Assert.Equal(DiabloPlayerActionKind.Move, moveAction.Kind);
        Assert.Equal((0, 1), (moveAction.DirectionX, moveAction.DirectionY));
        Assert.Equal(6, activated.PlayerClasses.Definitions.Count);
        Assert.Equal(6, activated.PlayerAnimations.Definitions.Count);
        Assert.Same(activated.PlayerAnimations, activated.Simulation.PlayerAnimations);
        Assert.Equal("warrior", activated.PlayerClasses.Default.Symbol);
        Assert.Equal(0U, snapshot.Player.CharacterClassId);
        Assert.Equal(30, snapshot.Player.Attributes.Strength.Base);
        Assert.Equal(10, snapshot.Player.Attributes.Magic.Base);
        Assert.Equal(20, snapshot.Player.Attributes.Dexterity.Base);
        Assert.Equal(25, snapshot.Player.Attributes.Vitality.Base);
        Assert.Equal(4480, initial.Player.Life);
        Assert.Equal(4480, initial.Player.LifeMaximum);
        Assert.Equal(640, initial.Player.Mana);
        Assert.Equal(640, initial.Player.ManaMaximum);
        Assert.Equal(100U, initial.Player.Gold);
        Assert.Equal(26U, initial.Player.AbilitySpellId);
        Assert.Empty(initial.Player.MemorizedSpells);
        Assert.Equal(2, initial.Player.Equipment.Count);
        Assert.Equal(2, initial.Player.Belt.Count);
        Assert.Equal(2, initial.Player.Inventory.Count);
        var startingGold = Assert.Single(
            initial.Player.Inventory,
            item => item.State.ItemType == activated.Simulation.CombatRules.DeathGoldItemType);
        Assert.Equal(100U, startingGold.Price);
        Assert.Equal(1, initial.Player.InventoryGrid[30]);
        Assert.Equal(4U, activated.StoreRules.GetSalePrice(new OwnedStoreItem(
            0,
            0,
            1,
            0,
            0,
            AuthoritativeItemState.Empty with { Value = 16 })));
        Assert.NotEmpty(snapshot.Monsters);
        var store = Assert.IsType<DiabloStoreState>(snapshot.ActiveStore);
        Assert.Equal(2, store.Items.Count);
        var firstStoreItem = store.Items[0];
        Assert.Equal(42U, firstStoreItem.ItemSeed);
        Assert.Equal(1, firstStoreItem.State.ItemIndex);
        Assert.Equal(4, firstStoreItem.State.MinDamage);
        Assert.Equal(8, firstStoreItem.State.MaxDamage);
        Assert.True(firstStoreItem.State.Identified);
        var secondStoreItem = store.Items[1];
        Assert.Equal(43U, secondStoreItem.ItemSeed);
        Assert.Equal(2, secondStoreItem.State.ItemIndex);
        Assert.Equal(5, secondStoreItem.State.ArmorClass);
        var legacyItems = AuthoritativeItemCatalog.LoadLegacyTsv("itemdat.tsv", File.ReadAllText(Path.Combine(contentRoot, "itemdat.tsv")));
        Assert.NotEqual(0U, legacyItems.ResolveLegacySymbol("IDI_WARRIOR"));
        Assert.Equal(6, legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_WARRIOR"), 0x12345678).MaxDamage);

        var invalidClass = snapshot with {
            Player = snapshot.Player with { CharacterClassId = 99 },
        };
        var livePlayer = runtime.Read("player", player => player);
        Assert.Throws<InvalidDataException>(() =>
            activated.Simulation.RestoreSnapshot(livePlayer, invalidClass));
        Assert.Equal(0U, livePlayer.CharacterClassId);
    }

    [Fact]
    public void BaseContentActivationRejectsAMissingDirectStartingSpell()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var basePack = ContentPackLoader.LoadDirectory("base", "1", contentRoot);
        var spells = basePack.Tables.Single(table => table.SourcePath == "spells.tsv");
        var withoutChargedBolt = TsvTable.Parse(
            spells.SourcePath,
            string.Join(
                '\n',
                spells.ToCanonicalTsv()
                    .Split('\n')
                    .Where(line => !line.StartsWith("30\t", StringComparison.Ordinal))));
        var incompletePack = new ContentPack(
            basePack.Id,
            basePack.Version,
            basePack.Tables
                .Select(table => table.SourcePath == spells.SourcePath
                    ? withoutChargedBolt
                    : table)
                .ToArray());
        var incompleteManifest = new ContentManifest("base", "1", [incompletePack]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloModule.Instance.ContentBootstrap.Activate(
                ComposedContent.Compose(incompleteManifest)));

        Assert.Contains("charged item 104 spell 30", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BaseContentGenerationAndServicePricingCoverTheShippedSources()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var rules = AuthoritativeItemGenerationRules.LoadTsv(
            "item_generation.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_generation.tsv")));
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_prefixes.tsv")),
            "item_suffixes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_suffixes.tsv")),
            rules);
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "items.tsv")),
            affixes);
        var uniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "unique_items.tsv")));
        items.AttachUniqueCatalog(uniques);

        var weapon = items.Generate(1, 42, 10);
        Assert.Equal(weapon, items.Generate(1, 42, 10));
        Assert.Equal(1, weapon.ItemIndex);
        Assert.Equal(4, weapon.MinDamage);
        Assert.Equal(8, weapon.MaxDamage);
        var unique = items.GenerateUnique(1001, 42, 1);
        Assert.Equal(1001, unique.UniqueId);
        Assert.Equal(3650, unique.Value);
        Assert.Equal(10, unique.PlusToHit);
        Assert.Equal(1, unique.FireMinDamage);
        Assert.Equal(10, unique.FireMaxDamage);

        var legacyItems = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "itemdat.tsv")));
        var warrior = legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_WARRIOR"), 0x12345678);
        var cleaver = legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_CLEAVER"), 0x12345678);
        Assert.Equal(6, warrior.MaxDamage);
        Assert.Equal(24, cleaver.MaxDamage);
        Assert.Equal(2, cleaver.ItemType);

        var pricing = StoreServicePricing.LoadTsv(
            "store_services.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "store_services.tsv")));
        Assert.Equal(4, pricing.SaleDivisor);
        Assert.Equal(2, pricing.NormalRepairDivisor);
        Assert.Equal(30, pricing.MagicalRepairPercent);
        Assert.Equal(100U, pricing.IdentificationPrice);
        Assert.Equal(64, pricing.ManaChunk);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
