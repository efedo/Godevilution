using Devilution.Data;

namespace Devilution.Diablo;

/** Native player attack sprite profiles whose timing varies by class and equipment. */
public enum DiabloPlayerAttackAnimationProfile
{
    Unarmed,
    UnarmedShield,
    Sword,
    SwordShield,
    Bow,
    Axe,
    Mace,
    MaceShield,
    Staff,
}

/** Frame count plus the native one-based attack-effect frame. */
public sealed record AuthoritativePlayerAttackAnimationTiming(
    int Frames,
    int ActionFrame);

/** Complete native animation timing metadata for one stable player class. */
public sealed record AuthoritativePlayerAnimationDefinition(
    uint ClassId,
    AuthoritativePlayerAttackAnimationTiming Unarmed,
    AuthoritativePlayerAttackAnimationTiming UnarmedShield,
    AuthoritativePlayerAttackAnimationTiming Sword,
    AuthoritativePlayerAttackAnimationTiming SwordShield,
    AuthoritativePlayerAttackAnimationTiming Bow,
    AuthoritativePlayerAttackAnimationTiming Axe,
    AuthoritativePlayerAttackAnimationTiming Mace,
    AuthoritativePlayerAttackAnimationTiming MaceShield,
    AuthoritativePlayerAttackAnimationTiming Staff,
    int IdleFrames,
    int WalkingFrames,
    int BlockingFrames,
    int DeathFrames,
    int CastingFrames,
    int RecoveryFrames,
    int TownIdleFrames,
    int TownWalkingFrames,
    int CastingActionFrame)
{
    public AuthoritativePlayerAttackAnimationTiming GetAttack(
        DiabloPlayerAttackAnimationProfile profile)
    {
        return profile switch {
            DiabloPlayerAttackAnimationProfile.Unarmed => Unarmed,
            DiabloPlayerAttackAnimationProfile.UnarmedShield => UnarmedShield,
            DiabloPlayerAttackAnimationProfile.Sword => Sword,
            DiabloPlayerAttackAnimationProfile.SwordShield => SwordShield,
            DiabloPlayerAttackAnimationProfile.Bow => Bow,
            DiabloPlayerAttackAnimationProfile.Axe => Axe,
            DiabloPlayerAttackAnimationProfile.Mace => Mace,
            DiabloPlayerAttackAnimationProfile.MaceShield => MaceShield,
            DiabloPlayerAttackAnimationProfile.Staff => Staff,
            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };
    }
}

/** Validated class-keyed animation catalog extracted from native class tables. */
public sealed class AuthoritativePlayerAnimationCatalog
{
    private static readonly IReadOnlyList<string> RequiredColumns = [
        "class_id",
        "unarmed_frames",
        "unarmed_action_frame",
        "unarmed_shield_frames",
        "unarmed_shield_action_frame",
        "sword_frames",
        "sword_action_frame",
        "sword_shield_frames",
        "sword_shield_action_frame",
        "bow_frames",
        "bow_action_frame",
        "axe_frames",
        "axe_action_frame",
        "mace_frames",
        "mace_action_frame",
        "mace_shield_frames",
        "mace_shield_action_frame",
        "staff_frames",
        "staff_action_frame",
        "idle_frames",
        "walking_frames",
        "blocking_frames",
        "death_frames",
        "casting_frames",
        "recovery_frames",
        "town_idle_frames",
        "town_walking_frames",
        "casting_action_frame",
    ];

    private static readonly IReadOnlyList<string> SymbolicColumns = [
        "class_id",
        "class_symbol",
        .. RequiredColumns.Skip(1),
    ];

    private readonly Dictionary<uint, AuthoritativePlayerAnimationDefinition> byClassId;

    public AuthoritativePlayerAnimationCatalog(
        IEnumerable<AuthoritativePlayerAnimationDefinition> definitions,
        AuthoritativePlayerClassCatalog playerClasses)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(playerClasses);
        byClassId = new Dictionary<uint, AuthoritativePlayerAnimationDefinition>();
        foreach (var definition in definitions) {
            ArgumentNullException.ThrowIfNull(definition);
            Validate(definition);
            if (!byClassId.TryAdd(definition.ClassId, definition)) {
                throw new InvalidDataException(
                    $"Player animation class ID {definition.ClassId} is defined more than once.");
            }
        }

