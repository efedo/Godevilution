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
            new ContentSchemaIdentity("devilution.diablo", "0.99.0"),
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
            new ContentSchemaIdentity("devilution.diablo", "0.99.0"),
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
        Assert.Equal(100, hellfireMonster.MagicResistance);
        Assert.Equal(11, hellfireMonster.MaxHitPoints);
        Assert.Equal(7, activation.Simulation.WorldState.CombatTargets.Count);
        Assert.Equal(25, activation.Simulation.WorldState.CombatTargets[11].MaxHitPoints);
        Assert.True(activation.Simulation.WorldState.CombatTargets[12].IsUndead);
        Assert.Equal(5, activation.Simulation.WorldState.CombatTargets[15].MaxDamageSpecial);
        var monsterTypes = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            composed.GetRequiredTable("monster_types.tsv").ToCanonicalTsv());
        Assert.Equal(139, monsterTypes.Definitions.Count);
        Assert.Equal("Zombie", monsterTypes.Definitions[1].Name);
        Assert.Equal(0U, monsterTypes.Definitions[1].NativeMonsterId);
        Assert.Equal(100, monsterTypes.Definitions[1].MagicResistance);
        Assert.Equal("Carver", monsterTypes.Definitions[6].Name);
        Assert.Equal("Horror", monsterTypes.Definitions[12].Name);
        Assert.True(monsterTypes.Definitions[12].Undead);
        Assert.Equal(11U, monsterTypes.Definitions[12].NativeMonsterId);
        Assert.Equal("Bone Gasher", monsterTypes.Definitions[20].Name);
        Assert.Equal(19U, monsterTypes.Definitions[20].NativeMonsterId);
        Assert.Equal(75, monsterTypes.Definitions[20].LightningResistance);
        Assert.Equal("Horror", monsterTypes.Definitions[24].Name);
        Assert.Equal(23U, monsterTypes.Definitions[24].NativeMonsterId);
        Assert.Equal("Invisible Lord", monsterTypes.Definitions[29].Name);
        Assert.Equal(28U, monsterTypes.Definitions[29].NativeMonsterId);
        Assert.Equal((75, 75, 75), (
            monsterTypes.Definitions[29].FireResistance,
            monsterTypes.Definitions[29].LightningResistance,
            monsterTypes.Definitions[29].MagicResistance));
        Assert.Equal("Stone Clan", monsterTypes.Definitions[36].Name);
        Assert.Equal(35U, monsterTypes.Definitions[36].NativeMonsterId);
        Assert.Equal(100, monsterTypes.Definitions[36].MagicResistance);
        Assert.Equal("Night Clan", monsterTypes.Definitions[38].Name);
        Assert.Equal((30, 30), (
            monsterTypes.Definitions[38].MinimumSpecialDamage,
            monsterTypes.Definitions[38].MaximumSpecialDamage));
        Assert.Equal(100, monsterTypes.Definitions[38].MagicResistance);
        Assert.Equal((100, 75), (
            monsterTypes.Definitions[42].LightningResistance,
            monsterTypes.Definitions[42].MagicResistance));
        Assert.Equal("Poison Spitter", monsterTypes.Definitions[48].Name);
        Assert.Equal(47U, monsterTypes.Definitions[48].NativeMonsterId);
        Assert.Equal(100, monsterTypes.Definitions[48].MagicResistance);
        Assert.Equal(100, monsterTypes.Definitions[48].AcidResistance);
        Assert.Equal("Pit Beast", monsterTypes.Definitions[49].Name);
        Assert.Equal((75, 100), (
            monsterTypes.Definitions[49].LightningResistance,
            monsterTypes.Definitions[49].MagicResistance));
        Assert.Equal("Skeleton King", monsterTypes.Definitions[51].Name);
        Assert.True(monsterTypes.Definitions[51].Undead);
        Assert.Equal((100, 100, 100), (
            monsterTypes.Definitions[51].FireResistance,
            monsterTypes.Definitions[51].LightningResistance,
            monsterTypes.Definitions[51].MagicResistance));
        Assert.Equal((8, 20), (
            monsterTypes.Definitions[55].MinimumSpecialDamage,
            monsterTypes.Definitions[55].MaximumSpecialDamage));
        Assert.Equal("Devourer", monsterTypes.Definitions[60].Name);
        Assert.Equal(59U, monsterTypes.Definitions[60].NativeMonsterId);
        Assert.Equal((100, 100), (
            monsterTypes.Definitions[61].FireResistance,
            monsterTypes.Definitions[61].MagicResistance));
        Assert.Equal((5, 32), (
            monsterTypes.Definitions[65].MinimumSpecialDamage,
            monsterTypes.Definitions[65].MaximumSpecialDamage));
        Assert.Equal("Undead Balrog", monsterTypes.Definitions[72].Name);
        Assert.Equal(71U, monsterTypes.Definitions[72].NativeMonsterId);
        Assert.False(monsterTypes.Definitions[72].Undead);
        Assert.Equal((75, 75, 100), (
            monsterTypes.Definitions[72].FireResistance,
            monsterTypes.Definitions[72].LightningResistance,
            monsterTypes.Definitions[72].MagicResistance));
        Assert.Equal((4, 16), (
            monsterTypes.Definitions[77].MinimumSpecialDamage,
            monsterTypes.Definitions[77].MaximumSpecialDamage));
        Assert.Equal((75, 75, 75), (
            monsterTypes.Definitions[81].FireResistance,
            monsterTypes.Definitions[81].LightningResistance,
            monsterTypes.Definitions[81].MagicResistance));
        Assert.Equal("Blood Claw", monsterTypes.Definitions[84].Name);
        Assert.Equal(83U, monsterTypes.Definitions[84].NativeMonsterId);
        Assert.Equal((100, 75, 100), (
            monsterTypes.Definitions[84].FireResistance,
            monsterTypes.Definitions[84].LightningResistance,
            monsterTypes.Definitions[84].MagicResistance));
        Assert.Equal("Death Wing", monsterTypes.Definitions[85].Name);
        Assert.Equal((75, 100, 100), (
            monsterTypes.Definitions[85].FireResistance,
            monsterTypes.Definitions[85].LightningResistance,
            monsterTypes.Definitions[85].MagicResistance));
        Assert.Equal("Balrog", monsterTypes.Definitions[89].Name);
        Assert.Equal(88U, monsterTypes.Definitions[89].NativeMonsterId);
        Assert.Equal("Steel Lord", monsterTypes.Definitions[96].Name);
        Assert.Equal(95U, monsterTypes.Definitions[96].NativeMonsterId);
        Assert.Equal("Blood Knight", monsterTypes.Definitions[97].Name);
        Assert.Equal((75, 100, 100), (
            monsterTypes.Definitions[97].FireResistance,
            monsterTypes.Definitions[97].LightningResistance,
            monsterTypes.Definitions[97].MagicResistance));
        Assert.True(monsterTypes.Definitions[98].Undead);
        Assert.Equal("Reality Weaver", monsterTypes.Definitions[101].Name);
        Assert.True(monsterTypes.Definitions[101].Undead);
        Assert.Equal((100, 100, 75), (
            monsterTypes.Definitions[101].FireResistance,
            monsterTypes.Definitions[101].LightningResistance,
            monsterTypes.Definitions[101].MagicResistance));
        Assert.Equal("Cabalist", monsterTypes.Definitions[108].Name);
        Assert.Equal(107U, monsterTypes.Definitions[108].NativeMonsterId);
        Assert.Equal("Advocate", monsterTypes.Definitions[109].Name);
        Assert.Equal((100, 100, 100), (
            monsterTypes.Definitions[109].FireResistance,
            monsterTypes.Definitions[109].LightningResistance,
            monsterTypes.Definitions[109].MagicResistance));
        Assert.Equal("The Dark Lord", monsterTypes.Definitions[111].Name);
        Assert.Equal(110U, monsterTypes.Definitions[111].NativeMonsterId);
        Assert.Equal(1666, monsterTypes.Definitions[111].MaximumHitPoints);
        Assert.Equal("Necromorb", monsterTypes.Definitions[120].Name);
        Assert.Equal(119U, monsterTypes.Definitions[120].NativeMonsterId);
        Assert.Equal((100, 75), (
            monsterTypes.Definitions[120].FireResistance,
            monsterTypes.Definitions[120].LightningResistance));
        Assert.Equal((10, 10), (
            monsterTypes.Definitions[121].MinimumSpecialDamage,
            monsterTypes.Definitions[121].MaximumSpecialDamage));
        Assert.Equal((50, 60), (
            monsterTypes.Definitions[125].MinimumSpecialDamage,
            monsterTypes.Definitions[125].MaximumSpecialDamage));
        Assert.True(monsterTypes.Definitions[129].Undead);
        Assert.Equal("Hellbat", monsterTypes.Definitions[132].Name);
        Assert.Equal(131U, monsterTypes.Definitions[132].NativeMonsterId);
        Assert.Equal("Bone Demon", monsterTypes.Definitions[133].Name);
        Assert.True(monsterTypes.Definitions[133].Undead);
        Assert.Equal((50, 50), (
            monsterTypes.Definitions[133].MinimumSpecialDamage,
            monsterTypes.Definitions[133].MaximumSpecialDamage));
        Assert.Equal("Na-Krul", monsterTypes.Definitions[^1].Name);
        Assert.Equal(137U, monsterTypes.Definitions[^1].NativeMonsterId);
        Assert.Equal(125, monsterTypes.Definitions[^1].ArmorClass);
        Assert.Equal((40, 50), (
            monsterTypes.Definitions[^1].MinimumSpecialDamage,
            monsterTypes.Definitions[^1].MaximumSpecialDamage));
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
        var griswoldsEdge = hellfireItems.GenerateUnique(1010, 0x12345678, 1);
        Assert.Equal(1010, griswoldsEdge.UniqueId);
        Assert.Equal(42000, griswoldsEdge.Value);
        Assert.Equal(4, griswoldsEdge.MinDamage);
        Assert.Equal(12, griswoldsEdge.MaxDamage);
        Assert.Equal(1, griswoldsEdge.FireMinDamage);
        Assert.Equal(10, griswoldsEdge.FireMaxDamage);
        Assert.Equal(25, griswoldsEdge.PlusToHit);
        Assert.NotEqual(0U, griswoldsEdge.Flags & 0x00040000U);
        Assert.NotEqual(0U, griswoldsEdge.Flags & 0x00000800U);
        Assert.Equal(20 << 6, griswoldsEdge.PlusMana);
        Assert.Equal(-(20 << 6), griswoldsEdge.PlusHitPoints);
        var bovinePlate = hellfireItems.GenerateUnique(1011, 0x12345678, 1);
        Assert.Equal(1011, bovinePlate.UniqueId);
        Assert.Equal(400, bovinePlate.Value);
        Assert.Equal(150, bovinePlate.ArmorClass);
        Assert.Equal(255, bovinePlate.Durability);
        Assert.Equal(255, bovinePlate.MaxDurability);
        Assert.Equal(50, bovinePlate.MinimumStrength);
        Assert.Equal(30, bovinePlate.PlusFireResistance);
        Assert.Equal(30, bovinePlate.PlusLightningResistance);
        Assert.Equal(30, bovinePlate.PlusMagicResistance);
        Assert.Equal(5, bovinePlate.PlusLight);
        Assert.Equal(-(50 << 6), bovinePlate.PlusMana);
        Assert.Equal(-2, bovinePlate.SpellLevelAdd);
        var riftBow = hellfireItems.GenerateUnique(1012, 0x12345678, 1);
        Assert.Equal(1012, riftBow.UniqueId);
        Assert.Equal(1800, riftBow.Value);
        Assert.Equal(4, riftBow.ItemIndex);
        Assert.Equal(2, riftBow.PlusDamageModifier);
        Assert.Equal(-3, riftBow.PlusDexterity);
        Assert.NotEqual(0U, riftBow.Flags & 0x00000004U);
        var butchersCleaver = hellfireItems.GenerateUnique(1013, 0x12345678, 1);
        Assert.Equal(1013, butchersCleaver.UniqueId);
        Assert.Equal(3650, butchersCleaver.Value);
        Assert.Equal(6, butchersCleaver.ItemIndex);
        Assert.Equal(4, butchersCleaver.MinDamage);
        Assert.Equal(24, butchersCleaver.MaxDamage);
        Assert.Equal(10, butchersCleaver.PlusStrength);
        Assert.Equal(10, butchersCleaver.Durability);
        Assert.Equal(10, butchersCleaver.MaxDurability);
        var needler = hellfireItems.GenerateUnique(1014, 0x12345678, 2);
        Assert.Equal(1014, needler.UniqueId);
        Assert.Equal(8900, needler.Value);
        Assert.Equal(4, needler.ItemIndex);
        Assert.Equal(1, needler.MinDamage);
        Assert.Equal(3, needler.MaxDamage);
        Assert.Equal(50, needler.PlusToHit);
        Assert.NotEqual(0U, needler.Flags & 0x00040000U);
        var celestialBow = hellfireItems.GenerateUnique(1015, 0x12345678, 2);
        Assert.Equal(1015, celestialBow.UniqueId);
        Assert.Equal(1200, celestialBow.Value);
        Assert.Equal(146, celestialBow.ItemIndex);
        Assert.Equal(1, celestialBow.MinDamage);
        Assert.Equal(6, celestialBow.MaxDamage);
        Assert.Equal(0, celestialBow.MinimumStrength);
        Assert.Equal(2, celestialBow.PlusDamageModifier);
        Assert.Equal(5, celestialBow.ArmorClass);
        var deadlyHunter = hellfireItems.GenerateUnique(1016, 0x12345678, 3);
        Assert.Equal(1016, deadlyHunter.UniqueId);
        Assert.Equal(8750, deadlyHunter.Value);
        Assert.Equal(147, deadlyHunter.ItemIndex);
        Assert.Equal(3, deadlyHunter.MinDamage);
        Assert.Equal(6, deadlyHunter.MaxDamage);
        Assert.Equal(20, deadlyHunter.PlusToHit);
        Assert.Equal(-5, deadlyHunter.PlusMagic);
        Assert.NotEqual(0U, deadlyHunter.Flags & 0x40000000U);
        var bowOfTheDead = hellfireItems.GenerateUnique(1017, 0x12345678, 5);
        Assert.Equal(1017, bowOfTheDead.UniqueId);
        Assert.Equal(2500, bowOfTheDead.Value);
        Assert.Equal(147, bowOfTheDead.ItemIndex);
        Assert.Equal(10, bowOfTheDead.PlusToHit);
        Assert.Equal(4, bowOfTheDead.PlusDexterity);
        Assert.Equal(-3, bowOfTheDead.PlusVitality);
        Assert.Equal(-2, bowOfTheDead.PlusLight);
        Assert.Equal(30, bowOfTheDead.Durability);
        Assert.Equal(30, bowOfTheDead.MaxDurability);
        var blackoakBow = hellfireItems.GenerateUnique(1018, 0x12345678, 5);
        Assert.Equal(1018, blackoakBow.UniqueId);
        Assert.Equal(2500, blackoakBow.Value);
        Assert.Equal(146, blackoakBow.ItemIndex);
        Assert.Equal(10, blackoakBow.PlusDexterity);
        Assert.Equal(-10, blackoakBow.PlusVitality);
        Assert.Equal(50, blackoakBow.PlusDamage);
        Assert.Equal(-1, blackoakBow.PlusLight);
        var flamedart = hellfireItems.GenerateUnique(1019, 0x12345678, 10);
        Assert.Equal(1019, flamedart.UniqueId);
        Assert.Equal(14250, flamedart.Value);
        Assert.Equal(145, flamedart.ItemIndex);
        Assert.Equal(2, flamedart.MinDamage);
        Assert.Equal(5, flamedart.MaxDamage);
        Assert.Equal(20, flamedart.PlusToHit);
        Assert.Equal(40, flamedart.PlusFireResistance);
        Assert.Equal(1, flamedart.FireMinDamage);
        Assert.Equal(6, flamedart.FireMaxDamage);
        Assert.NotEqual(0U, flamedart.Flags & 0x00000010U);
        var fleshstinger = hellfireItems.GenerateUnique(1020, 0x12345678, 13);
        Assert.Equal(1020, fleshstinger.UniqueId);
        Assert.Equal(16500, fleshstinger.Value);
        Assert.Equal(146, fleshstinger.ItemIndex);
        Assert.Equal(15, fleshstinger.PlusDexterity);
        Assert.Equal(40, fleshstinger.PlusToHit);
        Assert.Equal(80, fleshstinger.PlusDamage);
        Assert.Equal(37, fleshstinger.Durability);
        Assert.Equal(37, fleshstinger.MaxDurability);
        var windforce = hellfireItems.GenerateUnique(1021, 0x12345678, 17);
        Assert.Equal(1021, windforce.UniqueId);
        Assert.Equal(37750, windforce.Value);
        Assert.Equal(151, windforce.ItemIndex);
        Assert.Equal(1, windforce.MinDamage);
        Assert.Equal(14, windforce.MaxDamage);
        Assert.Equal(5, windforce.PlusStrength);
        Assert.Equal(200, windforce.PlusDamage);
        Assert.NotEqual(0U, windforce.Flags & 0x00000800U);
        var eaglehorn = hellfireItems.GenerateUnique(1022, 0x12345678, 26);
        Assert.Equal(1022, eaglehorn.UniqueId);
        Assert.Equal(42500, eaglehorn.Value);
        Assert.Equal(149, eaglehorn.ItemIndex);
        Assert.Equal(1, eaglehorn.MinDamage);
        Assert.Equal(10, eaglehorn.MaxDamage);
        Assert.Equal(20, eaglehorn.PlusDexterity);
        Assert.Equal(50, eaglehorn.PlusToHit);
        Assert.Equal(100, eaglehorn.PlusDamage);
        Assert.Equal(255, eaglehorn.Durability);
        Assert.Equal(255, eaglehorn.MaxDurability);
        var gonnagalsDirk = hellfireItems.GenerateUnique(1023, 0x12345678, 1);
        Assert.Equal(1023, gonnagalsDirk.UniqueId);
        Assert.Equal(7040, gonnagalsDirk.Value);
        Assert.Equal(119, gonnagalsDirk.ItemIndex);
        Assert.Equal(1, gonnagalsDirk.MinDamage);
        Assert.Equal(4, gonnagalsDirk.MaxDamage);
        Assert.Equal(-5, gonnagalsDirk.PlusDexterity);
        Assert.Equal(4, gonnagalsDirk.PlusDamageModifier);
        Assert.Equal(25, gonnagalsDirk.PlusFireResistance);
        Assert.NotEqual(0U, gonnagalsDirk.Flags & 0x00040000U);
        var defender = hellfireItems.GenerateUnique(1024, 0x12345678, 1);
        Assert.Equal(1024, defender.UniqueId);
        Assert.Equal(2000, defender.Value);
        Assert.Equal(125, defender.ItemIndex);
        Assert.Equal(1, defender.MinDamage);
        Assert.Equal(8, defender.MaxDamage);
        Assert.Equal(5, defender.ArmorClass);
        Assert.Equal(17, defender.MinimumStrength);
        Assert.Equal(5, defender.PlusVitality);
        Assert.Equal(-5, defender.PlusToHit);
        var gryphonsClaw = hellfireItems.GenerateUnique(1025, 0x12345678, 1);
        Assert.Equal(1025, gryphonsClaw.UniqueId);
        Assert.Equal(1000, gryphonsClaw.Value);
        Assert.Equal(121, gryphonsClaw.ItemIndex);
        Assert.Equal(4, gryphonsClaw.MinDamage);
        Assert.Equal(8, gryphonsClaw.MaxDamage);
        Assert.Equal(30, gryphonsClaw.MinimumStrength);
        Assert.Equal(100, gryphonsClaw.PlusDamage);
        Assert.Equal(-2, gryphonsClaw.PlusMagic);
        Assert.Equal(-5, gryphonsClaw.PlusDexterity);
        var blackRazor = hellfireItems.GenerateUnique(1026, 0x12345678, 1);
        Assert.Equal(1026, blackRazor.UniqueId);
        Assert.Equal(2000, blackRazor.Value);
        Assert.Equal(119, blackRazor.ItemIndex);
        Assert.Equal(1, blackRazor.MinDamage);
        Assert.Equal(4, blackRazor.MaxDamage);
        Assert.Equal(150, blackRazor.PlusDamage);
        Assert.Equal(2, blackRazor.PlusVitality);
        Assert.Equal(5, blackRazor.Durability);
        Assert.Equal(5, blackRazor.MaxDurability);
        var gibbousMoon = hellfireItems.GenerateUnique(1027, 0x12345678, 2);
        Assert.Equal(1027, gibbousMoon.UniqueId);
        Assert.Equal(6660, gibbousMoon.Value);
        Assert.Equal(127, gibbousMoon.ItemIndex);
        Assert.Equal(4, gibbousMoon.MinDamage);
        Assert.Equal(12, gibbousMoon.MaxDamage);
        Assert.Equal(40, gibbousMoon.MinimumStrength);
        Assert.Equal(2, gibbousMoon.PlusStrength);
        Assert.Equal(2, gibbousMoon.PlusMagic);
        Assert.Equal(2, gibbousMoon.PlusDexterity);
        Assert.Equal(2, gibbousMoon.PlusVitality);
        Assert.Equal(25, gibbousMoon.PlusDamage);
        Assert.Equal(15 << 6, gibbousMoon.PlusMana);
        Assert.Equal(-3, gibbousMoon.PlusLight);
        var iceShank = hellfireItems.GenerateUnique(1028, 0x12345678, 3);
        Assert.Equal(1028, iceShank.UniqueId);
        Assert.Equal(5250, iceShank.Value);
        Assert.Equal(126, iceShank.ItemIndex);
        Assert.Equal(2, iceShank.MinDamage);
        Assert.Equal(10, iceShank.MaxDamage);
        Assert.Equal(30, iceShank.MinimumStrength);
        Assert.Equal(30, iceShank.MinimumDexterity);
        Assert.Equal(40, iceShank.PlusFireResistance);
        Assert.Equal(15, iceShank.Durability);
        Assert.Equal(15, iceShank.MaxDurability);
        Assert.InRange(iceShank.PlusStrength, 5, 10);
        var executionersBlade = hellfireItems.GenerateUnique(1029, 0x12345678, 3);
        Assert.Equal(1029, executionersBlade.UniqueId);
        Assert.Equal(7080, executionersBlade.Value);
        Assert.Equal(121, executionersBlade.ItemIndex);
        Assert.Equal(4, executionersBlade.MinDamage);
        Assert.Equal(8, executionersBlade.MaxDamage);
        Assert.Equal(30, executionersBlade.MinimumStrength);
        Assert.Equal(150, executionersBlade.PlusDamage);
        Assert.Equal(-(10 << 6), executionersBlade.PlusHitPoints);
        Assert.Equal(-1, executionersBlade.PlusLight);
        Assert.Equal(60, executionersBlade.Durability);
        Assert.Equal(60, executionersBlade.MaxDurability);
        var bonesaw = hellfireItems.GenerateUnique(1030, 0x12345678, 6);
        Assert.Equal(1030, bonesaw.UniqueId);
        Assert.Equal(4400, bonesaw.Value);
        Assert.Equal(123, bonesaw.ItemIndex);
        Assert.Equal(1, bonesaw.MinDamage);
        Assert.Equal(12, bonesaw.MaxDamage);
        Assert.Equal(35, bonesaw.MinimumStrength);
        Assert.Equal(10, bonesaw.PlusDamageModifier);
        Assert.Equal(10, bonesaw.PlusStrength);
        Assert.Equal(-5, bonesaw.PlusMagic);
        Assert.Equal(-5, bonesaw.PlusDexterity);
        Assert.Equal(10 << 6, bonesaw.PlusHitPoints);
        Assert.Equal(-(10 << 6), bonesaw.PlusMana);
        var shadowhawk = hellfireItems.GenerateUnique(1031, 0x12345678, 8);
        Assert.Equal(1031, shadowhawk.UniqueId);
        Assert.Equal(13750, shadowhawk.Value);
        Assert.Equal(127, shadowhawk.ItemIndex);
        Assert.Equal(4, shadowhawk.MinDamage);
        Assert.Equal(12, shadowhawk.MaxDamage);
        Assert.Equal(40, shadowhawk.MinimumStrength);
        Assert.Equal(-2, shadowhawk.PlusLight);
        Assert.Equal(15, shadowhawk.PlusToHit);
        Assert.Equal(5, shadowhawk.PlusFireResistance);
        Assert.Equal(5, shadowhawk.PlusLightningResistance);
        Assert.Equal(5, shadowhawk.PlusMagicResistance);
        Assert.NotEqual(0U, shadowhawk.Flags & 0x00010000U);
        var wizardspike = hellfireItems.GenerateUnique(1032, 0x12345678, 11);
        Assert.Equal(1032, wizardspike.UniqueId);
        Assert.Equal(12920, wizardspike.Value);
        Assert.Equal(119, wizardspike.ItemIndex);
        Assert.Equal(1, wizardspike.MinDamage);
        Assert.Equal(4, wizardspike.MaxDamage);
        Assert.Equal(15, wizardspike.PlusMagic);
        Assert.Equal(35 << 6, wizardspike.PlusMana);
        Assert.Equal(25, wizardspike.PlusToHit);
        Assert.Equal(15, wizardspike.PlusFireResistance);
        Assert.Equal(15, wizardspike.PlusLightningResistance);
        Assert.Equal(15, wizardspike.PlusMagicResistance);
        var lightsabre = hellfireItems.GenerateUnique(1033, 0x12345678, 13);
        Assert.Equal(1033, lightsabre.UniqueId);
        Assert.Equal(19150, lightsabre.Value);
        Assert.Equal(125, lightsabre.ItemIndex);
        Assert.Equal(1, lightsabre.MinDamage);
        Assert.Equal(8, lightsabre.MaxDamage);
        Assert.Equal(17, lightsabre.MinimumStrength);
        Assert.Equal(2, lightsabre.PlusLight);
        Assert.Equal(1, lightsabre.LightningMinDamage);
        Assert.Equal(10, lightsabre.LightningMaxDamage);
        Assert.Equal(20, lightsabre.PlusToHit);
        Assert.Equal(50, lightsabre.PlusLightningResistance);
        var falconsTalon = hellfireItems.GenerateUnique(1034, 0x12345678, 15);
        Assert.Equal(1034, falconsTalon.UniqueId);
        Assert.Equal(7867, falconsTalon.Value);
        Assert.Equal(122, falconsTalon.ItemIndex);
        Assert.Equal(3, falconsTalon.MinDamage);
        Assert.Equal(7, falconsTalon.MaxDamage);
        Assert.Equal(23, falconsTalon.MinimumStrength);
        Assert.Equal(23, falconsTalon.MinimumDexterity);
        Assert.Equal(20, falconsTalon.PlusToHit);
        Assert.Equal(-33, falconsTalon.PlusDamage);
        Assert.Equal(10, falconsTalon.PlusDexterity);
        Assert.NotEqual(0U, falconsTalon.Flags & 0x00100000U);
        var inferno = hellfireItems.GenerateUnique(1035, 0x12345678, 17);
        Assert.Equal(1035, inferno.UniqueId);
        Assert.Equal(34600, inferno.Value);
        Assert.Equal(126, inferno.ItemIndex);
        Assert.Equal(2, inferno.MinDamage);
        Assert.Equal(10, inferno.MaxDamage);
        Assert.Equal(30, inferno.MinimumStrength);
        Assert.Equal(30, inferno.MinimumDexterity);
        Assert.Equal(2, inferno.FireMinDamage);
        Assert.Equal(12, inferno.FireMaxDamage);
        Assert.Equal(3, inferno.PlusLight);
        Assert.Equal(20 << 6, inferno.PlusMana);
        Assert.Equal(80, inferno.PlusFireResistance);
        var doombringer = hellfireItems.GenerateUnique(1036, 0x12345678, 19);
        Assert.Equal(1036, doombringer.UniqueId);
        Assert.Equal(18250, doombringer.Value);
        Assert.Equal(129, doombringer.ItemIndex);
        Assert.Equal(8, doombringer.MinDamage);
        Assert.Equal(16, doombringer.MaxDamage);
        Assert.Equal(65, doombringer.MinimumStrength);
        Assert.Equal(25, doombringer.PlusToHit);
        Assert.Equal(250, doombringer.PlusDamage);
        Assert.Equal(-5, doombringer.PlusStrength);
        Assert.Equal(-5, doombringer.PlusMagic);
        Assert.Equal(-5, doombringer.PlusDexterity);
        Assert.Equal(-5, doombringer.PlusVitality);
        Assert.Equal(-(25 << 6), doombringer.PlusHitPoints);
        Assert.Equal(-2, doombringer.PlusLight);
        var grandfather = hellfireItems.GenerateUnique(1037, 0x12345678, 27);
        Assert.Equal(1037, grandfather.UniqueId);
        Assert.Equal(119800, grandfather.Value);
        Assert.Equal(130, grandfather.ItemIndex);
        Assert.Equal(10, grandfather.MinDamage);
        Assert.Equal(20, grandfather.MaxDamage);
        Assert.Equal(75, grandfather.MinimumStrength);
        Assert.Equal(1, grandfather.EquipLocation);
        Assert.Equal(5, grandfather.PlusStrength);
        Assert.Equal(5, grandfather.PlusMagic);
        Assert.Equal(5, grandfather.PlusDexterity);
        Assert.Equal(5, grandfather.PlusVitality);
        Assert.Equal(20, grandfather.PlusToHit);
        Assert.Equal(70, grandfather.PlusDamage);
        Assert.Equal(20 << 6, grandfather.PlusHitPoints);
        var mangler = hellfireItems.GenerateUnique(1038, 0x12345678, 2);
        Assert.Equal(1038, mangler.UniqueId);
        Assert.Equal(2850, mangler.Value);
        Assert.Equal(133, mangler.ItemIndex);
        Assert.Equal(6, mangler.MinDamage);
        Assert.Equal(16, mangler.MaxDamage);
        Assert.Equal(30, mangler.MinimumStrength);
        Assert.Equal(2, mangler.EquipLocation);
        Assert.Equal(200, mangler.PlusDamage);
        Assert.Equal(-5, mangler.PlusDexterity);
        Assert.Equal(-5, mangler.PlusMagic);
        Assert.Equal(-(10 << 6), mangler.PlusMana);
        var sharpBeak = hellfireItems.GenerateUnique(1039, 0x12345678, 2);
        Assert.Equal(1039, sharpBeak.UniqueId);
        Assert.Equal(2850, sharpBeak.Value);
        Assert.Equal(133, sharpBeak.ItemIndex);
        Assert.Equal(6, sharpBeak.MinDamage);
        Assert.Equal(16, sharpBeak.MaxDamage);
        Assert.Equal(30, sharpBeak.MinimumStrength);
        Assert.Equal(2, sharpBeak.EquipLocation);
        Assert.Equal(20 << 6, sharpBeak.PlusHitPoints);
        Assert.Equal(-10, sharpBeak.PlusMagic);
        Assert.Equal(-(10 << 6), sharpBeak.PlusMana);
        var bloodSlayer = hellfireItems.GenerateUnique(1040, 0x12345678, 3);
        Assert.Equal(1040, bloodSlayer.UniqueId);
        Assert.Equal(2500, bloodSlayer.Value);
        Assert.Equal(134, bloodSlayer.ItemIndex);
        Assert.Equal(8, bloodSlayer.MinDamage);
        Assert.Equal(20, bloodSlayer.MaxDamage);
        Assert.Equal(50, bloodSlayer.MinimumStrength);
        Assert.Equal(2, bloodSlayer.EquipLocation);
        Assert.Equal(100, bloodSlayer.PlusDamage);
        Assert.NotEqual(0U, bloodSlayer.Flags & 0x40000000U);
        Assert.Equal(-5, bloodSlayer.PlusStrength);
        Assert.Equal(-5, bloodSlayer.PlusMagic);
        Assert.Equal(-5, bloodSlayer.PlusDexterity);
        Assert.Equal(-5, bloodSlayer.PlusVitality);
        Assert.Equal(-1, bloodSlayer.SpellLevelAdd);
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
