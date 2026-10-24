using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralNativeCharacterStartTests
{
    [Fact]
    public void MissingTableRetainsThePinnedRogueHeroFacts()
    {
        var catalogs = LoadCatalogs();

        var start = AuthoritativeCathedralNativeCharacterStart.Resolve(
            null, catalogs.Classes, catalogs.Animations, catalogs.Loadouts);

        Assert.False(start.HasCompiledTable);
        Assert.Equal(1U, start.ClassId);
        Assert.Equal("rogue", start.ClassSymbol);
        Assert.Equal(2880, start.Hero.MaximumLifeBase);
        Assert.Equal(1440, start.Hero.MaximumManaBase);
        Assert.Equal(8, start.Hero.LightRadius);
        Assert.Equal(catalogs.Animations.GetRequired(1).IdleFrames, start.Hero.IdleFrames);
        Assert.Equal((57, 40), (start.Hero.PreEntryFuture.X, start.Hero.PreEntryFuture.Y));
        Assert.True(start.Hero.LevelChanging);
        Assert.True(start.Hero.LevelLoading);
    }

    [Theory]
    [InlineData(0, "warrior", 4480, 640)]
    [InlineData(2, "sorcerer", 1920, 4480)]
    public void CompiledClassStartUsesCanonicalCatalogFacts(
        uint classId,
        string symbol,
        int expectedLife,
        int expectedMana)
    {
        var catalogs = LoadCatalogs();
        var template = catalogs.Loadouts.CreateTemplate(
            "native-cathedral-population-profile", classId, null, null, null, 1,
            startingPositionX: 57, startingPositionY: 40);
        var player = template.CreatePlayer();
        var animation = catalogs.Animations.GetRequired(classId);
        var table = CreateTable(classId, symbol, expectedLife, expectedMana,
            player.LightRadius, animation.IdleFrames);

        var start = AuthoritativeCathedralNativeCharacterStart.Resolve(
            table, catalogs.Classes, catalogs.Animations, catalogs.Loadouts);

        Assert.True(start.HasCompiledTable);
        Assert.Equal(classId, start.ClassId);
        Assert.Equal(symbol, start.ClassSymbol);
        Assert.Equal(expectedLife, start.Hero.MaximumLifeBase);
        Assert.Equal(expectedMana, start.Hero.MaximumManaBase);
        Assert.Equal(10, start.Hero.LightRadius);
        Assert.Equal(animation.IdleFrames, start.Hero.IdleFrames);
        Assert.Equal((57, 40), (start.Hero.PreEntryFuture.X, start.Hero.PreEntryFuture.Y));
        Assert.True(start.Hero.LevelChanging);
        Assert.True(start.Hero.LevelLoading);
        if (classId == 0) {
            Assert.Equal(DiabloPlayerAbilityKind.Repair, player.AbilityKind);
            Assert.Empty(player.MemorizedSpells);
        } else {
            Assert.Equal(DiabloPlayerAbilityKind.Recharge, player.AbilityKind);
            Assert.Equal(new MemorizedSpellState(1, 2), Assert.Single(player.MemorizedSpells));
        }
    }

    [Fact]
    public void ClassStartRejectsUnsupportedClassSymbolsAndNonCanonicalFacts()
    {
        var catalogs = LoadCatalogs();
        var warrior = CreateTable(0, "warrior", 4480, 640, 10,
            catalogs.Animations.GetRequired(0).IdleFrames);

        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralNativeCharacterStart.Resolve(
            TsvTable.Parse("wrong-path.tsv", warrior.ToCanonicalTsv()),
            catalogs.Classes, catalogs.Animations, catalogs.Loadouts));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralNativeCharacterStart.Resolve(
            CreateTable(0, "rogue", 4480, 640, 10, catalogs.Animations.GetRequired(0).IdleFrames),
            catalogs.Classes, catalogs.Animations, catalogs.Loadouts));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralNativeCharacterStart.Resolve(
            CreateTable(0, "warrior", 4481, 640, 10, catalogs.Animations.GetRequired(0).IdleFrames),
            catalogs.Classes, catalogs.Animations, catalogs.Loadouts));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralNativeCharacterStart.Resolve(
            CreateTable(4, "bard", 3000, 1400, 10, 8),
            catalogs.Classes, catalogs.Animations, catalogs.Loadouts));
    }

    [Fact]
    public void ClassStartRequiresExactlyOneRowAndTheExactOrderedSchema()
    {
        var catalogs = LoadCatalogs();
        var canonical = CreateTable(2, "sorcerer", 1920, 4480, 10,
            catalogs.Animations.GetRequired(2).IdleFrames).ToCanonicalTsv();
        var duplicate = TsvTable.Parse(
            AuthoritativeCathedralNativeCharacterStart.TableName,
            canonical.TrimEnd('\r', '\n') + canonical[canonical.IndexOf('\n')..]);
        var reordered = TsvTable.Parse(
            AuthoritativeCathedralNativeCharacterStart.TableName,
            "class_symbol\tclass_id\thero_base_life_maximum\thero_base_mana_maximum\thero_light_radius\thero_idle_frames\n"
                + "sorcerer\t2\t1920\t4480\t10\t8\n");

        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralNativeCharacterStart.Resolve(
            duplicate, catalogs.Classes, catalogs.Animations, catalogs.Loadouts));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralNativeCharacterStart.Resolve(
            reordered, catalogs.Classes, catalogs.Animations, catalogs.Loadouts));
    }

    [Fact]
    public void AllClassTableResolvesTheSelectedClassRow()
    {
        var catalogs = LoadCatalogs();
        var table = TsvTable.Parse(AuthoritativeCathedralNativeCharacterStart.TableName,
            "class_id\tclass_symbol\thero_base_life_maximum\thero_base_mana_maximum\thero_light_radius\thero_idle_frames\n"
                + "0\twarrior\t4480\t640\t10\t10\n1\trogue\t2880\t1440\t10\t8\n2\tsorcerer\t1920\t4480\t10\t8\n");

        Assert.Equal(new uint[] { 0, 1, 2 }, AuthoritativeCathedralNativeCharacterStart.ClassIds(table));
        foreach (var (classId, symbol, life) in new[] { (0U, "warrior", 4480), (1U, "rogue", 2880), (2U, "sorcerer", 1920) }) {
            var start = AuthoritativeCathedralNativeCharacterStart.Resolve(
                table, catalogs.Classes, catalogs.Animations, catalogs.Loadouts, classId);
            Assert.True(start.HasCompiledTable);
            Assert.Equal((classId, symbol, life, 10), (start.ClassId, start.ClassSymbol, start.Hero.MaximumLifeBase, start.Hero.LightRadius));
        }
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralNativeCharacterStart.Resolve(
            table, catalogs.Classes, catalogs.Animations, catalogs.Loadouts));
        Assert.Throws<NotSupportedException>(() => AuthoritativeCathedralNativeCharacterStart.Resolve(
            table, catalogs.Classes, catalogs.Animations, catalogs.Loadouts, 3));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralNativeCharacterStart.Resolve(
            null, catalogs.Classes, catalogs.Animations, catalogs.Loadouts, 1));
    }

    private static TsvTable CreateTable(
        uint classId,
        string symbol,
        int life,
        int mana,
        int lightRadius,
        int idleFrames)
        => TsvTable.Parse(
            AuthoritativeCathedralNativeCharacterStart.TableName,
            "class_id\tclass_symbol\thero_base_life_maximum\thero_base_mana_maximum\thero_light_radius\thero_idle_frames\n"
                + $"{classId}\t{symbol}\t{life}\t{mana}\t{lightRadius}\t{idleFrames}\n");

    private static (
        AuthoritativePlayerClassCatalog Classes,
        AuthoritativePlayerAnimationCatalog Animations,
        AuthoritativePlayerLoadoutCatalog Loadouts) LoadCatalogs()
    {
        var root = FindRepositoryRoot();
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv", ReadBase(root, "content_ids.tsv"));
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv", ReadBase(root, "classes.tsv"), stableIds);
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv", ReadBase(root, "class_animations.tsv"), classes, stableIds);
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv", ReadBase(root, "items.tsv"));
        var combatRules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv", ReadBase(root, "combat.tsv"));
        var loadouts = AuthoritativePlayerLoadoutCatalog.LoadTsv(
            classes, items, combatRules,
            "class_loadouts.tsv", ReadBase(root, "class_loadouts.tsv"),
            "class_loadout_items.tsv", ReadBase(root, "class_loadout_items.tsv"), stableIds);
        return (classes, animations, loadouts);
    }

    private static string ReadBase(string root, string fileName)
        => File.ReadAllText(Path.Combine(root, "server", "content", "base", fileName));

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var roots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in roots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