        var expectedIds = playerClasses.Definitions
            .Select(definition => definition.ClassId)
            .Order()
            .ToArray();
        var actualIds = byClassId.Keys.Order().ToArray();
        if (!actualIds.SequenceEqual(expectedIds)) {
            throw new InvalidDataException(
                "Player animation definitions must exactly match the activated player classes.");
        }
    }

    public IReadOnlyList<AuthoritativePlayerAnimationDefinition> Definitions
        => byClassId.Values.OrderBy(definition => definition.ClassId).ToArray();

    public bool TryGet(
        uint classId,
        out AuthoritativePlayerAnimationDefinition definition)
        => byClassId.TryGetValue(classId, out definition!);

    public AuthoritativePlayerAnimationDefinition GetRequired(uint classId)
    {
        return TryGet(classId, out var definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Player animation class ID {classId} is not defined.");
    }

    /**
     * Rejects numeric-only animation data at the current production boundary.
     * Direct callers may continue to load older compatibility shapes.
     */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(SymbolicColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production player-animation table '{table.SourcePath}' must use the complete ordered symbolic schema.");
        foreach (var row in table.Rows)
            foreach (var column in SymbolicColumns)
                _ = row.Required(column);
    }

    public static AuthoritativePlayerAnimationCatalog LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativePlayerClassCatalog playerClasses,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var hasSymbols = table.Columns.SequenceEqual(SymbolicColumns, StringComparer.Ordinal);
        if (!hasSymbols
            && !table.Columns.SequenceEqual(RequiredColumns, StringComparer.Ordinal)) {
            throw new InvalidDataException(
                $"Player animation table '{sourcePath}' does not match the required ordered schema.");
        }

        return new AuthoritativePlayerAnimationCatalog(
            table.Rows.Select(row => new AuthoritativePlayerAnimationDefinition(
                hasSymbols
                    ? AuthoritativeClassIdentity.ResolveOptional(
                        row,
                        stableIds,
                        "class_id",
                        "class_symbol")
                    : row.RequiredUInt32("class_id"),
                ReadAttack(row, "unarmed"),
                ReadAttack(row, "unarmed_shield"),
                ReadAttack(row, "sword"),
                ReadAttack(row, "sword_shield"),
                ReadAttack(row, "bow"),
                ReadAttack(row, "axe"),
                ReadAttack(row, "mace"),
                ReadAttack(row, "mace_shield"),
                ReadAttack(row, "staff"),
                row.RequiredInt32("idle_frames"),
                row.RequiredInt32("walking_frames"),
                row.RequiredInt32("blocking_frames"),
                row.RequiredInt32("death_frames"),
                row.RequiredInt32("casting_frames"),
                row.RequiredInt32("recovery_frames"),
                row.RequiredInt32("town_idle_frames"),
                row.RequiredInt32("town_walking_frames"),
                row.RequiredInt32("casting_action_frame"))),
            playerClasses);
    }

    private static AuthoritativePlayerAttackAnimationTiming ReadAttack(
        TsvRow row,
        string prefix)
    {
        return new AuthoritativePlayerAttackAnimationTiming(
            row.RequiredInt32($"{prefix}_frames"),
            row.RequiredInt32($"{prefix}_action_frame"));
    }

    private static void Validate(AuthoritativePlayerAnimationDefinition definition)
    {
        ValidateAttack(definition.ClassId, nameof(definition.Unarmed), definition.Unarmed);
        ValidateAttack(
            definition.ClassId,
            nameof(definition.UnarmedShield),
            definition.UnarmedShield);
        ValidateAttack(definition.ClassId, nameof(definition.Sword), definition.Sword);
        ValidateAttack(
            definition.ClassId,
            nameof(definition.SwordShield),
            definition.SwordShield);
        ValidateAttack(definition.ClassId, nameof(definition.Bow), definition.Bow);
        ValidateAttack(definition.ClassId, nameof(definition.Axe), definition.Axe);
        ValidateAttack(definition.ClassId, nameof(definition.Mace), definition.Mace);
        ValidateAttack(
            definition.ClassId,
            nameof(definition.MaceShield),
            definition.MaceShield);
        ValidateAttack(definition.ClassId, nameof(definition.Staff), definition.Staff);

        ValidateFrameCount(definition.ClassId, "idle", definition.IdleFrames);
        ValidateWalkFrameCount(definition.ClassId, "walking", definition.WalkingFrames);
        ValidateFrameCount(definition.ClassId, "blocking", definition.BlockingFrames);
        ValidateFrameCount(definition.ClassId, "death", definition.DeathFrames);
        ValidateFrameCount(definition.ClassId, "casting", definition.CastingFrames);
        ValidateFrameCount(definition.ClassId, "recovery", definition.RecoveryFrames);
        ValidateFrameCount(definition.ClassId, "town idle", definition.TownIdleFrames);
        ValidateWalkFrameCount(
            definition.ClassId,
            "town walking",
            definition.TownWalkingFrames);
        if (definition.BlockingFrames < 2
            || definition.CastingActionFrame < 0
            || definition.CastingActionFrame >= definition.CastingFrames) {
            throw new InvalidDataException(
                $"Player animation class {definition.ClassId} has invalid action timing.");
        }
    }

    private static void ValidateAttack(
        uint classId,
        string name,
        AuthoritativePlayerAttackAnimationTiming timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        ValidateFrameCount(classId, name, timing.Frames);
        if (timing.ActionFrame is < 1 || timing.ActionFrame > timing.Frames) {
            throw new InvalidDataException(
                $"Player animation class {classId} has an invalid {name} action frame.");
        }
    }

    private static void ValidateFrameCount(uint classId, string name, int frames)
    {
        if (frames is < 1 or > sbyte.MaxValue) {
            throw new InvalidDataException(
                $"Player animation class {classId} has an invalid {name} frame count.");
        }
    }

    private static void ValidateWalkFrameCount(uint classId, string name, int frames)
    {
        ValidateFrameCount(classId, name, frames);
        if (frames > 24) {
            throw new InvalidDataException(
                $"Player animation class {classId} exceeds the native {name} frame limit.");
        }
    }
}
