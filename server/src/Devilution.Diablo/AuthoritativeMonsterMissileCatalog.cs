using Devilution.Data;

namespace Devilution.Diablo;

/** Native ranged-AI branch selected by a validated monster missile mapping. */
public enum AuthoritativeMonsterRangedAttackMode
{
    Ranged,
    RangedAvoidance,
}

/** Validated native AI-to-missile mappings supplied by the active content pack. */
public sealed record AuthoritativeMonsterMissileDefinition(
    string NativeAi,
    string MissileId,
    AuthoritativeProjectileBehavior ProjectileBehavior,
    AuthoritativeMonsterRangedAttackMode AttackMode = AuthoritativeMonsterRangedAttackMode.Ranged,
    int AvoidanceProbabilityPercent = 100,
    uint MonsterId = 0,
    uint SpellId = 0,
    uint SpawnMonsterId = 0,
    int ProjectileSpeed = 0,
    int ProjectileLifetime = 0,
    int ProjectileLifetimeBase = 0,
    int ProjectileLifetimeIntelligenceScale = 0,
    int PuddleDurationRandomRange = 0,
    int PuddleDurationBase = 0,
    int PuddleDurationIntelligenceScale = 0,
    int PuddleDamageMinimumMonsterLevel = 0,
    int PuddleDamageLow = 0,
    int PuddleDamageHigh = 0,
    AuthoritativeDamageType? DamageType = null,
    int ChargeMinimumDistance = 0,
    int ChargeChanceBase = 0,
    int ChargeChanceIntelligenceScale = 0);

/** Externalized native monster missile identities used by server-owned AI. */
public sealed class AuthoritativeMonsterMissileCatalog
{
    private readonly Dictionary<string, AuthoritativeMonsterMissileDefinition> definitions;

    public AuthoritativeMonsterMissileCatalog(
        IEnumerable<AuthoritativeMonsterMissileDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.definitions = new Dictionary<string, AuthoritativeMonsterMissileDefinition>(
            StringComparer.Ordinal);
        foreach (var definition in definitions) {
            if (!AuthoritativeMonsterTypeCatalog.IsValidNativeAi(definition.NativeAi)
                || string.IsNullOrWhiteSpace(definition.MissileId)
                || !Enum.IsDefined(definition.ProjectileBehavior)
                || !Enum.IsDefined(definition.AttackMode)
                || definition.DamageType is not null
                    && !Enum.IsDefined(definition.DamageType.Value)
                || definition.AvoidanceProbabilityPercent is < 1 or > 100
                || definition.ProjectileSpeed < 0
                || definition.ProjectileLifetime < 0
                || definition.ProjectileLifetimeBase < 0
                || definition.ProjectileLifetimeIntelligenceScale < 0
                || definition.PuddleDurationRandomRange < 0
                || definition.PuddleDurationBase < 0
                || definition.PuddleDurationIntelligenceScale < 0
                || definition.PuddleDamageMinimumMonsterLevel < 0
                || definition.PuddleDamageLow < 0
                || definition.PuddleDamageHigh < definition.PuddleDamageLow
                || definition.ChargeMinimumDistance is < 0 or > 64
                || definition.ChargeChanceBase is < 0 or > 100
                || definition.ChargeChanceIntelligenceScale is < 0 or > 100
                || (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.BatCharge
                    && ((definition.ChargeMinimumDistance != 0
                            || definition.ChargeChanceBase != 0
                            || definition.ChargeChanceIntelligenceScale != 0)
                        && (definition.ChargeMinimumDistance <= 0
                            || definition.ChargeChanceBase <= 0
                            || definition.ChargeChanceIntelligenceScale <= 0)))
                || (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.SnakeCharge
                    && (definition.ChargeChanceBase != 0
                        || definition.ChargeChanceIntelligenceScale != 0))
                || (definition.ProjectileBehavior is not (
                        AuthoritativeProjectileBehavior.BatCharge
                        or AuthoritativeProjectileBehavior.SnakeCharge)
                    && (definition.ChargeMinimumDistance != 0
                        || definition.ChargeChanceBase != 0
                        || definition.ChargeChanceIntelligenceScale != 0))
                || !this.definitions.TryAdd(
                    CreateKey(definition.NativeAi, definition.MonsterId),
                    definition)) {
                throw new InvalidDataException(
                    "Monster missile definitions contain invalid or duplicate identities.");
            }
        }
        if (this.definitions.Count == 0)
            throw new InvalidDataException("The monster missile catalog cannot be empty.");
    }

