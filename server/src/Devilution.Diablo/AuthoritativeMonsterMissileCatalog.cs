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
    int ChargeChanceIntelligenceScale = 0,
    int RangedAvoidanceMinimumDistance = 0,
    int RangedAvoidanceFarDistance = 0,
    int RangedAvoidanceChanceBase = 0,
    int RangedAvoidanceChanceIntelligenceScale = 0,
    int RangedAvoidanceFarIntelligenceOffset = 0,
    int RangedRetreatMinimumDistance = 0,
    int RangedRetreatChanceBase = 0,
    int RangedRetreatChanceIntelligenceScale = 0,
    int InfernoMaximumDistance = 0,
    int InfernoAdjacentChanceBase = 0,
    int InfernoAdjacentChanceIntelligenceScale = 0,
    int InfernoRangedFarDistance = 0,
    int InfernoRangedChanceBase = 0,
    int InfernoRangedChanceIntelligenceScale = 0,
    int InfernoRangedFarIntelligenceOffset = 0,
    int InfernoAdjacentChoiceModulo = 0,
    /** Optional fixed impact damage; zero preserves behavior-specific/native damage derivation. */
    int ProjectileDamage = 0,
    /** Optional Acid puddle impact lifetime; zero preserves the native two-tick phase. */
    int PuddleImpactLifetime = 0,
    /** Optional mapped projectile impact lifetime; zero preserves the native one-tick phase. */
    int ProjectileImpactLifetime = 0,
    /** Optional SnakeAi lateral drift rotation sequence; empty preserves the native sequence. */
    IReadOnlyList<int>? SnakeDriftPattern = null,
    /** Optional post-shot Ranged delay base; zero preserves the native base. */
    int RangedDelayBase = 0,
    /** Optional exclusive post-shot Ranged delay random range. */
    int RangedDelayRandomRange = 0,
    /** Optional RangedAvoidance recovery-delay base; zero preserves native behavior. */
    int RangedAvoidanceDelayBase = 0,
    /** Optional exclusive RangedAvoidance recovery-delay random range. */
    int RangedAvoidanceDelayRandomRange = 0,
    /** Optional HorkDemon recovery-delay base; zero preserves native behavior. */
    int HorkDemonDelayBase = 0,
    /** Optional exclusive HorkDemon recovery-delay random range. */
    int HorkDemonDelayRandomRange = 0,
    /** Optional Snake adjacent-delay base before intelligence reduction. */
    int SnakeAdjacentDelayBase = 0,
    /** Optional Snake adjacent-delay intelligence reduction. */
    int SnakeAdjacentDelayIntelligenceScale = 0,
    /** Optional exclusive Snake adjacent-delay random range. */
    int SnakeAdjacentDelayRandomRange = 0,
    /** Optional Snake distant-delay base before intelligence reduction. */
    int SnakeDistantDelayBase = 0,
    /** Optional Snake distant-delay intelligence reduction. */
    int SnakeDistantDelayIntelligenceScale = 0,
    /** Optional exclusive Snake distant-delay random range. */
    int SnakeDistantDelayRandomRange = 0,
    /** Optional Snake distant-delay admission chance base. */
    int SnakeDistantDelayChanceBase = 0,
    /** Optional Snake distant-delay admission chance reduction per intelligence. */
    int SnakeDistantDelayChanceIntelligenceScale = 0,
    /** Optional Snake adjacent attack admission chance base. */
    int SnakeAdjacentAttackChanceBase = 0,
    /** Optional Snake adjacent attack admission chance intelligence scale. */
    int SnakeAdjacentAttackChanceIntelligenceScale = 0,
    /** Optional Mega post-action delay base; zero preserves native behavior. */
    int MegaDelayBase = 0,
    /** Optional exclusive Mega post-action delay random range. */
    int MegaDelayRandomRange = 0,
    /** Optional exclusive upper bound for native GenerateRnd damage rolls. */
    int ProjectileDamageRandomRange = 0,
    /** Optional AiRangedAvoidance adjacent attack chance base per 10,000. */
    int RangedAvoidanceAdjacentAttackChanceBase = 0,
    /** Optional AiRangedAvoidance adjacent attack chance per intelligence per 10,000. */
    int RangedAvoidanceAdjacentAttackChanceIntelligenceScale = 0,
    /** Optional divisor for the AiRangedAvoidance adjacent attack chance band. */
    int RangedAvoidanceAdjacentAttackChanceDivisor = 0,
    /** Optional explicit post-melee projectile controller identity. */
    bool IsPostMeleeMissile = false)
{
    /** Optional AiRangedAvoidance far-distance movement-goal admission modulo. */
    public int RangedAvoidanceMovementChanceModulo { get; init; }

    /** Optional AiRangedAvoidance ordinary movement chance base. */
    public int RangedAvoidanceMovementChanceBase { get; init; }

    /** Optional AiRangedAvoidance ordinary movement intelligence scale. */
    public int RangedAvoidanceMovementChanceIntelligenceScale { get; init; }

    /** Optional AiRangedAvoidance fresh-mode movement chance bonus. */
    public int RangedAvoidanceMovementFreshChanceBonus { get; init; }

    /** Optional native initial travel advance in velocity quanta. */
    public int InitialTravelQuanta { get; init; }

    /** Optional number of projectiles emitted by one native missile admission. */
    public int ProjectileCount { get; init; }

    /**
     * Optional replay-compatibility boundary that suppresses impact on
     * non-primary participants for this Snake charge mapping.
     */
    public bool SuppressNonPrimaryPlayerImpact { get; init; }
}

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
                || definition.InitialTravelQuanta is < 0 or > 16
                || definition.ProjectileCount is < 0 or > 32
                || definition.ProjectileLifetime < 0
                || definition.ProjectileLifetimeBase < 0
                || definition.ProjectileLifetimeIntelligenceScale < 0
                || definition.ProjectileDamage < 0
                || definition.ProjectileDamageRandomRange < 0
                || definition.RangedAvoidanceAdjacentAttackChanceBase is < 0 or > 10_000
                || definition.RangedAvoidanceAdjacentAttackChanceIntelligenceScale is < 0 or > 10_000
                || definition.RangedAvoidanceAdjacentAttackChanceDivisor is < 0 or > 100
                || definition.IsPostMeleeMissile
                    && (definition.MonsterId == 0
                        || definition.ProjectileBehavior
                            != AuthoritativeProjectileBehavior.MonsterMagic)
                || definition.SuppressNonPrimaryPlayerImpact
                    && definition.ProjectileBehavior != AuthoritativeProjectileBehavior.SnakeCharge
                || definition.SnakeDriftPattern is not null
                    && definition.SnakeDriftPattern.Any(rotation => rotation is < -4 or > 4)
                || definition.ProjectileBehavior == AuthoritativeProjectileBehavior.SnakeCharge
                    && definition.SnakeDriftPattern is not null
                    && definition.SnakeDriftPattern.Count != 6
                || definition.ProjectileBehavior != AuthoritativeProjectileBehavior.SnakeCharge
                    && definition.SnakeDriftPattern is not null
                || definition.PuddleDurationRandomRange < 0
                || definition.PuddleDurationBase < 0
                || definition.PuddleDurationIntelligenceScale < 0
                || definition.PuddleDamageMinimumMonsterLevel < 0
                || definition.PuddleDamageLow < 0
                || definition.PuddleDamageHigh < definition.PuddleDamageLow
                || definition.PuddleImpactLifetime < 0
                || definition.ProjectileImpactLifetime < 0
                || definition.SnakeAdjacentAttackChanceBase is < 0 or > 100
                || definition.SnakeAdjacentAttackChanceIntelligenceScale is < 0 or > 100
                || definition.RangedDelayBase < 0
                || definition.RangedDelayRandomRange < 0
                || (definition.RangedDelayBase != 0
                    || definition.RangedDelayRandomRange != 0)
                    && (definition.AttackMode != AuthoritativeMonsterRangedAttackMode.Ranged
                        || definition.RangedDelayRandomRange <= 0)
                || definition.RangedAvoidanceDelayBase < 0
                || definition.RangedAvoidanceDelayRandomRange < 0
                || (definition.RangedAvoidanceDelayBase != 0
                    || definition.RangedAvoidanceDelayRandomRange != 0)
                    && (definition.AttackMode
                        != AuthoritativeMonsterRangedAttackMode.RangedAvoidance
                        || definition.RangedAvoidanceDelayRandomRange <= 0)
                || definition.HorkDemonDelayBase < 0
                || definition.HorkDemonDelayRandomRange < 0
                || (definition.HorkDemonDelayBase != 0
                    || definition.HorkDemonDelayRandomRange != 0)
                    && (definition.ProjectileBehavior
                        != AuthoritativeProjectileBehavior.HorkSpawn
                        || definition.HorkDemonDelayRandomRange <= 0)
                || definition.SnakeAdjacentDelayBase < 0
                || definition.SnakeAdjacentDelayIntelligenceScale < 0
                || definition.SnakeAdjacentDelayRandomRange < 0
                || definition.SnakeDistantDelayBase < 0
                || definition.SnakeDistantDelayIntelligenceScale < 0
                || definition.SnakeDistantDelayRandomRange < 0
                || definition.SnakeDistantDelayChanceBase is < 0 or > 100
                || definition.SnakeDistantDelayChanceIntelligenceScale is < 0 or > 100
                || (definition.SnakeAdjacentDelayBase != 0
                    || definition.SnakeAdjacentDelayIntelligenceScale != 0
                    || definition.SnakeAdjacentDelayRandomRange != 0
                    || definition.SnakeDistantDelayBase != 0
                    || definition.SnakeDistantDelayIntelligenceScale != 0
                    || definition.SnakeDistantDelayRandomRange != 0)
                    && (definition.ProjectileBehavior
                        != AuthoritativeProjectileBehavior.SnakeCharge
                        || definition.SnakeAdjacentDelayRandomRange <= 0
                        || definition.SnakeDistantDelayRandomRange <= 0)
                || (definition.SnakeDistantDelayChanceBase != 0
                    || definition.SnakeDistantDelayChanceIntelligenceScale != 0)
                    && (definition.ProjectileBehavior
                        != AuthoritativeProjectileBehavior.SnakeCharge
                        || definition.SnakeDistantDelayChanceBase <= 0
                        || definition.SnakeDistantDelayChanceIntelligenceScale <= 0)
                || definition.MegaDelayBase < 0
                || definition.MegaDelayRandomRange < 0
                || (definition.MegaDelayBase != 0
                    || definition.MegaDelayRandomRange != 0)
                    && (definition.ProjectileBehavior
                        != AuthoritativeProjectileBehavior.InfernoController
                        || definition.MegaDelayRandomRange <= 0)
                || definition.ChargeMinimumDistance is < 0 or > 64
                || definition.ChargeChanceBase is < 0 or > 100
                || definition.ChargeChanceIntelligenceScale is < 0 or > 100
                || definition.RangedAvoidanceMinimumDistance is < 0 or > 64
                || definition.RangedAvoidanceFarDistance is < 0 or > 64
                || definition.RangedAvoidanceChanceBase is < 0 or > 10_000
                || definition.RangedAvoidanceChanceIntelligenceScale is < 0 or > 10_000
                || definition.RangedAvoidanceFarIntelligenceOffset is < 0 or > 64
                || definition.RangedAvoidanceMovementChanceModulo is < 0 or > 64
                || definition.RangedAvoidanceMovementChanceBase is < 0 or > 100
                || definition.RangedAvoidanceMovementChanceIntelligenceScale is < 0 or > 100
                || definition.RangedAvoidanceMovementFreshChanceBonus is < 0 or > 100
                || definition.RangedRetreatMinimumDistance is < 0 or > 64
                || definition.RangedRetreatChanceBase is < 0 or > 100
                || definition.RangedRetreatChanceIntelligenceScale is < 0 or > 100
                || definition.InfernoMaximumDistance is < 0 or > 64
                || definition.InfernoAdjacentChanceBase is < 0 or > 100
                || definition.InfernoAdjacentChanceIntelligenceScale is < 0 or > 100
                || definition.InfernoRangedFarDistance is < 0 or > 64
                || definition.InfernoRangedChanceBase is < 0 or > 100
                || definition.InfernoRangedChanceIntelligenceScale is < 0 or > 100
                || definition.InfernoRangedFarIntelligenceOffset is < 0 or > 64
                || definition.InfernoAdjacentChoiceModulo is < 0 or > 100
                || (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.HorkSpawn
                    && definition.SpawnMonsterId == 0)
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
                || (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.SnakeCharge
                    && (definition.SnakeAdjacentAttackChanceBase != 0
                        || definition.SnakeAdjacentAttackChanceIntelligenceScale != 0)
                    && (definition.SnakeAdjacentAttackChanceBase <= 0
                        || definition.SnakeAdjacentAttackChanceIntelligenceScale <= 0))
                || (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.SnakeCharge
                    && (definition.SnakeAdjacentAttackChanceBase != 0
                        || definition.SnakeAdjacentAttackChanceIntelligenceScale != 0))
                || (definition.ProjectileBehavior is not (
                        AuthoritativeProjectileBehavior.BatCharge
                        or AuthoritativeProjectileBehavior.SnakeCharge)
                    && (definition.ChargeMinimumDistance != 0
                        || definition.ChargeChanceBase != 0
                        || definition.ChargeChanceIntelligenceScale != 0))
                || (definition.AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance
                    && (definition.RangedAvoidanceMinimumDistance != 0
                        || definition.RangedAvoidanceFarDistance != 0
                        || definition.RangedAvoidanceChanceBase != 0
                        || definition.RangedAvoidanceChanceIntelligenceScale != 0
                        || definition.RangedAvoidanceFarIntelligenceOffset != 0)
                    && (definition.RangedAvoidanceMinimumDistance <= 0
                        || definition.RangedAvoidanceFarDistance
                            < definition.RangedAvoidanceMinimumDistance
                        || definition.RangedAvoidanceChanceBase <= 0
                        || definition.RangedAvoidanceChanceIntelligenceScale <= 0))
                || (definition.AttackMode != AuthoritativeMonsterRangedAttackMode.RangedAvoidance
                    && (definition.RangedAvoidanceMinimumDistance != 0
                        || definition.RangedAvoidanceFarDistance != 0
                        || definition.RangedAvoidanceChanceBase != 0
                        || definition.RangedAvoidanceChanceIntelligenceScale != 0
                        || definition.RangedAvoidanceFarIntelligenceOffset != 0
                        || definition.RangedAvoidanceMovementChanceModulo != 0
                        || definition.RangedAvoidanceMovementChanceBase != 0
                        || definition.RangedAvoidanceMovementChanceIntelligenceScale != 0
                        || definition.RangedAvoidanceMovementFreshChanceBonus != 0))
                || (definition.RangedAvoidanceAdjacentAttackChanceBase != 0
                    || definition.RangedAvoidanceAdjacentAttackChanceIntelligenceScale != 0)
                    && (definition.AttackMode
                        != AuthoritativeMonsterRangedAttackMode.RangedAvoidance
                        || definition.RangedAvoidanceAdjacentAttackChanceBase <= 0
                        || definition.RangedAvoidanceAdjacentAttackChanceIntelligenceScale <= 0)
                || definition.RangedAvoidanceAdjacentAttackChanceDivisor != 0
                    && (definition.AttackMode
                        != AuthoritativeMonsterRangedAttackMode.RangedAvoidance
                        || definition.RangedAvoidanceAdjacentAttackChanceDivisor <= 0)
                || (definition.AttackMode == AuthoritativeMonsterRangedAttackMode.RangedAvoidance
                    && (definition.RangedAvoidanceMovementChanceBase != 0
                        || definition.RangedAvoidanceMovementChanceIntelligenceScale != 0
                        || definition.RangedAvoidanceMovementFreshChanceBonus != 0)
                    && (definition.RangedAvoidanceMovementChanceBase <= 0
                        || definition.RangedAvoidanceMovementChanceIntelligenceScale <= 0
                        || definition.RangedAvoidanceMovementFreshChanceBonus <= 0))
                || (definition.AttackMode == AuthoritativeMonsterRangedAttackMode.Ranged
                    && (definition.RangedRetreatMinimumDistance != 0
                        || definition.RangedRetreatChanceBase != 0
                        || definition.RangedRetreatChanceIntelligenceScale != 0)
                    && (definition.RangedRetreatMinimumDistance <= 0
                        || definition.RangedRetreatChanceBase <= 0
                        || definition.RangedRetreatChanceIntelligenceScale <= 0))
                || (definition.AttackMode != AuthoritativeMonsterRangedAttackMode.Ranged
                    && (definition.RangedRetreatMinimumDistance != 0
                        || definition.RangedRetreatChanceBase != 0
                        || definition.RangedRetreatChanceIntelligenceScale != 0))
                || (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoController
                    && (definition.InfernoMaximumDistance != 0
                        || definition.InfernoAdjacentChanceBase != 0
                        || definition.InfernoAdjacentChanceIntelligenceScale != 0
                        || definition.InfernoRangedFarDistance != 0
                        || definition.InfernoRangedChanceBase != 0
                        || definition.InfernoRangedChanceIntelligenceScale != 0
                        || definition.InfernoRangedFarIntelligenceOffset != 0
                        || definition.InfernoAdjacentChoiceModulo != 0)
                    && (definition.InfernoMaximumDistance <= 0
                        || definition.InfernoAdjacentChanceBase <= 0
                        || definition.InfernoAdjacentChanceIntelligenceScale <= 0
                        || definition.InfernoRangedFarDistance <= 0
                        || definition.InfernoRangedChanceBase <= 0
                        || definition.InfernoRangedChanceIntelligenceScale <= 0
                        || definition.InfernoAdjacentChoiceModulo <= 0))
                || (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.InfernoController
                    && (definition.InfernoMaximumDistance != 0
                        || definition.InfernoAdjacentChanceBase != 0
                        || definition.InfernoAdjacentChanceIntelligenceScale != 0
                        || definition.InfernoRangedFarDistance != 0
                        || definition.InfernoRangedChanceBase != 0
                        || definition.InfernoRangedChanceIntelligenceScale != 0
                        || definition.InfernoRangedFarIntelligenceOffset != 0
                        || definition.InfernoAdjacentChoiceModulo != 0))
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

    /** Finds a validated missile identity independent of its native-AI owner. */
    public bool TryGetByMissileId(
        string missileId,
        out AuthoritativeMonsterMissileDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(missileId)) {
            definition = null!;
            return false;
        }
        var match = definitions.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.MissileId, missileId, StringComparison.Ordinal));
        if (match is null) {
            definition = null!;
            return false;
        }
        definition = match;
        return true;
    }

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

    /** Validates optional stable spell references against the active spell catalog. */
    public void ValidateSpellReferences(AuthoritativeSpellCatalog spells)
    {
        ArgumentNullException.ThrowIfNull(spells);
        foreach (var definition in Definitions) {
            if (definition.SpellId == 0)
                continue;
            if (!spells.TryGet(definition.SpellId, out _))
                throw new InvalidDataException(
                    $"Monster missile '{definition.NativeAi}'/{definition.MissileId} "
                    + $"references unknown spell ID {definition.SpellId}.");
        }
    }

    /** Validates summoned monster identities against the active monster catalog. */
    public void ValidateSpawnMonsterReferences(AuthoritativeMonsterTypeCatalog monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(monsterTypes);
        foreach (var definition in Definitions.Where(definition => definition.SpawnMonsterId != 0)) {
            if (!monsterTypes.TryGet(definition.SpawnMonsterId, out _))
                throw new InvalidDataException(
                    $"Monster missile '{definition.NativeAi}'/{definition.MissileId} "
                    + $"references unknown spawn monster {definition.SpawnMonsterId}.");
        }
    }

    /** Validates every referenced missile identity against native misdat metadata. */
    public void ValidateMissileReferences(AuthoritativeNativeMissileCatalog nativeMissiles)
    {
        ArgumentNullException.ThrowIfNull(nativeMissiles);
        foreach (var definition in Definitions) {
            if (!nativeMissiles.TryGet(definition.MissileId, out _))
                throw new InvalidDataException(
                    $"Monster missile '{definition.NativeAi}'/{definition.MissileId} "
                    + "references an unknown native missile identity.");
        }
    }

    /** Validates monster-specific selectors against the active monster catalog. */
    public void ValidateMonsterSelectorReferences(AuthoritativeMonsterTypeCatalog monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(monsterTypes);
        var activeNativeAis = monsterTypes.Definitions
            .Select(definition => definition.NativeAi)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var definition in Definitions.Where(definition =>
                     definition.MonsterId != 0 && activeNativeAis.Contains(definition.NativeAi))) {
            if (!monsterTypes.TryGet(definition.MonsterId, out _))
                throw new InvalidDataException(
                    $"Monster missile '{definition.NativeAi}'/{definition.MissileId} "
                    + $"references unknown monster type {definition.MonsterId}.");
        }
    }

    public static AuthoritativeMonsterMissileCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var hasSpellSymbol = table.Columns.Contains("spell_symbol");
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
                AuthoritativeMonsterIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "monster_id",
                    "monster_symbol"),
                ResolveSpellId(row, hasSpellSymbol, stableIds),
                AuthoritativeMonsterIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "spawn_monster_id",
                    "spawn_monster_symbol"),
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
                row.OptionalInt32("charge_chance_intelligence_scale"),
                row.OptionalInt32("ranged_avoidance_minimum_distance"),
                row.OptionalInt32("ranged_avoidance_far_distance"),
                row.OptionalInt32("ranged_avoidance_chance_base"),
                row.OptionalInt32("ranged_avoidance_chance_intelligence_scale"),
                row.OptionalInt32("ranged_avoidance_far_intelligence_offset"),
                row.OptionalInt32("ranged_retreat_minimum_distance"),
                row.OptionalInt32("ranged_retreat_chance_base"),
                row.OptionalInt32("ranged_retreat_chance_intelligence_scale"),
                row.OptionalInt32("inferno_maximum_distance"),
                row.OptionalInt32("inferno_adjacent_chance_base"),
                row.OptionalInt32("inferno_adjacent_chance_intelligence_scale"),
                row.OptionalInt32("inferno_ranged_far_distance"),
                row.OptionalInt32("inferno_ranged_chance_base"),
                row.OptionalInt32("inferno_ranged_chance_intelligence_scale"),
                row.OptionalInt32("inferno_ranged_far_intelligence_offset"),
                row.OptionalInt32("inferno_adjacent_choice_modulo"),
                row.OptionalInt32("projectile_damage"),
                row.OptionalInt32("puddle_impact_lifetime"),
                row.OptionalInt32("projectile_impact_lifetime"),
                ParseSnakeDriftPattern(row),
                row.OptionalInt32("ranged_delay_base"),
                row.OptionalInt32("ranged_delay_random_range"),
                row.OptionalInt32("ranged_avoidance_delay_base"),
                row.OptionalInt32("ranged_avoidance_delay_random_range"),
                row.OptionalInt32("hork_demon_delay_base"),
                row.OptionalInt32("hork_demon_delay_random_range"),
                row.OptionalInt32("snake_adjacent_delay_base"),
                row.OptionalInt32("snake_adjacent_delay_intelligence_scale"),
                row.OptionalInt32("snake_adjacent_delay_random_range"),
                row.OptionalInt32("snake_distant_delay_base"),
                row.OptionalInt32("snake_distant_delay_intelligence_scale"),
                row.OptionalInt32("snake_distant_delay_random_range"),
                row.OptionalInt32("snake_distant_delay_chance_base"),
                row.OptionalInt32("snake_distant_delay_chance_intelligence_scale"),
                row.OptionalInt32("snake_adjacent_attack_chance_base"),
                row.OptionalInt32("snake_adjacent_attack_chance_intelligence_scale"),
                row.OptionalInt32("mega_delay_base"),
                row.OptionalInt32("mega_delay_random_range")) {
                RangedAvoidanceMovementChanceModulo = row.OptionalInt32(
                    "ranged_avoidance_movement_chance_modulo"),
                RangedAvoidanceMovementChanceBase = row.OptionalInt32(
                    "ranged_avoidance_movement_chance_base"),
                RangedAvoidanceMovementChanceIntelligenceScale = row.OptionalInt32(
                    "ranged_avoidance_movement_chance_intelligence_scale"),
                RangedAvoidanceMovementFreshChanceBonus = row.OptionalInt32(
                    "ranged_avoidance_movement_fresh_chance_bonus"),
                ProjectileDamageRandomRange = row.OptionalInt32(
                    "projectile_damage_random_range"),
                RangedAvoidanceAdjacentAttackChanceBase = row.OptionalInt32(
                    "ranged_avoidance_adjacent_attack_chance_base"),
                RangedAvoidanceAdjacentAttackChanceIntelligenceScale = row.OptionalInt32(
                    "ranged_avoidance_adjacent_attack_chance_intelligence_scale"),
                RangedAvoidanceAdjacentAttackChanceDivisor = row.OptionalInt32(
                    "ranged_avoidance_adjacent_attack_chance_divisor"),
                IsPostMeleeMissile = row.OptionalBoolean("post_melee_missile"),
                InitialTravelQuanta = row.OptionalInt32("initial_travel_quanta"),
                ProjectileCount = row.OptionalInt32("projectile_count"),
                SuppressNonPrimaryPlayerImpact = row.OptionalBoolean(
                    "suppress_non_primary_player_impact"),
            };
        }));
    }

    private static IReadOnlyList<int>? ParseSnakeDriftPattern(TsvRow row)
    {
        if (!row.TryGet("snake_drift_pattern", out var value)
            || string.IsNullOrWhiteSpace(value))
            return null;

        var entries = value.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pattern = new int[entries.Length];
        for (var index = 0; index < entries.Length; index++) {
            if (!int.TryParse(entries[index], out pattern[index]))
                throw new InvalidDataException(
                    $"Monster missile row {row.LineNumber} contains an invalid Snake drift rotation.");
        }
        return pattern;
    }

    private static uint ResolveSpellId(
        TsvRow row,
        bool hasSpellSymbol,
        StableContentIdCatalog? stableIds)
    {
        var numericId = row.OptionalUInt32("spell_id");
        if (!hasSpellSymbol
            || !row.TryGet("spell_symbol", out var symbol)
            || string.IsNullOrWhiteSpace(symbol)) {
            return numericId;
        }
        if (stableIds is null) {
            if (numericId == 0) {
                throw new InvalidDataException(
                    $"Monster missile row {row.LineNumber} requires stable spell IDs to resolve '{symbol}'.");
            }
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("spell", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Monster missile row {row.LineNumber} references unknown stable spell symbol '{symbol}'.",
                exception);
        }
        if (numericId != 0 && numericId != resolvedId) {
            throw new InvalidDataException(
                $"Monster missile row {row.LineNumber} maps spell {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        }
        return resolvedId;
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
