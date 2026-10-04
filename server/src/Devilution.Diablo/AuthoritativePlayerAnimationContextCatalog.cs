using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Devilution.Diablo;

/** Native player armor graphic bits supplied by the already-resolved player graphic context. */
public enum DiabloPlayerArmorGraphic
{
    Light = 0,
    Medium = 16,
    Heavy = 32,
}

/** Native spell graphic school. Values mirror the native MagicType enum. */
public enum DiabloPlayerAnimationMagicType
{
    Fire = 0,
    Lightning = 1,
    Magic = 2,
}

/** Explicit level context; town is data and is never inferred from an ID. */
public sealed record AuthoritativePlayerAnimationLevelDefinition(
    uint LevelId,
    string ContextIdentity,
    bool IsTown);

/** Explicit class/level exceptions applied over the base animation table. */
public sealed record AuthoritativePlayerAnimationClassContextDefinition(
    uint ClassId,
    uint LevelId,
    int? DungeonBowIdleFrames,
    int? ArmoredDeathFrames,
    string VariantIdentity = "");

/** Native spell graphic metadata with distinct stable and native identities. */
public sealed record AuthoritativePlayerAnimationSpellDefinition(
    uint SpellId,
    uint NativeSpellId,
    DiabloPlayerAnimationMagicType MagicType);

/** The nine scalar frame fields written by Player::setAnimations. */
public sealed record AuthoritativePlayerAnimationFrameSet(
    int StandFrames,
    int WalkingFrames,
    int AttackFrames,
    int AttackActionFrame,
    int HitFrames,
    int BlockingFrames,
    int DeathFrames,
    int CastingFrames,
    int CastingActionFrame);

/** Native frame-count/tick-rate pair selected for one player_graphic value. */
public sealed record AuthoritativePlayerAnimationTimingSample(
    int Frames,
    int TicksPerFrame);

/**
 * Resolved prepared animation context. It contains no player state or item
 * admission logic. Town application intentionally preserves incoming attack,
 * hit, and block fields, matching native setAnimations.
 */
public sealed record AuthoritativePlayerAnimationContextResolution(
    AuthoritativePlayerAnimationLevelDefinition Level,
    AuthoritativePlayerAnimationDefinition BaseAnimation,
    AuthoritativePlayerAnimationClassContextDefinition? ContextOverride,
    DiabloPlayerAttackAnimationProfile WeaponProfile,
    AuthoritativePlayerAttackAnimationTiming? AttackTiming,
    DiabloPlayerArmorGraphic ArmorGraphic,
    int StandFrames,
    int WalkingFrames,
    int? HitFrames,
    int? BlockingFrames,
    int DeathFrames,
    int CastingFrames,
    int CastingActionFrame,
    string CatalogFingerprint)
{
    public bool IsTown => Level.IsTown;

    /** Applies native setAnimations writes to a prior frame set. */
    public AuthoritativePlayerAnimationFrameSet ApplyTo(
        AuthoritativePlayerAnimationFrameSet previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        AuthoritativePlayerAnimationContextCatalog.ValidateFrameSet(previous);
        var result = previous with {
            StandFrames = StandFrames,
            WalkingFrames = WalkingFrames,
            DeathFrames = DeathFrames,
            CastingFrames = CastingFrames,
            CastingActionFrame = CastingActionFrame,
        };
        if (!IsTown) {
            var attack = AttackTiming
                ?? throw new InvalidOperationException("Dungeon resolution is missing attack timing.");
            result = result with {
                AttackFrames = attack.Frames,
                AttackActionFrame = attack.ActionFrame,
                HitFrames = HitFrames
                    ?? throw new InvalidOperationException("Dungeon resolution is missing hit timing."),
                BlockingFrames = BlockingFrames
                    ?? throw new InvalidOperationException("Dungeon resolution is missing block timing."),
            };
        }
        return result;
    }
}

/**
 * Detached prepared-graphics context catalog. Equipment eligibility, player
 * lifecycle, RNG, and controller transitions are intentionally outside this
 * boundary; callers supply already-resolved native weapon and armor graphics.
 */
