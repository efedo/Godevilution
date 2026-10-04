using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativePlayerAnimationContextCatalogTests
{
    [Fact]
    public void ResolvesExplicitDungeonExceptionsFromPreparedGraphics()
    {
        var source = CreateSource();
        var resolved = CreateCatalog(source).ResolveNative(
            0,
            1,
            DiabloPlayerAttackAnimationProfile.Bow,
            DiabloPlayerArmorGraphic.Heavy);

        Assert.False(resolved.IsTown);
        Assert.Equal(DiabloPlayerAttackAnimationProfile.Bow, resolved.WeaponProfile);
        Assert.Equal(DiabloPlayerArmorGraphic.Heavy, resolved.ArmorGraphic);
        Assert.Equal(8, resolved.StandFrames);
        Assert.Equal(15, resolved.DeathFrames);
        Assert.Equal(16, resolved.AttackTiming!.Frames);
    }

    [Fact]
    public void TownApplicationPreservesIncomingAttackHitAndBlockFields()
    {
        var source = CreateSource();
        var resolved = CreateCatalog(source).ResolveNative(
            0,
            0,
            DiabloPlayerAttackAnimationProfile.SwordShield,
            DiabloPlayerArmorGraphic.Heavy);
        var previous = new AuthoritativePlayerAnimationFrameSet(1, 2, 3, 4, 5, 6, 7, 8, 9);

        var after = resolved.ApplyTo(previous);

        Assert.Equal((resolved.StandFrames, resolved.WalkingFrames), (after.StandFrames, after.WalkingFrames));
        Assert.Equal((3, 4, 5, 6), (after.AttackFrames, after.AttackActionFrame, after.HitFrames, after.BlockingFrames));
        Assert.Equal((resolved.DeathFrames, resolved.CastingFrames, resolved.CastingActionFrame),
            (after.DeathFrames, after.CastingFrames, after.CastingActionFrame));
    }

    [Fact]
    public void UnknownClassesLevelsAndGraphicsFailClosed()
    {
        var source = CreateSource();
        var catalog = CreateCatalog(source);

        Assert.Throws<KeyNotFoundException>(() => catalog.ResolveNative(99, 1,
            DiabloPlayerAttackAnimationProfile.Bow, DiabloPlayerArmorGraphic.Light));
        Assert.Throws<KeyNotFoundException>(() => catalog.ResolveNative(0, 99,
            DiabloPlayerAttackAnimationProfile.Bow, DiabloPlayerArmorGraphic.Light));
        Assert.Throws<InvalidDataException>(() => catalog.ResolveNative(0, 1,
            (DiabloPlayerAttackAnimationProfile)99, DiabloPlayerArmorGraphic.Light));
        Assert.Throws<InvalidDataException>(() => catalog.ResolveNative(0, 1,
            DiabloPlayerAttackAnimationProfile.Bow, (DiabloPlayerArmorGraphic)1));
    }

    [Fact]
    public void SpellGraphicUsesExplicitNativeSchoolAndNotDamageType()
    {
        var source = CreateSource();
        var first = source.Spells.Definitions[0];
        var rows = source.SpellRows
            .Select(row => row.SpellId == first.SpellId
                ? row with { MagicType = DiabloPlayerAnimationMagicType.Lightning }
                : row)
            .ToArray();
        var catalog = CreateCatalog(source, spellRows: rows);

        Assert.Equal(DiabloPlayerAnimationMagicType.Lightning, catalog.ResolveSpell(first.SpellId));
        Assert.Equal(DiabloPlayerAnimationMagicType.Lightning, catalog.ResolveNativeSpell(first.NativeSpellId));
    }

    [Fact]
    public void SpellRowsRequireCompleteStableNativeBindings()
    {
        var source = CreateSource();
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source, spellRows: source.SpellRows.Skip(1)));

        var first = source.SpellRows[0];
        var mismatched = source.SpellRows
            .Select(row => row.SpellId == first.SpellId
                ? row with { NativeSpellId = first.NativeSpellId + 1000 }
                : row)
            .ToArray();
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source, spellRows: mismatched));

        var duplicate = source.SpellRows.Append(first).ToArray();
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source, spellRows: duplicate));
    }

    [Fact]
    public void FingerprintIsDetachedUppercaseAndInputSensitive()
    {
        var source = CreateSource();
        var first = CreateCatalog(source);
        var changedLevels = source.Levels
            .Select(row => row.LevelId == 1 ? row with { ContextIdentity = "dungeon-changed" } : row)
            .ToArray();
        var second = CreateCatalog(source, levelRows: changedLevels);

        Assert.Matches("^[0-9A-F]{64}$", first.Fingerprint);
        Assert.Equal(first.Fingerprint, CreateCatalog(source).Fingerprint);
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void NullAndZeroClassExceptionsAreDisabledWithoutClassNameInference()
    {
        var source = CreateSource();
        var disabled = source.Overrides.Select(row => row with {
            DungeonBowIdleFrames = 0,
            ArmoredDeathFrames = null,
            VariantIdentity = "",
        }).ToArray();
        var resolved = CreateCatalog(source, overrideRows: disabled).ResolveNative(
            0, 1, DiabloPlayerAttackAnimationProfile.Bow, DiabloPlayerArmorGraphic.Heavy);

        Assert.Equal(source.Animations.GetRequired(0).IdleFrames, resolved.StandFrames);
        Assert.Equal(source.Animations.GetRequired(0).DeathFrames, resolved.DeathFrames);
    }

    [Fact]
    public void NativeGraphicTimingUsesSourceRatesAndSignedWidthValidation()
    {
        var frames = new AuthoritativePlayerAnimationFrameSet(1, 2, 3, 4, 5, 6, 7, 8, 9);
        var expected = new[] {
            new AuthoritativePlayerAnimationTimingSample(1, 4),
            new AuthoritativePlayerAnimationTimingSample(2, 1),
            new AuthoritativePlayerAnimationTimingSample(3, 1),
            new AuthoritativePlayerAnimationTimingSample(5, 1),
            new AuthoritativePlayerAnimationTimingSample(8, 1),
            new AuthoritativePlayerAnimationTimingSample(8, 1),
            new AuthoritativePlayerAnimationTimingSample(8, 1),
            new AuthoritativePlayerAnimationTimingSample(7, 2),
            new AuthoritativePlayerAnimationTimingSample(6, 3),
        };
        for (var graphic = 0; graphic < expected.Length; graphic++)
            Assert.Equal(expected[graphic], AuthoritativePlayerAnimationContextCatalog.GetTiming(frames, graphic));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerAnimationContextCatalog.GetTiming(frames with { DeathFrames = 128 }, 7));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativePlayerAnimationContextCatalog.GetTiming(frames, 9));
    }

    [Fact]
    public void RowOrderDoesNotChangeFingerprint()
    {
        var source = CreateSource();
        var normal = CreateCatalog(source);
        var reordered = CreateCatalog(
            source,
            levelRows: source.Levels.Reverse(),
            overrideRows: source.Overrides.Reverse(),
            spellRows: source.SpellRows.Reverse());

        Assert.Equal(normal.Fingerprint, reordered.Fingerprint);
    }

    [Fact]
    public void MalformedTextIdentitiesFailClosed()
    {
        var source = CreateSource();
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source, levelRows: source.Levels.Select(row =>
            row.LevelId == 1 ? row with { ContextIdentity = "bad\0identity" } : row)));
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source, levelRows: source.Levels.Select(row =>
            row.LevelId == 1 ? row with { ContextIdentity = "bad\uD800" } : row)));
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source, overrideRows: source.Overrides.Select(row =>
            row with { VariantIdentity = "bad\nidentity" })));
    }

    [Fact]
    public void InputRowsAndGetterSnapshotsAreDetached()
    {
        var source = CreateSource();
        var levels = source.Levels.ToArray();
        var overrides = source.Overrides.ToArray();
        var spellRows = source.SpellRows.ToArray();
        var catalog = CreateCatalog(source, levelRows: levels, overrideRows: overrides, spellRows: spellRows);
        var fingerprint = catalog.Fingerprint;
        var firstSpell = source.SpellRows[0];
        var originalStableSchool = catalog.ResolveSpell(firstSpell.SpellId);
        var originalNativeSchool = catalog.ResolveNativeSpell(firstSpell.NativeSpellId);
        var differentSchool = originalStableSchool == DiabloPlayerAnimationMagicType.Fire
            ? DiabloPlayerAnimationMagicType.Lightning
            : DiabloPlayerAnimationMagicType.Fire;

        levels[0] = levels[0] with { ContextIdentity = "mutated-level" };
        overrides[0] = overrides[0] with { DungeonBowIdleFrames = 12 };
        spellRows[0] = spellRows[0] with { MagicType = differentSchool };
        ((AuthoritativePlayerAnimationLevelDefinition[])catalog.Levels)[0] =
            catalog.Levels[0] with { ContextIdentity = "mutated-export" };
        ((AuthoritativePlayerAnimationClassContextDefinition[])catalog.Overrides)[0] =
            catalog.Overrides[0] with { ArmoredDeathFrames = 12 };
        ((AuthoritativePlayerAnimationSpellDefinition[])catalog.SpellGraphics)[0] =
            catalog.SpellGraphics[0] with { MagicType = differentSchool };

        var resolved = catalog.ResolveNative(0, 1,
            DiabloPlayerAttackAnimationProfile.Bow, DiabloPlayerArmorGraphic.Heavy);
        Assert.Equal(fingerprint, catalog.Fingerprint);
        Assert.Equal(8, resolved.StandFrames);
        Assert.Equal(15, resolved.DeathFrames);
        Assert.Equal("town", catalog.Levels.Single(row => row.LevelId == 0).ContextIdentity);
        Assert.Equal("cathedral", catalog.Levels.Single(row => row.LevelId == 1).ContextIdentity);
        Assert.Equal(originalStableSchool, catalog.ResolveSpell(firstSpell.SpellId));
        Assert.Equal(originalNativeSchool, catalog.ResolveNativeSpell(firstSpell.NativeSpellId));
    }

    [Fact]
    public void DuplicateAndUnknownContextReferencesAndInvalidRowsFailClosed()
    {
        var source = CreateSource();
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source,
            levelRows: source.Levels.Append(source.Levels[0])));
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source,
            overrideRows: source.Overrides.Append(source.Overrides[0])));
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source,
            overrideRows: source.Overrides.Append(
                new AuthoritativePlayerAnimationClassContextDefinition(0, 99, null, null))));
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source,
            overrideRows: source.Overrides.Append(
                new AuthoritativePlayerAnimationClassContextDefinition(99, 1, null, null))));
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source,
            overrideRows: source.Overrides.Select(row => row with { DungeonBowIdleFrames = -1 })));
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source,
            overrideRows: source.Overrides.Select(row => row with { ArmoredDeathFrames = 128 })));
        Assert.Throws<InvalidDataException>(() => CreateCatalog(source,
            spellRows: source.SpellRows.Select(row => row with {
                MagicType = (DiabloPlayerAnimationMagicType)99,
            })));
    }

    [Fact]
    public void StableAndNativeSpellIdentitiesDoNotFallbackAcrossNamespaces()
    {
        var source = CreateSource();
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 1, 0, 0, 0, 0) { NativeSpellId = 50 },
            new AuthoritativeSpellDefinition(50, 1, 0, 0, 0, 0) { NativeSpellId = 1 },
        ]);
        var rows = new[] {
            new AuthoritativePlayerAnimationSpellDefinition(1, 50, DiabloPlayerAnimationMagicType.Fire),
            new AuthoritativePlayerAnimationSpellDefinition(50, 1, DiabloPlayerAnimationMagicType.Magic),
        };
        var catalog = CreateCatalog(source, spellCatalog: spells, spellRows: rows);

        Assert.Equal(DiabloPlayerAnimationMagicType.Fire, catalog.ResolveSpell(1));
        Assert.Equal(DiabloPlayerAnimationMagicType.Magic, catalog.ResolveSpell(50));
        Assert.Equal(DiabloPlayerAnimationMagicType.Magic, catalog.ResolveNativeSpell(1));
        Assert.Equal(DiabloPlayerAnimationMagicType.Fire, catalog.ResolveNativeSpell(50));
        Assert.Throws<KeyNotFoundException>(() => catalog.ResolveSpell(2));
        Assert.Throws<KeyNotFoundException>(() => catalog.ResolveNativeSpell(2));
    }

    private static SourceCatalog CreateSource()
    {
        var root = FindRepositoryRoot();
        var basePath = Path.Combine(root, "server", "content", "base");
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv", File.ReadAllText(Path.Combine(basePath, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv", File.ReadAllText(Path.Combine(basePath, "class_animations.tsv")), classes);
        var spells = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv", File.ReadAllText(Path.Combine(basePath, "spells.tsv")));
        var spellRows = spells.Definitions
            .Select((spell, index) => new AuthoritativePlayerAnimationSpellDefinition(
                spell.SpellId, spell.NativeSpellId, (DiabloPlayerAnimationMagicType)(index % 3)))
            .ToArray();
        return new SourceCatalog(
            classes,
            animations,
            spells,
            [
                new AuthoritativePlayerAnimationLevelDefinition(0, "town", true),
                new AuthoritativePlayerAnimationLevelDefinition(1, "cathedral", false),
            ],
            [
                new AuthoritativePlayerAnimationClassContextDefinition(0, 1, 8, 15, "warrior-dungeon"),
            ],
            spellRows);
    }

    private static AuthoritativePlayerAnimationContextCatalog CreateCatalog(
        SourceCatalog source,
        IEnumerable<AuthoritativePlayerAnimationLevelDefinition>? levelRows = null,
        IEnumerable<AuthoritativePlayerAnimationClassContextDefinition>? overrideRows = null,
        AuthoritativeSpellCatalog? spellCatalog = null,
        IEnumerable<AuthoritativePlayerAnimationSpellDefinition>? spellRows = null)
        => new(
            source.Animations,
            source.Classes,
            spellCatalog ?? source.Spells,
            levelRows ?? source.Levels,
            overrideRows ?? source.Overrides,
            spellRows ?? source.SpellRows);

    private static string FindRepositoryRoot()
    {
        var configured = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var starts = string.IsNullOrWhiteSpace(configured)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configured!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in starts) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed record SourceCatalog(
        AuthoritativePlayerClassCatalog Classes,
        AuthoritativePlayerAnimationCatalog Animations,
        AuthoritativeSpellCatalog Spells,
        IReadOnlyList<AuthoritativePlayerAnimationLevelDefinition> Levels,
        IReadOnlyList<AuthoritativePlayerAnimationClassContextDefinition> Overrides,
        IReadOnlyList<AuthoritativePlayerAnimationSpellDefinition> SpellRows);
}
