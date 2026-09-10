using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativePlayerAnimationCatalogTests
{
    [Fact]
    public void ShippedCatalogPreservesNativeClassAndEquipmentTimings()
    {
        var catalog = LoadShippedCatalog();

        Assert.Equal(6, catalog.Definitions.Count);
        var warrior = catalog.GetRequired(0);
        Assert.Equal(
            new AuthoritativePlayerAttackAnimationTiming(16, 9),
            warrior.GetAttack(DiabloPlayerAttackAnimationProfile.SwordShield));
        Assert.Equal((8, 2, 20, 6), (
            warrior.WalkingFrames,
            warrior.BlockingFrames,
            warrior.CastingFrames,
            warrior.RecoveryFrames));
        Assert.Equal(14, warrior.CastingActionFrame);

        var rogue = catalog.GetRequired(1);
        Assert.Equal(
            new AuthoritativePlayerAttackAnimationTiming(12, 7),
            rogue.GetAttack(DiabloPlayerAttackAnimationProfile.Bow));

        var sorcerer = catalog.GetRequired(2);
        Assert.Equal((12, 8), (sorcerer.CastingFrames, sorcerer.CastingActionFrame));

        var monk = catalog.GetRequired(3);
        Assert.Equal(
            new AuthoritativePlayerAttackAnimationTiming(13, 8),
            monk.GetAttack(DiabloPlayerAttackAnimationProfile.Staff));

        var bard = catalog.GetRequired(4);
        Assert.Equal(
            new AuthoritativePlayerAttackAnimationTiming(12, 11),
            bard.GetAttack(DiabloPlayerAttackAnimationProfile.Bow));

        var barbarian = catalog.GetRequired(5);
        Assert.Equal(
            new AuthoritativePlayerAttackAnimationTiming(20, 8),
            barbarian.GetAttack(DiabloPlayerAttackAnimationProfile.Axe));
    }

    [Fact]
    public void ShippedCatalogExactlyMirrorsEveryNativeAnimationTable()
    {
        var classes = LoadShippedClasses();
        var catalog = LoadShippedCatalog();

        foreach (var definition in catalog.Definitions) {
            var classSymbol = classes.GetRequired(definition.ClassId).Symbol;
            var nativeTimings = ReadNativeAnimations(classSymbol);
            var authoritativeTimings = new Dictionary<string, int> {
                ["unarmedFrames"] = definition.Unarmed.Frames,
                ["unarmedActionFrame"] = definition.Unarmed.ActionFrame,
                ["unarmedShieldFrames"] = definition.UnarmedShield.Frames,
                ["unarmedShieldActionFrame"] = definition.UnarmedShield.ActionFrame,
                ["swordFrames"] = definition.Sword.Frames,
                ["swordActionFrame"] = definition.Sword.ActionFrame,
                ["swordShieldFrames"] = definition.SwordShield.Frames,
                ["swordShieldActionFrame"] = definition.SwordShield.ActionFrame,
                ["bowFrames"] = definition.Bow.Frames,
                ["bowActionFrame"] = definition.Bow.ActionFrame,
                ["axeFrames"] = definition.Axe.Frames,
                ["axeActionFrame"] = definition.Axe.ActionFrame,
                ["maceFrames"] = definition.Mace.Frames,
                ["maceActionFrame"] = definition.Mace.ActionFrame,
                ["maceShieldFrames"] = definition.MaceShield.Frames,
                ["maceShieldActionFrame"] = definition.MaceShield.ActionFrame,
                ["staffFrames"] = definition.Staff.Frames,
                ["staffActionFrame"] = definition.Staff.ActionFrame,
                ["idleFrames"] = definition.IdleFrames,
                ["walkingFrames"] = definition.WalkingFrames,
                ["blockingFrames"] = definition.BlockingFrames,
                ["deathFrames"] = definition.DeathFrames,
                ["castingFrames"] = definition.CastingFrames,
                ["recoveryFrames"] = definition.RecoveryFrames,
                ["townIdleFrames"] = definition.TownIdleFrames,
                ["townWalkingFrames"] = definition.TownWalkingFrames,
                ["castingActionFrame"] = definition.CastingActionFrame,
            };

            Assert.Equal(nativeTimings, authoritativeTimings);
        }
    }

    [Fact]
    public void CatalogRequiresOneValidDefinitionForEveryActivatedClass()
    {
        var source = ReadShippedAnimations();
        var missingClass = string.Join(
            Environment.NewLine,
            source.Split(["\r\n", "\n"], StringSplitOptions.None)
                .Where(line => !line.StartsWith("5\t", StringComparison.Ordinal)));
        var duplicateClass = source.Replace(
            "1\trogue\t18\t10\t",
            "0\trogue\t18\t10\t",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Load(missingClass));
        Assert.Throws<InvalidDataException>(() => Load(duplicateClass));
    }

    [Fact]
    public void CatalogRejectsSchemaDriftAndInvalidNativeFrameRanges()
    {
        var source = ReadShippedAnimations();
        var changedSchema = source.Replace(
            "class_id\tclass_symbol\t",
            "id\tclass_symbol\t",
            StringComparison.Ordinal);
        var invalidAttackAction = source.Replace(
            "0\twarrior\t16\t9\t",
            "0\twarrior\t16\t17\t",
            StringComparison.Ordinal);
        var invalidWalkFrames = source.Replace(
            "\t10\t8\t2\t20\t20\t6\t",
            "\t10\t25\t2\t20\t20\t6\t",
            StringComparison.Ordinal);
        var invalidCastingAction = source.Replace(
            "\t20\t8\t14",
            "\t20\t8\t20",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Load(changedSchema));
        Assert.Throws<InvalidDataException>(() => Load(invalidAttackAction));
        Assert.Throws<InvalidDataException>(() => Load(invalidWalkFrames));
        Assert.Throws<InvalidDataException>(() => Load(invalidCastingAction));
    }

    [Fact]
    public void ProductionValidationRejectsNumericAnimationSchema()
    {
        var table = TsvTable.Parse(
            "class_animations.tsv",
            "class_id\tunarmed_frames\tunarmed_action_frame\n"
                + "0\t16\t9\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerAnimationCatalog.ValidateExternalizedProduction(table));

        Assert.Contains("complete ordered symbolic schema", exception.Message);
    }

    [Fact]
    public void ProductionValidationAcceptsShippedAnimationSchema()
    {
        var table = TsvTable.Parse("class_animations.tsv", ReadShippedAnimations());

        AuthoritativePlayerAnimationCatalog.ValidateExternalizedProduction(table);
    }

    private static AuthoritativePlayerAnimationCatalog LoadShippedCatalog()
    {
        return Load(ReadShippedAnimations());
    }

    private static AuthoritativePlayerAnimationCatalog Load(string animations)
    {
        return AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            animations,
            LoadShippedClasses());
    }

    private static AuthoritativePlayerClassCatalog LoadShippedClasses()
    {
        return AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(BaseContentRoot, "classes.tsv")));
    }

    private static IReadOnlyDictionary<string, int> ReadNativeAnimations(
        string classSymbol)
    {
        var sourcePath = Path.Combine(
            FindRepositoryRoot(),
            "assets",
            "txtdata",
            "classes",
            classSymbol,
            "animations.tsv");
        return File.ReadLines(sourcePath)
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split('\t'))
            .ToDictionary(
                fields => fields[0],
                fields => int.Parse(
                    fields[1],
                    System.Globalization.CultureInfo.InvariantCulture),
                StringComparer.Ordinal);
    }

    private static string ReadShippedAnimations()
    {
        return File.ReadAllText(Path.Combine(BaseContentRoot, "class_animations.tsv"));
    }

    private static string BaseContentRoot => Path.Combine(
        FindRepositoryRoot(),
        "server",
        "content",
        "base");

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
