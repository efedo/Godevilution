using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativePlayerClassCatalogTests
{
    [Fact]
    public void ResolvesStableClassSymbolsIncludingNativeZero()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("class", "warrior", 0),
            new StableContentId("class", "rogue", 1),
            new StableContentId("class", "sorcerer", 2),
            new StableContentId("class", "monk", 3),
            new StableContentId("class", "bard", 4),
            new StableContentId("class", "barbarian", 5),
        ]);
        var catalog = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            ReadShippedTable(),
            stableIds);

        Assert.Equal(0U, catalog.Resolve("warrior").ClassId);
        Assert.Equal(5U, catalog.Resolve("barbarian").ClassId);
    }

    [Fact]
    public void RejectsStableClassSymbolNumericMismatch()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("class", "warrior", 0),
            new StableContentId("class", "rogue", 1),
            new StableContentId("class", "sorcerer", 2),
            new StableContentId("class", "monk", 3),
            new StableContentId("class", "bard", 4),
            new StableContentId("class", "barbarian", 5),
        ]);
        var source = ReadShippedTable().Replace(
            "0\twarrior\tWarrior",
            "1\twarrior\tWarrior",
            StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", source, stableIds));

        Assert.Contains("maps class 1 to symbol 'warrior', which resolves to 0", exception.Message);
    }

    [Fact]
    public void ProductionValidationRejectsLegacyClassSchema()
    {
        var legacyTable = TsvTable.Parse(
            "classes.tsv",
            "class_id\tsymbol\tdisplay_name\tavailability\tis_default\tclass_flags\n"
                + "0\twarrior\tWarrior\tBaseGame\ttrue\tCriticalStrike\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.ValidateExternalizedProduction(legacyTable));

        Assert.Contains("complete ordered class tuning schema", exception.Message);
    }

    [Fact]
    public void ProductionValidationAcceptsShippedClassSchema()
    {
        var table = TsvTable.Parse("classes.tsv", ReadShippedTable());

        AuthoritativePlayerClassCatalog.ValidateExternalizedProduction(table);
    }

    [Fact]
    public void ShippedCatalogPreservesBaseAndHellfireClassMetadata()
    {
        var catalog = LoadShippedCatalog();

        Assert.Equal(6, catalog.Definitions.Count);
        Assert.Equal("warrior", catalog.Default.Symbol);
        Assert.Equal(0U, catalog.Default.ClassId);
        Assert.Equal(200, catalog.Default.SelfHealingSpellPercent);
        Assert.Equal(200, catalog.Default.HealOtherSpellPercent);
        Assert.Equal(100, catalog.Default.ManaSpellPercent);
        Assert.Equal(0, catalog.Default.MeleeArmorReductionDivisor);
        Assert.Equal(9, catalog.Default.RepairAbilityDurabilityLevelOffset);
        Assert.Equal(2, catalog.Default.CriticalDamageMultiplier);
        Assert.Equal(1, catalog.Default.CriticalStrikeChancePerLevelPercent);
        Assert.Equal(4, catalog.Resolve("barbarian").IronSkinLevelDivisor);
        Assert.Equal(100, catalog.Resolve("barbarian").NaturalResistancePerLevelPercent);
        Assert.Equal(25, catalog.Resolve("barbarian").HeavyHitThresholdBonusPercent);
        Assert.Equal(2, catalog.Resolve("monk").FastBlockFrameSkip);
        Assert.Equal(8, catalog.Resolve("warrior").RangedArrowSpeedLevelDivisor);
        Assert.Equal(4, catalog.Resolve("rogue").RangedArrowSpeedLevelDivisor);
        Assert.Equal(8, catalog.Resolve("bard").RangedArrowSpeedLevelDivisor);
        Assert.Equal(0, catalog.Resolve("sorcerer").RangedArrowSpeedLevelDivisor);
        Assert.Equal(20, catalog.Default.PvpEarCursorGraphic);
        Assert.Equal(
            new PlayerAttributesState(
                new PlayerAttributeState(30, 30),
                new PlayerAttributeState(10, 10),
                new PlayerAttributeState(20, 20),
                new PlayerAttributeState(25, 25)),
            catalog.Default.CreateStartingAttributes());

        var monk = catalog.Resolve("monk");
        Assert.Equal(3U, monk.ClassId);
        Assert.Equal(DiabloPlayerClassAvailability.Hellfire, monk.Availability);
        Assert.Equal(352, monk.ManaAdjustmentQ6);
        Assert.Equal(96, monk.ItemLifePerVitalityQ6);
        Assert.Equal(AuthoritativePhysicalDamageFormula.Monk, monk.PhysicalDamageFormula);
        Assert.Equal(2, monk.MonkUnarmedMinimumDamageLevelDivisor);
        Assert.Equal(1, monk.MonkUnarmedMaximumDamageLevelDivisor);
        Assert.Equal(
            DiabloPlayerClassFlags.FlexibleBlock
                | DiabloPlayerClassFlags.FastBlock,
            monk.Flags);
        Assert.Equal(25, monk.SpellManaReductionPercent);
        Assert.Equal(25, monk.HellfireSpellManaReductionPercent);
        Assert.Equal(200, monk.SelfHealingSpellPercent);
        Assert.Equal(300, monk.HealOtherSpellPercent);
        Assert.Equal(2, monk.MonkLightArmorLevelMultiplier);
        Assert.Equal(1, monk.MonkMediumArmorLevelMultiplier);
        Assert.Equal(2, monk.MonkUniqueMediumArmorLevelMultiplier);
        Assert.Equal(1, monk.MonkUniqueHeavyArmorLevelMultiplier);
        Assert.Equal(2, monk.MonkArmorLevelDivisor);

        var sorcerer = catalog.Resolve("sorcerer");
        Assert.Equal(0, sorcerer.SpellManaReductionPercent);
        Assert.Equal(50, sorcerer.HellfireSpellManaReductionPercent);
        Assert.Equal(100, sorcerer.SelfHealingSpellPercent);
        Assert.Equal(100, sorcerer.HealOtherSpellPercent);
        Assert.Equal(200, sorcerer.ManaSpellPercent);
        Assert.Equal(19, sorcerer.PvpEarCursorGraphic);

        var bard = catalog.Resolve("bard");
        Assert.Equal(DiabloPlayerClassAvailability.HellfireHidden, bard.Availability);
        Assert.Equal(DiabloPlayerClassFlags.DualWield, bard.Flags);
        Assert.Equal(112, bard.ItemManaPerMagicQ6);
        Assert.Equal(150, bard.SelfHealingSpellPercent);
        Assert.Equal(150, bard.HealOtherSpellPercent);
        Assert.Equal(150, bard.ManaSpellPercent);
        Assert.Equal(21, bard.PvpEarCursorGraphic);

        var barbarian = catalog.Resolve("barbarian");
        Assert.Equal(5U, barbarian.ClassId);
        Assert.Equal(0, barbarian.MaximumMagic);
        Assert.Equal(160, barbarian.ItemLifePerVitalityQ6);
        Assert.Equal(200, barbarian.SelfHealingSpellPercent);
        Assert.Equal(200, barbarian.HealOtherSpellPercent);
        Assert.Equal(8, barbarian.MeleeArmorReductionDivisor);
        Assert.Equal(
            AuthoritativePhysicalDamageFormula.Barbarian,
            barbarian.PhysicalDamageFormula);
        Assert.Equal(
            DiabloPlayerClassFlags.CriticalStrike
                | DiabloPlayerClassFlags.IronSkin
                | DiabloPlayerClassFlags.NaturalResistance
                | DiabloPlayerClassFlags.HeavyHitThreshold,
            barbarian.Flags);
    }

    [Fact]
    public void CatalogRequiresExactlyOneDeclarativeDefault()
    {
        var source = ReadShippedTable();
        var withoutDefault = source.Replace(
            "0\twarrior\tWarrior\tBaseGame\ttrue\t",
            "0\twarrior\tWarrior\tBaseGame\tfalse\t",
            StringComparison.Ordinal);
        var withTwoDefaults = source.Replace(
            "1\trogue\tRogue\tBaseGame\tfalse\t",
            "1\trogue\tRogue\tBaseGame\ttrue\t",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", withoutDefault));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", withTwoDefaults));
    }

    [Fact]
    public void LegacyClassRowsDefaultManaSpellMultiplierToOneHundredPercent()
    {
        var legacySource = string.Join(
            '\n',
            ReadShippedTable()
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => {
                    if (!line.Contains('\t'))
                        return line;
                    var legacyLine = line;
                    for (var remove = 0; remove < 13; remove++)
                        legacyLine = legacyLine[..legacyLine.LastIndexOf('\t')];
                    return legacyLine;
                }));

        var catalog = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            legacySource);

        Assert.Equal(100, catalog.Resolve("sorcerer").ManaSpellPercent);
    }

    [Fact]
    public void LegacyClassRowsRetainNativeTraitDefaults()
    {
        var legacySource = ReadShippedTable()
            .Replace("FlexibleBlock,FastBlock", "FlexibleBlock", StringComparison.Ordinal)
            .Replace(
                "CriticalStrike,IronSkin,NaturalResistance,HeavyHitThreshold",
                "CriticalStrike,IronSkin,NaturalResistance",
                StringComparison.Ordinal);

        var catalog = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            legacySource);

        Assert.True(catalog.Resolve("monk").Flags.HasFlag(
            DiabloPlayerClassFlags.FastBlock));
        Assert.True(catalog.Resolve("barbarian").Flags.HasFlag(
            DiabloPlayerClassFlags.HeavyHitThreshold));
    }

    [Fact]
    public void LoadsAndValidatesRangedArrowSpeedLevelDivisor()
    {
        var source = ReadShippedTable();
        var configured = source.Replace(
            "\t25\t2\t8",
            "\t25\t2\t6",
            StringComparison.Ordinal);

        var catalog = AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", configured);

        Assert.Equal(6, catalog.Default.RangedArrowSpeedLevelDivisor);

        var invalid = configured.Replace(
            "\t25\t2\t6",
            "\t25\t2\t101",
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalid));
    }

    [Fact]
    public void LegacyClassRowsRetainNativeDefenseScalarDefaults()
    {
        var legacySource = string.Join(
            '\n',
            ReadShippedTable()
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => {
                    if (!line.Contains('\t'))
                        return line;
                    var legacyLine = line;
                    for (var remove = 0; remove < 4; remove++)
                        legacyLine = legacyLine[..legacyLine.LastIndexOf('\t')];
                    return legacyLine;
                }));

        var catalog = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            legacySource);
        var barbarian = catalog.Resolve("barbarian");

        Assert.Equal(100, barbarian.NaturalResistancePerLevelPercent);
        Assert.Equal(25, barbarian.HeavyHitThresholdBonusPercent);
        Assert.Equal(2, catalog.Resolve("monk").FastBlockFrameSkip);
        Assert.Equal(4, catalog.Resolve("rogue").RangedArrowSpeedLevelDivisor);
        Assert.Equal(8, catalog.Resolve("warrior").RangedArrowSpeedLevelDivisor);
    }

    [Fact]
    public void LegacyClassRowsInferNativePhysicalDamageFormulas()
    {
        var legacySource = ReadShippedTable();
        legacySource = string.Join(
            '\n',
            legacySource
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => {
                    if (!line.Contains('\t'))
                        return line;
                    var legacyLine = line;
                    for (var remove = 0; remove < 11; remove++)
                        legacyLine = legacyLine[..legacyLine.LastIndexOf('\t')];
                    return legacyLine;
                }));

        var catalog = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            legacySource);

        Assert.Equal(
            AuthoritativePhysicalDamageFormula.Rogue,
            catalog.Resolve("rogue").PhysicalDamageFormula);
        Assert.Equal(
            AuthoritativePhysicalDamageFormula.Bard,
            catalog.Resolve("bard").PhysicalDamageFormula);
        Assert.Equal(
            AuthoritativePhysicalDamageFormula.Standard,
            catalog.Resolve("sorcerer").PhysicalDamageFormula);
        Assert.Equal(2, catalog.Resolve("monk").MonkUnarmedMinimumDamageLevelDivisor);
        Assert.Equal(1, catalog.Resolve("monk").MonkUnarmedMaximumDamageLevelDivisor);
    }

    [Fact]
    public void ClassDefenseTuningCalculatesNativeLevelContributions()
    {
        var barbarian = LoadShippedCatalog().Resolve("barbarian");

        Assert.Equal(12, barbarian.CalculateNaturalResistanceBonus(12));
        Assert.Equal(15, barbarian.CalculateHeavyHitThreshold(12));

        var tuned = barbarian with {
            NaturalResistancePerLevelPercent = 50,
            HeavyHitThresholdBonusPercent = 50,
        };
        Assert.Equal(6, tuned.CalculateNaturalResistanceBonus(12));
        Assert.Equal(18, tuned.CalculateHeavyHitThreshold(12));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            barbarian.CalculateNaturalResistanceBonus(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            barbarian.CalculateHeavyHitThreshold(0));
    }

    [Fact]
    public void ManaOnlyClassRowsDefaultMeleeArmorReductionToZero()
    {
        var manaOnlySource = string.Join(
            '\n',
            ReadShippedTable()
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => {
                    if (!line.Contains('\t'))
                        return line;
                    var legacyLine = line;
                    for (var remove = 0; remove < 12; remove++)
                        legacyLine = legacyLine[..legacyLine.LastIndexOf('\t')];
                    return legacyLine;
                }));

        var catalog = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            manaOnlySource);

        Assert.Equal(200, catalog.Resolve("sorcerer").ManaSpellPercent);
        Assert.Equal(0, catalog.Resolve("barbarian").MeleeArmorReductionDivisor);
    }

    [Fact]
    public void ShippedClassesCalculateNativeStartingLifeAndManaInQ6Units()
    {
        var catalog = LoadShippedCatalog();
        var expected = new Dictionary<string, (int Life, int Mana)> {
            ["warrior"] = (4480, 640),
            ["rogue"] = (2880, 1440),
            ["sorcerer"] = (1920, 4480),
            ["monk"] = (2880, 1440),
            ["bard"] = (2880, 2240),
            ["barbarian"] = (4480, 0),
        };

        foreach (var (symbol, resources) in expected) {
            var definition = catalog.Resolve(symbol);
            Assert.Equal(
                resources.Life,
                definition.CalculateBaseLifeQ6(1, definition.BaseVitality));
            Assert.Equal(
                resources.Mana,
                definition.CalculateBaseManaQ6(1, definition.BaseMagic));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            catalog.Default.CalculateBaseLifeQ6(0, catalog.Default.BaseVitality));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            catalog.Default.CalculateBaseManaQ6(1, catalog.Default.MaximumMagic + 1));
    }

    [Fact]
    public void ShippedClassesCalculateNativeMeleeAndRangedToHit()
    {
        var catalog = LoadShippedCatalog();
        var warrior = catalog.Resolve("warrior");
        var sorcerer = catalog.Resolve("sorcerer");

        Assert.Equal(81, warrior.CalculatePhysicalToHit(1, 20, ranged: false));
        Assert.Equal(81, warrior.CalculatePhysicalToHit(1, 20, ranged: true));
        Assert.Equal(61, sorcerer.CalculatePhysicalToHit(1, 20, ranged: false));
        Assert.Equal(71, sorcerer.CalculatePhysicalToHit(1, 20, ranged: true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            warrior.CalculatePhysicalToHit(0, 20, ranged: false));
    }

    [Fact]
    public void ClassScaledEquipmentAttributesAffectNativeResourceMaxima()
    {
        var playerClass = LoadShippedCatalog().Resolve("bard");
        var item = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                PlusMagic = 5,
                PlusVitality = 7,
            });
        var unidentified = new EquippedStoreItem(
            5,
            43,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                Magical = 1,
                PlusMagic = 100,
                PlusVitality = 100,
            });

        var resources = AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
            64 << 6,
            64 << 6,
            [item, unidentified],
            playerClass);

        Assert.Equal((64 + ((7 * playerClass.ItemLifePerVitalityQ6 >> 6))) << 6, resources.LifeQ6);
        Assert.Equal((64 + ((5 * playerClass.ItemManaPerMagicQ6 >> 6))) << 6, resources.ManaQ6);
    }

    [Fact]
    public void CatalogRejectsUnstableIdentityAndUnknownMetadata()
    {
        var source = ReadShippedTable();
        var nonContiguousIds = source.Replace(
            "5\tbarbarian\t",
            "6\tbarbarian\t",
            StringComparison.Ordinal);
        var duplicateId = source.Replace(
            "1\trogue\t",
            "0\trogue\t",
            StringComparison.Ordinal);
        var duplicateSymbol = source.Replace(
            "1\trogue\tRogue\t",
            "1\twarrior\tRogue\t",
            StringComparison.Ordinal);
        var invalidSymbol = source.Replace(
            "0\twarrior\t",
            "0\tWarrior!\t",
            StringComparison.Ordinal);
        var unknownAvailability = source.Replace(
            "\tHellfire\tfalse\t",
            "\tExpansion\tfalse\t",
            StringComparison.Ordinal);
        var unknownFlag = source.Replace(
            "\tCriticalStrike\t30\t",
            "\tUnknownPower\t30\t",
            StringComparison.Ordinal);
        var numericAvailability = source.Replace(
            "\tHellfire\tfalse\t",
            "\t1\tfalse\t",
            StringComparison.Ordinal);
        var numericFlag = source.Replace(
            "\tCriticalStrike\t30\t",
            "\t1\t30\t",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", nonContiguousIds));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", duplicateId));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", duplicateSymbol));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidSymbol));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", unknownAvailability));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", unknownFlag));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", numericAvailability));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", numericFlag));
    }

    [Fact]
    public void CatalogRejectsSchemaDriftAndInvalidNativeRanges()
    {
        var source = ReadShippedTable();
        var changedSchema = source.Replace(
            "class_id\tsymbol\t",
            "id\tsymbol\t",
            StringComparison.Ordinal);
        var invalidAttributeBounds = source.Replace(
            "\t30\t10\t20\t25\t250\t",
            "\t251\t10\t20\t25\t250\t",
            StringComparison.Ordinal);
        var invalidPercentage = source.Replace(
            "\t100\t30\t50\t70\t60\t",
            "\t100\t101\t50\t70\t60\t",
            StringComparison.Ordinal);
        var invalidQ6 = source.Replace(
            "\t1152\t-64\t",
            "\t40000\t-64\t",
            StringComparison.Ordinal);
        var invalidHealingPercentage = source.Replace(
            "\t0\t0\t200\t200\t20",
            "\t0\t0\t401\t200\t20",
            StringComparison.Ordinal);
        var invalidHealOtherPercentage = source.Replace(
            "\t0\t0\t200\t200\t20",
            "\t0\t0\t200\t401\t20",
            StringComparison.Ordinal);
        var invalidEarCursor = source.Replace(
            "\t0\t0\t200\t200\t20",
            "\t0\t0\t200\t200\t256",
            StringComparison.Ordinal);
        var invalidMonkArmorTuning = source.Replace(
            "\t2\t1\t2\t1\t2",
            "\t-1\t1\t2\t1\t2",
            StringComparison.Ordinal);
        var invalidManaSpellPercentage = string.Join(
            '\n',
            source
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => line.StartsWith("2\tsorcerer\t", StringComparison.Ordinal)
                    ? line.Replace("\t200\t0", "\t401\t0", StringComparison.Ordinal)
                    : line));
        var invalidMeleeArmorReduction = string.Join(
            '\n',
            source
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => line.StartsWith("5\tbarbarian\t", StringComparison.Ordinal)
                    ? line[..line.LastIndexOf('\t')] + "\t-1"
                    : line));
        var invalidPhysicalDamageFormula = source.Replace(
            "\tStandard",
            "\tUnknown",
            StringComparison.Ordinal);
        var invalidMonkUnarmedDivisor = source.Replace(
            "\tMonk\t2\t1\t0\t2",
            "\tMonk\t0\t1\t0\t2",
            StringComparison.Ordinal);
        var invalidCriticalMultiplier = source.Replace(
            "\t9\t2\t1",
            "\t9\t0\t1",
            StringComparison.Ordinal);
        var invalidCriticalChance = source.Replace(
            "\t2\t1\t4",
            "\t2\t101\t4",
            StringComparison.Ordinal);
        var invalidIronSkinDivisor = source.Replace(
            "\t2\t1\t4",
            "\t2\t1\t0",
            StringComparison.Ordinal);
        var invalidNaturalResistancePercent = source.Replace(
            "\t4\t100\t25",
            "\t4\t101\t25",
            StringComparison.Ordinal);
        var invalidHeavyHitThresholdPercent = source.Replace(
            "\t4\t100\t25",
            "\t4\t100\t101",
            StringComparison.Ordinal);
        var invalidFastBlockFrameSkip = source.Replace(
            "\t4\t100\t25\t2",
            "\t4\t100\t25\t11",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", changedSchema));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidAttributeBounds));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidPercentage));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidQ6));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidHealingPercentage));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidHealOtherPercentage));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidEarCursor));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidMonkArmorTuning));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidManaSpellPercentage));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidMeleeArmorReduction));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidPhysicalDamageFormula));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidMonkUnarmedDivisor));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidCriticalMultiplier));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidCriticalChance));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", invalidIronSkinDivisor));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv(
                "classes.tsv",
                invalidNaturalResistancePercent));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv(
                "classes.tsv",
                invalidHeavyHitThresholdPercent));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerClassCatalog.LoadTsv(
                "classes.tsv",
                invalidFastBlockFrameSkip));
    }

    private static AuthoritativePlayerClassCatalog LoadShippedCatalog()
    {
        return AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            ReadShippedTable());
    }

    private static string ReadShippedTable()
    {
        return File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "base",
            "classes.tsv"));
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