public sealed class AuthoritativePlayerAnimationContextCatalog
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly IReadOnlyDictionary<uint, AuthoritativePlayerAnimationLevelDefinition> levels;
    private readonly IReadOnlyDictionary<(uint ClassId, uint LevelId), AuthoritativePlayerAnimationClassContextDefinition> overrides;
    private readonly IReadOnlyDictionary<uint, AuthoritativePlayerAnimationDefinition> animations;
    private readonly IReadOnlyDictionary<uint, AuthoritativePlayerAnimationSpellDefinition> spellsByStableId;
    private readonly IReadOnlyDictionary<uint, AuthoritativePlayerAnimationSpellDefinition> spellsByNativeId;

    public AuthoritativePlayerAnimationContextCatalog(
        AuthoritativePlayerAnimationCatalog animationCatalog,
        AuthoritativePlayerClassCatalog playerClassCatalog,
        AuthoritativeSpellCatalog spellCatalog,
        IEnumerable<AuthoritativePlayerAnimationLevelDefinition> levels,
        IEnumerable<AuthoritativePlayerAnimationClassContextDefinition> overrides,
        IEnumerable<AuthoritativePlayerAnimationSpellDefinition> spellGraphics)
    {
        ArgumentNullException.ThrowIfNull(animationCatalog);
        ArgumentNullException.ThrowIfNull(playerClassCatalog);
        ArgumentNullException.ThrowIfNull(spellCatalog);
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(spellGraphics);

        var levelRows = levels.Select(CloneLevel).ToArray();
        if (levelRows.Length == 0)
            throw new InvalidDataException("Player animation context requires a level table.");
        this.levels = BuildLevels(levelRows);

        var animationRows = animationCatalog.Definitions.ToArray();
        var classRows = playerClassCatalog.Definitions.ToArray();
        animations = animationRows.ToDictionary(row => row.ClassId);
        var classIds = classRows.Select(row => row.ClassId).Order().ToArray();
        if (!classIds.SequenceEqual(animations.Keys.Order()))
            throw new InvalidDataException("Player class and animation catalogs have different class identities.");

        var overrideRows = overrides.Select(CloneOverride).ToArray();
        this.overrides = BuildOverrides(overrideRows, classIds, this.levels.Keys);

        var spellRows = spellGraphics.Select(CloneSpell).ToArray();
        var spellCatalogRows = spellCatalog.Definitions.ToArray();
        (spellsByStableId, spellsByNativeId) = BuildSpells(spellRows, spellCatalogRows);

        Fingerprint = ComputeFingerprint(
            animationRows,
            classRows,
            spellCatalogRows,
            levelRows,
            overrideRows,
            spellRows);
    }

    public IReadOnlyList<AuthoritativePlayerAnimationLevelDefinition> Levels
        => levels.Values.OrderBy(row => row.LevelId).ToArray();

    public IReadOnlyList<AuthoritativePlayerAnimationClassContextDefinition> Overrides
        => overrides.Values
            .OrderBy(row => row.ClassId)
            .ThenBy(row => row.LevelId)
            .ToArray();

    public IReadOnlyList<AuthoritativePlayerAnimationSpellDefinition> SpellGraphics
        => spellsByStableId.Values.OrderBy(row => row.SpellId).ToArray();

    /** Uppercase SHA-256 over the detached, order-independent input metadata. */
    public string Fingerprint { get; }

    /** Resolves a spell by stable content identity only. */
    public DiabloPlayerAnimationMagicType ResolveSpell(uint spellId)
        => spellsByStableId.TryGetValue(spellId, out var row)
            ? row.MagicType
            : throw new KeyNotFoundException($"Player animation spell {spellId} is not defined.");

    /** Resolves a spell by native SpellID only. */
    public DiabloPlayerAnimationMagicType ResolveNativeSpell(uint nativeSpellId)
        => spellsByNativeId.TryGetValue(nativeSpellId, out var row)
            ? row.MagicType
            : throw new KeyNotFoundException(
                $"Native player animation spell {nativeSpellId} is not defined.");

    /** Resolves a prepared native graphic context without consuming RNG. */
    public AuthoritativePlayerAnimationContextResolution ResolveNative(
        uint classId,
        uint levelId,
        DiabloPlayerAttackAnimationProfile weaponProfile,
        DiabloPlayerArmorGraphic armorGraphic)
    {
        if (!Enum.IsDefined(weaponProfile))
            throw new InvalidDataException("Player animation weapon profile is invalid.");
        if (!Enum.IsDefined(armorGraphic))
            throw new InvalidDataException("Player animation armor graphic is invalid.");

        var level = GetLevel(levelId);
        if (!animations.TryGetValue(classId, out var animation))
            throw new KeyNotFoundException($"Player animation class {classId} is not defined.");
        var context = overrides.GetValueOrDefault((classId, levelId));
        var idleFrames = level.IsTown ? animation.TownIdleFrames : animation.IdleFrames;
        if (!level.IsTown
            && weaponProfile == DiabloPlayerAttackAnimationProfile.Bow
            && context?.DungeonBowIdleFrames is > 0) {
            idleFrames = context.DungeonBowIdleFrames.Value;
        }

        var deathFrames = context?.ArmoredDeathFrames is > 0
            && armorGraphic != DiabloPlayerArmorGraphic.Light
            ? context.ArmoredDeathFrames.Value
            : animation.DeathFrames;
        var attack = level.IsTown
            ? (AuthoritativePlayerAttackAnimationTiming?)null
            : animation.GetAttack(weaponProfile);
        return new AuthoritativePlayerAnimationContextResolution(
            level,
            animation,
            context,
            weaponProfile,
            attack,
            armorGraphic,
            idleFrames,
            level.IsTown ? animation.TownWalkingFrames : animation.WalkingFrames,
            level.IsTown ? null : animation.RecoveryFrames,
            level.IsTown ? null : animation.BlockingFrames,
            deathFrames,
            animation.CastingFrames,
            animation.CastingActionFrame,
            Fingerprint);
    }

    /** Returns native frame count/rate for player_graphic 0..8. */
    public static AuthoritativePlayerAnimationTimingSample GetTiming(
        AuthoritativePlayerAnimationFrameSet frameSet,
        int nativeGraphic)
    {
        ArgumentNullException.ThrowIfNull(frameSet);
        ValidateFrameSet(frameSet);
        if (nativeGraphic is < 0 or > 8)
            throw new InvalidDataException("Native player graphic is outside player_graphic.");
        return nativeGraphic switch {
            0 => new(frameSet.StandFrames, 4),
            1 => new(frameSet.WalkingFrames, 1),
            2 => new(frameSet.AttackFrames, 1),
            3 => new(frameSet.HitFrames, 1),
            4 or 5 or 6 => new(frameSet.CastingFrames, 1),
            7 => new(frameSet.DeathFrames, 2),
            8 => new(frameSet.BlockingFrames, 3),
            _ => throw new InvalidDataException("Player graphic is outside player_graphic."),
        };
    }

    internal static void ValidateFrameSet(AuthoritativePlayerAnimationFrameSet frameSet)
    {
        foreach (var value in new[] {
                     frameSet.StandFrames,
                     frameSet.WalkingFrames,
                     frameSet.AttackFrames,
                     frameSet.AttackActionFrame,
                     frameSet.HitFrames,
                     frameSet.BlockingFrames,
                     frameSet.DeathFrames,
                     frameSet.CastingFrames,
                     frameSet.CastingActionFrame,
                 }) {
            if (value is < sbyte.MinValue or > sbyte.MaxValue)
                throw new InvalidDataException("Player animation scalar is outside native int8.");
        }
    }

    private AuthoritativePlayerAnimationLevelDefinition GetLevel(uint levelId)
        => levels.TryGetValue(levelId, out var level)
            ? level
            : throw new KeyNotFoundException($"Player animation level {levelId} is not defined.");

    private static IReadOnlyDictionary<uint, AuthoritativePlayerAnimationLevelDefinition> BuildLevels(
        IReadOnlyList<AuthoritativePlayerAnimationLevelDefinition> rows)
    {
        var result = new Dictionary<uint, AuthoritativePlayerAnimationLevelDefinition>();
        foreach (var row in rows) {
            ValidateText(row.ContextIdentity, nameof(row.ContextIdentity));
            if (!result.TryAdd(row.LevelId, row))
                throw new InvalidDataException($"Player animation level {row.LevelId} is duplicated.");
        }
        return result;
    }

    private static IReadOnlyDictionary<(uint ClassId, uint LevelId), AuthoritativePlayerAnimationClassContextDefinition> BuildOverrides(
        IReadOnlyList<AuthoritativePlayerAnimationClassContextDefinition> rows,
        IEnumerable<uint> classIds,
        IEnumerable<uint> levelIds)
    {
        var knownClasses = classIds.ToHashSet();
        var knownLevels = levelIds.ToHashSet();
        var result = new Dictionary<(uint, uint), AuthoritativePlayerAnimationClassContextDefinition>();
        foreach (var row in rows) {
            if (!knownClasses.Contains(row.ClassId) || !knownLevels.Contains(row.LevelId))
                throw new InvalidDataException("Player animation override references an unknown class or level.");
            ValidateOptionalFrame(row.DungeonBowIdleFrames, nameof(row.DungeonBowIdleFrames));
            ValidateOptionalFrame(row.ArmoredDeathFrames, nameof(row.ArmoredDeathFrames));
            ValidateText(row.VariantIdentity, nameof(row.VariantIdentity));
            if (!result.TryAdd((row.ClassId, row.LevelId), row))
                throw new InvalidDataException("Player animation override is duplicated.");
        }
        return result;
    }

    private static (IReadOnlyDictionary<uint, AuthoritativePlayerAnimationSpellDefinition> Stable,
        IReadOnlyDictionary<uint, AuthoritativePlayerAnimationSpellDefinition> Native) BuildSpells(
        IReadOnlyList<AuthoritativePlayerAnimationSpellDefinition> rows,
        IReadOnlyList<AuthoritativeSpellDefinition> source)
    {
        var expected = source.ToDictionary(row => row.SpellId);
        var stable = new Dictionary<uint, AuthoritativePlayerAnimationSpellDefinition>();
        var native = new Dictionary<uint, AuthoritativePlayerAnimationSpellDefinition>();
        foreach (var row in rows) {
            if (!Enum.IsDefined(row.MagicType))
                throw new InvalidDataException("Player animation spell graphic school is invalid.");
            if (!expected.TryGetValue(row.SpellId, out var sourceRow)
                || sourceRow.NativeSpellId != row.NativeSpellId)
                throw new InvalidDataException("Player animation spell row does not match the spell catalog.");
            if (!stable.TryAdd(row.SpellId, row))
                throw new InvalidDataException("Player animation spell stable identity is duplicated.");
            if (!native.TryAdd(row.NativeSpellId, row))
                throw new InvalidDataException("Player animation spell native identity is duplicated.");
        }
        if (stable.Count != expected.Count)
            throw new InvalidDataException("Every supplied spell requires one animation graphic row.");
        return (stable, native);
    }

    private static AuthoritativePlayerAnimationLevelDefinition CloneLevel(
        AuthoritativePlayerAnimationLevelDefinition row)
        => row with { ContextIdentity = row.ContextIdentity };

    private static AuthoritativePlayerAnimationClassContextDefinition CloneOverride(
        AuthoritativePlayerAnimationClassContextDefinition row)
        => row with { VariantIdentity = row.VariantIdentity };

    private static AuthoritativePlayerAnimationSpellDefinition CloneSpell(
        AuthoritativePlayerAnimationSpellDefinition row)
        => row with { };

    private static void ValidateOptionalFrame(int? value, string name)
    {
        if (value is < 0 or > sbyte.MaxValue)
            throw new InvalidDataException($"{name} is outside native int8.");
    }

    private static void ValidateText(string value, string name)
    {
        if (value is null || value.Contains('\0') || value.Contains('\r') || value.Contains('\n'))
            throw new InvalidDataException($"{name} contains invalid text.");
        if (name == nameof(AuthoritativePlayerAnimationLevelDefinition.ContextIdentity)
            && string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{name} must not be blank.");
        try {
            _ = StrictUtf8.GetBytes(value);
        } catch (EncoderFallbackException exception) {
            throw new InvalidDataException($"{name} is not valid UTF-8.", exception);
        }
    }

    private static string ComputeFingerprint(
        IReadOnlyList<AuthoritativePlayerAnimationDefinition> animationRows,
        IReadOnlyList<AuthoritativePlayerClassDefinition> classRows,
        IReadOnlyList<AuthoritativeSpellDefinition> spellCatalogRows,
        IReadOnlyList<AuthoritativePlayerAnimationLevelDefinition> levelRows,
        IReadOnlyList<AuthoritativePlayerAnimationClassContextDefinition> overrideRows,
        IReadOnlyList<AuthoritativePlayerAnimationSpellDefinition> spellRows)
    {
        var canonical = new StringBuilder("player-animation-context-v1\n");
        foreach (var row in classRows.OrderBy(row => row.ClassId)) {
            Add(canonical, "class.id", row.ClassId);
            Add(canonical, "class.symbol", row.Symbol);
        }
        foreach (var row in animationRows.OrderBy(row => row.ClassId))
            AppendAnimation(canonical, row);
        foreach (var row in levelRows.OrderBy(row => row.LevelId)) {
            Add(canonical, "level.id", row.LevelId);
            Add(canonical, "level.context", row.ContextIdentity);
            Add(canonical, "level.town", row.IsTown);
        }
        foreach (var row in overrideRows.OrderBy(row => (row.ClassId, row.LevelId))) {
            Add(canonical, "override.class", row.ClassId);
            Add(canonical, "override.level", row.LevelId);
            Add(canonical, "override.bow", row.DungeonBowIdleFrames);
            Add(canonical, "override.death", row.ArmoredDeathFrames);
            Add(canonical, "override.variant", row.VariantIdentity);
        }
        foreach (var spell in spellCatalogRows.OrderBy(row => row.SpellId)) {
            Add(canonical, "spell.id", spell.SpellId);
            Add(canonical, "spell.native", spell.NativeSpellId);
        }
        foreach (var row in spellRows.OrderBy(row => (row.SpellId, row.NativeSpellId))) {
            Add(canonical, "graphic.spell", row.SpellId);
            Add(canonical, "graphic.native", row.NativeSpellId);
            Add(canonical, "graphic.magic", (int)row.MagicType);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void AppendAnimation(StringBuilder output, AuthoritativePlayerAnimationDefinition row)
    {
        Add(output, "animation.class", row.ClassId);
        AppendAttack(output, "unarmed", row.Unarmed);
        AppendAttack(output, "unarmed-shield", row.UnarmedShield);
        AppendAttack(output, "sword", row.Sword);
        AppendAttack(output, "sword-shield", row.SwordShield);
        AppendAttack(output, "bow", row.Bow);
        AppendAttack(output, "axe", row.Axe);
        AppendAttack(output, "mace", row.Mace);
        AppendAttack(output, "mace-shield", row.MaceShield);
        AppendAttack(output, "staff", row.Staff);
        Add(output, "animation.idle", row.IdleFrames);
        Add(output, "animation.walk", row.WalkingFrames);
        Add(output, "animation.block", row.BlockingFrames);
        Add(output, "animation.death", row.DeathFrames);
        Add(output, "animation.cast", row.CastingFrames);
        Add(output, "animation.recovery", row.RecoveryFrames);
        Add(output, "animation.town-idle", row.TownIdleFrames);
        Add(output, "animation.town-walk", row.TownWalkingFrames);
        Add(output, "animation.cast-action", row.CastingActionFrame);
    }

    private static void AppendAttack(StringBuilder output, string name, AuthoritativePlayerAttackAnimationTiming timing)
    {
        Add(output, $"animation.{name}.frames", timing.Frames);
        Add(output, $"animation.{name}.action", timing.ActionFrame);
    }

    private static void Add(StringBuilder output, string name, object? value)
    {
        output.Append(name).Append('=');
        if (value is null)
            output.Append("null\n");
        else if (value is string text) {
            try {
                output.Append(Convert.ToBase64String(StrictUtf8.GetBytes(text))).Append('\n');
            } catch (EncoderFallbackException exception) {
                throw new InvalidDataException($"Fingerprint text {name} is not valid UTF-8.", exception);
            }
        }
        else if (value is bool boolean)
            output.Append(boolean ? '1' : '0').Append('\n');
        else if (value is IFormattable formattable)
            output.Append(formattable.ToString(null, CultureInfo.InvariantCulture)).Append('\n');
        else
            throw new InvalidDataException($"Unsupported fingerprint value type {value.GetType().FullName}.");
    }
}