    public IReadOnlyList<AuthoritativeMonsterMissileDefinition> Definitions
        => definitions.Values
            .OrderBy(definition => definition.NativeAi)
            .ThenBy(definition => definition.MonsterId)
            .ToArray();

    public bool TryGet(
        string nativeAi,
        out AuthoritativeMonsterMissileDefinition definition)
        => definitions.TryGetValue(CreateKey(nativeAi, 0), out definition!);

    /** Resolves a monster-specific row first, then its native-AI wildcard. */
    public bool TryGet(
        string nativeAi,
        uint monsterId,
        out AuthoritativeMonsterMissileDefinition definition)
        => definitions.TryGetValue(CreateKey(nativeAi, monsterId), out definition!)
            || definitions.TryGetValue(CreateKey(nativeAi, 0), out definition!);

    public static AuthoritativeMonsterMissileCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeMonsterMissileCatalog(table.Rows.Select(row => {
            var behaviorText = row.Required("projectile_behavior");
            if (!Enum.TryParse<AuthoritativeProjectileBehavior>(
                    behaviorText,
                    ignoreCase: false,
                    out var behavior)
                || !Enum.IsDefined(behavior)) {
                throw new InvalidDataException(
                    $"Monster missile row {row.LineNumber} contains unknown projectile behavior "
                    + $"'{behaviorText}'.");
            }
            return new AuthoritativeMonsterMissileDefinition(
                row.Required("native_ai"),
                row.Required("missile_id"),
                behavior,
                ParseAttackMode(row),
                row.OptionalInt32("avoidance_probability_percent", 100),
                row.OptionalUInt32("monster_id"),
                row.OptionalUInt32("spell_id"),
                row.OptionalUInt32("spawn_monster_id"),
                row.OptionalInt32("projectile_speed"),
                row.OptionalInt32("projectile_lifetime"),
                row.OptionalInt32("projectile_lifetime_base"),
                row.OptionalInt32("projectile_lifetime_intelligence_scale"),
                row.OptionalInt32("puddle_duration_random_range"),
                row.OptionalInt32("puddle_duration_base"),
                row.OptionalInt32("puddle_duration_intelligence_scale"),
                row.OptionalInt32("puddle_damage_minimum_monster_level"),
                row.OptionalInt32("puddle_damage_low"),
                row.OptionalInt32("puddle_damage_high"),
                ParseDamageType(row),
                row.OptionalInt32("charge_minimum_distance"),
                row.OptionalInt32("charge_chance_base"),
                row.OptionalInt32("charge_chance_intelligence_scale"));
        }));
    }

    private static string CreateKey(string nativeAi, uint monsterId)
        => $"{nativeAi}\u001F{monsterId}";

    private static AuthoritativeDamageType? ParseDamageType(TsvRow row)
    {
        if (!row.TryGet("damage_type", out var text)
            || string.IsNullOrWhiteSpace(text))
            return null;
        if (!Enum.TryParse<AuthoritativeDamageType>(
                text,
                ignoreCase: false,
                out var damageType)
            || !Enum.IsDefined(damageType)) {
            throw new InvalidDataException(
                $"Monster missile row {row.LineNumber} contains unknown damage type '{text}'.");
        }
        return damageType;
    }

    private static AuthoritativeMonsterRangedAttackMode ParseAttackMode(TsvRow row)
    {
        if (!row.TryGet("attack_mode", out var text)
            || string.IsNullOrWhiteSpace(text))
            return AuthoritativeMonsterRangedAttackMode.Ranged;
        if (!Enum.TryParse<AuthoritativeMonsterRangedAttackMode>(
                text,
                ignoreCase: false,
                out var mode)
            || !Enum.IsDefined(mode)) {
            throw new InvalidDataException(
                $"Monster missile row {row.LineNumber} contains unknown attack mode '{text}'.");
        }
        return mode;
    }
}
