using Devilution.Data;

namespace Devilution.Diablo;

/** Module-owned projectile transition selected by external spell data. */
public enum AuthoritativeProjectileBehavior
{
    Targeted = 0,
    ChargedBolt = 1,
    Firebolt = 2,
    Lightning = 3,
    LightningSegment = 4,
    Flash = 5,
    FlashBottom = 6,
    FlashTop = 7,
    Healing = 8,
    Teleport = 9,
    Resurrection = 10,
    ResurrectionBeam = 11,
    ManaShield = 12,
    StoneCurse = 13,
    Infravision = 14,
    Phasing = 15,
    Fireball = 16,
    HealOther = 17,
    Search = 18,
    HolyBolt = 19,
    BloodStar = 20,
    BoneSpirit = 21,
    FireWallController = 22,
    FireWallSegment = 23,
    FlameWaveController = 24,
    FlameWaveSegment = 25,
    InfernoController = 26,
    InfernoSegment = 27,
    ChainLightningController = 28,
    ChainLightningBolt = 29,
}

/** Declarative spell tuning consumed by authoritative cast resolution. */
public sealed record AuthoritativeSpellDefinition(
    uint SpellId,
    int ManaCost,
    int HealingAmount,
    uint StatusEffectId,
    uint StatusDuration,
    int StatusMagnitude)
{
    public int DamageAmount { get; init; }

    /** Per-level reduction applied after the first effective spell level. */
    public int ManaAdjustment { get; init; }

    /** Divides the level adjustment before it is subtracted. */
    public int ManaAdjustmentDivisor { get; init; } = 1;

    /** Additional whole-point mana cost per character level. */
    public int CharacterLevelManaCost { get; init; }

    /** Whole-point floor applied after class-specific mana reduction. */
    public int MinimumManaCost { get; init; }

    /** Converts content mana points into the simulation resource unit. */
    public int ManaCostScale { get; init; } = 1;

    public int Range { get; init; } = 1;

    public int AreaRadius { get; init; }

    public AuthoritativeDamageType DamageType { get; init; } = AuthoritativeDamageType.Physical;

    /** Zero keeps the legacy immediate-resolution behavior. */
    public int ProjectileSpeed { get; init; }

    public int ProjectileLifetime { get; init; } = 32;

    /** Number of independently authoritative projectiles created by one cast. */
    public int ProjectileCount { get; init; } = 1;

    /** Adds floor(effective spell level / divisor) projectiles; zero disables scaling. */
    public int ProjectileCountLevelDivisor { get; init; }

    public AuthoritativeProjectileBehavior ProjectileBehavior { get; init; }

    /** Native random damage is GenerateRnd(current magic / divisor) + 1. */
    public int RandomMagicDamageDivisor { get; init; }

    /** Exclusive upper bound passed to native GenerateRnd for base damage. */
    public int RandomDamageRange { get; init; }

    /** Divides current magic before it contributes to projectile damage. */
    public int MagicDamageDivisor { get; init; }

    /** Divides current magic before it is subtracted from Blood Star damage. */
    public int MagicDamageReductionDivisor { get; init; }

    /** Whole-point damage contributed by each effective spell level. */
    public int SpellLevelDamage { get; init; }

    /** Constant whole-point projectile damage added after scaling. */
    public int DamageBonus { get; init; }

    /** Whole life/mana-shield damage applied immediately when this spell is cast. */
    public int SelfDamage { get; init; }

    /** Divides character level before it supplies a native random-damage bound. */
    public int CharacterLevelRandomDamageDivisor { get; init; }

    /** Converts generated whole-point damage into the projectile's stored unit. */
    public int DamageScale { get; init; } = 1;

    /** Whole speed units added for each effective spell level. */
    public int ProjectileSpeedSpellLevelStep { get; init; }

    /** Maximum speed bonus contributed by effective spell level. */
    public int ProjectileSpeedMaximumBonus { get; init; }

    /** Divides character level before determining repeated random-damage rolls. */
    public int RandomDamageIterationsCharacterLevelDivisor { get; init; }

    /** Adds a fixed number of repeated random-damage rolls. */
    public int RandomDamageIterationsBonus { get; init; }

    /** Divides character level before adding it to generated damage. */
    public int CharacterLevelDamageDivisor { get; init; }

    /** Each effective spell level adds the current damage divided by this value. */
    public int SpellEffectScaleDivisor { get; init; }

    /** Adds this percentage of scaled damage after spell-level scaling. */
    public int PostScaleDamagePercent { get; init; }

    /** Exclusive upper bound for Healing's initial native random roll. */
    public int RandomHealingRange { get; init; }

    /** Exclusive upper bound rolled once per character level by Healing. */
    public int CharacterLevelRandomHealingRange { get; init; }

    /** Fixed amount added with each character-level Healing roll. */
    public int CharacterLevelHealing { get; init; }

    /** Exclusive upper bound rolled once per effective spell level by Healing. */
    public int SpellLevelRandomHealingRange { get; init; }

    /** Fixed amount added with each effective-spell-level Healing roll. */
    public int SpellLevelHealing { get; init; }

    /** Converts generated whole healing points into the simulation resource unit. */
    public int HealingScale { get; init; } = 1;

    /** Base lifetime of a secondary projectile emitted by a controller. */
    public int SecondaryProjectileLifetime { get; init; }

    /** Adds floor(effective spell level / divisor) to secondary lifetime. */
    public int SecondaryProjectileLifetimeSpellLevelDivisor { get; init; }

    /** Positive native monster resistance divides supported projectile damage by this value. */
    public int ResistantDamageDivisor { get; init; } = 1;

    /** Non-colliding impact phase retained by behaviors that stop on collision. */
    public int ProjectileImpactLifetime { get; init; } = 1;

    /** Whole life points restored when Resurrection revives a dead player. */
    public int ResurrectionLife { get; init; }

    /** Converts Resurrection life points into the simulation resource unit. */
    public int ResurrectionLifeScale { get; init; } = 1;

    /** Maximum native crawl radius used when a revived player must be relocated. */
    public int ResurrectionSearchRadius { get; init; }

    /** Reciprocal base used by native Mana Shield damage conversion. */
    public int ManaShieldBaseDamageReductionDivisor { get; init; }

    /** Amount subtracted from the reciprocal for each learned spell level. */
    public int ManaShieldDamageReductionLevelStep { get; init; }

    /** Maximum learned spell level used by Mana Shield damage conversion. */
    public int ManaShieldDamageReductionMaximumLevel { get; init; }

    /** Constant added to effective spell level before Stone Curse duration is capped. */
    public int StoneCurseBaseDuration { get; init; }

    /** Maximum unscaled Stone Curse duration. */
    public int StoneCurseMaximumDuration { get; init; }

    /** Converts Stone Curse duration units into authoritative ticks. */
    public int StoneCurseDurationScale { get; init; }

    /** Maximum native crawl radius used to find a curseable monster. */
    public int StoneCurseSearchRadius { get; init; }

    /** Repeated reciprocal growth used by native Infravision duration scaling. */
    public int InfravisionDurationGrowthDivisor { get; init; }

    /** Native Search ticks added for each character level. */
    public int SearchCharacterLevelDurationMultiplier { get; init; }

    /** Native Search ticks added for each effective spell level. */
    public int SearchSpellLevelDurationMultiplier { get; init; }

    /** Minimum absolute offset on each axis considered by native Phasing. */
    public int PhasingMinimumAxisOffset { get; init; }

    /** Maximum absolute offset on each axis considered by native Phasing. */
    public int PhasingMaximumAxisOffset { get; init; }

    /** Native staff-generation level used as the Staff Recharge divisor. */
    public int StaffLevel { get; init; }

    /** Native Fire Wall controller lifetime in simulation ticks. */
    public int WallControlLifetime { get; init; }

    /** Base native Fire Wall segment duration before spell/character scaling. */
    public int WallSegmentBaseDuration { get; init; }

    /** Native duration units added for each effective spell level. */
    public int WallSegmentSpellLevelDurationStep { get; init; }

    /** Native duration units added for each character level. */
    public int WallSegmentCharacterLevelDurationStep { get; init; }

    /** Converts native Fire Wall duration units into simulation ticks. */
    public int WallSegmentDurationScale { get; init; }

    /** Character-level multiplier used by the native Fire Wall damage roll. */
    public int WallCharacterLevelDamageMultiplier { get; init; }

    /** Native Flame Wave controller lifetime in simulation ticks. */
    public int FlameWaveControllerLifetime { get; init; }

    /** Base number of Flame Wave branches on each side of its center segment. */
    public int FlameWaveBranchBase { get; init; }

    /** Divisor for effective spell level added to the Flame Wave branch count. */
    public int FlameWaveBranchSpellLevelDivisor { get; init; }

    /** Character-level bonus in each Flame Wave segment's damage roll. */
    public int FlameWaveCharacterLevelDamageBonus { get; init; }

    /** Native Inferno segment lifetime increase for each controller step. */
    public int InfernoSegmentLifetimeStep { get; init; }

    /** Native Chain Lightning crawl radius added to the effective spell level. */
    public int ChainLightningSearchRadiusBonus { get; init; }

    /** Native one-tick Chain Lightning fan-out controller lifetime. */
    public int ChainLightningControllerLifetime { get; init; }
}

public sealed class AuthoritativeSpellCatalog
{
    private readonly Dictionary<uint, AuthoritativeSpellDefinition> spells;

    public AuthoritativeSpellCatalog(IEnumerable<AuthoritativeSpellDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        spells = new Dictionary<uint, AuthoritativeSpellDefinition>();
        foreach (var definition in definitions) {
            if (definition.SpellId == 0 || definition.ManaCost <= 0 || definition.HealingAmount < 0 || definition.DamageAmount < 0)
                throw new InvalidDataException("Spell definitions require a non-zero ID, positive mana cost, and non-negative effects.");
            if (definition.ManaAdjustment < 0
                || definition.ManaAdjustmentDivisor <= 0
                || definition.CharacterLevelManaCost < 0
                || definition.MinimumManaCost < 0
                || definition.ManaCostScale <= 0
                || definition.ManaCostScale > 64) {
                throw new InvalidDataException("Spell mana scaling is invalid.");
            }
            if (definition.StaffLevel < -1)
                throw new InvalidDataException("Spell staff level cannot be less than the native unavailable sentinel.");
            var hasDamage = definition.DamageAmount > 0
                || definition.RandomMagicDamageDivisor > 0
                || definition.RandomDamageRange > 0
                || definition.CharacterLevelRandomDamageDivisor > 0
                || definition.MagicDamageDivisor > 0
                || definition.SpellLevelDamage > 0
                || definition.ProjectileBehavior is AuthoritativeProjectileBehavior.BoneSpirit
                    or AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.FireWallSegment
                    or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment
                    or AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment
                    or AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt;
            if (hasDamage && definition.Range <= 0)
                throw new InvalidDataException("Damage-bearing spells require a positive range.");
            if (definition.AreaRadius < 0)
                throw new InvalidDataException("Spell area radius cannot be negative.");
            if (definition.ProjectileSpeed < 0 || definition.ProjectileLifetime <= 0)
                throw new InvalidDataException("Projectile speed cannot be negative and projectile lifetime must be positive.");
            if (definition.ProjectileSpeed > 0 && !hasDamage)
                throw new InvalidDataException("Only damage-bearing spells can create projectiles.");
            if (definition.ProjectileCount <= 0 || definition.ProjectileCount > 32)
                throw new InvalidDataException("Spell projectile count must be between 1 and 32.");
            if (definition.ProjectileCountLevelDivisor < 0
                || (definition.ProjectileCountLevelDivisor > 0
                    && definition.ProjectileCount + 15 / definition.ProjectileCountLevelDivisor > 32)) {
                throw new InvalidDataException(
                    "Spell level-scaled projectile count exceeds the native supported range.");
            }
            if (definition.ProjectileCount > 1
                && definition.ProjectileSpeed == 0
                && definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Flash) {
                throw new InvalidDataException("Multi-projectile spells require projectile movement.");
            }
            if (definition.RandomMagicDamageDivisor < 0
                || definition.RandomDamageRange < 0
                || definition.MagicDamageDivisor < 0
                || definition.MagicDamageReductionDivisor < 0
                || definition.SpellLevelDamage < 0
                || definition.DamageBonus < 0
                || definition.SelfDamage < 0
                || definition.CharacterLevelRandomDamageDivisor < 0
                || definition.DamageScale <= 0
                || definition.DamageScale > 64
                || definition.ProjectileSpeedSpellLevelStep < 0
                || definition.ProjectileSpeedMaximumBonus < 0
                || definition.ProjectileSpeedMaximumBonus > 256
                || definition.RandomDamageIterationsCharacterLevelDivisor < 0
                || definition.RandomDamageIterationsBonus is < 0 or > 64
                || definition.CharacterLevelDamageDivisor < 0
                || definition.SpellEffectScaleDivisor < 0
                || definition.PostScaleDamagePercent is < 0 or > 400
                || definition.SecondaryProjectileLifetime < 0
                || definition.SecondaryProjectileLifetimeSpellLevelDivisor < 0
                || definition.ResistantDamageDivisor <= 0
                || definition.ProjectileImpactLifetime <= 0) {
                throw new InvalidDataException(
                    "Projectile damage divisors and impact lifetime are invalid.");
            }
            if (definition.RandomDamageIterationsCharacterLevelDivisor > 0
                && 50 / definition.RandomDamageIterationsCharacterLevelDivisor
                    + definition.RandomDamageIterationsBonus > 64) {
                throw new InvalidDataException(
                    "Spell random-damage roll count exceeds the native supported range.");
            }
            if (definition.RandomHealingRange < 0
                || definition.RandomHealingRange > short.MaxValue
                || definition.CharacterLevelRandomHealingRange < 0
                || definition.CharacterLevelRandomHealingRange > short.MaxValue
                || definition.CharacterLevelHealing < 0
                || definition.CharacterLevelHealing > short.MaxValue
                || definition.SpellLevelRandomHealingRange < 0
                || definition.SpellLevelRandomHealingRange > short.MaxValue
                || definition.SpellLevelHealing < 0
                || definition.SpellLevelHealing > short.MaxValue
                || definition.HealingScale <= 0
                || definition.HealingScale > 64) {
                throw new InvalidDataException("Spell healing scaling is invalid.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.ChargedBolt
                && (definition.ProjectileSpeed == 0
                    || definition.RandomMagicDamageDivisor == 0
                    || definition.AreaRadius != 0)) {
                throw new InvalidDataException(
                    "Charged Bolt behavior requires movement, random magic damage, and no area radius.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Firebolt
                && (definition.ProjectileSpeed == 0
                    || definition.AreaRadius != 0
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.RandomDamageRange == 0
                    || definition.MagicDamageDivisor == 0)) {
                throw new InvalidDataException(
                    "Firebolt behavior requires one moving, random, magic-scaled projectile.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.BloodStar
                && (definition.HealingAmount != 0
                    || definition.DamageAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.ProjectileSpeed != 16
                    || definition.ProjectileLifetime != 256
                    || definition.AreaRadius != 0
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.MagicDamageDivisor != 2
                    || definition.MagicDamageReductionDivisor != 8
                    || definition.SpellLevelDamage != 3
                    || definition.SelfDamage != 5
                    || definition.ResistantDamageDivisor != 4
                    || definition.ProjectileImpactLifetime != 1)) {
                throw new InvalidDataException(
                    "Blood Star behavior requires the native fixed magic projectile and self-damage tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.BoneSpirit
                && (definition.HealingAmount != 0
                    || definition.DamageAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.ProjectileSpeed != 16
                    || definition.ProjectileLifetime != 256
                    || definition.AreaRadius != 0
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ResistantDamageDivisor != 4
                    || definition.ProjectileImpactLifetime != 7
                    || definition.SelfDamage != 6)) {
                throw new InvalidDataException(
                    "Bone Spirit behavior requires the native homing magic projectile and self-damage tuning.");
            }
            if ((definition.ProjectileBehavior is AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.FireWallSegment)
                && (definition.HealingAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.DamageType != AuthoritativeDamageType.Fire
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0
                    || definition.RandomDamageRange <= 0
                    || definition.RandomDamageIterationsBonus != 2
                    || definition.DamageBonus != 2
                    || definition.DamageScale != 8
                    || definition.ResistantDamageDivisor != 4
                    || definition.WallControlLifetime != 7
                    || definition.WallSegmentBaseDuration != 10
                    || definition.WallSegmentSpellLevelDurationStep != 10
                    || definition.WallSegmentCharacterLevelDurationStep != 1
                    || definition.WallSegmentDurationScale != 16
                    || definition.WallCharacterLevelDamageMultiplier != 1)) {
                throw new InvalidDataException(
                    "Fire Wall behavior requires the native seven-tick controller and shifted fire-segment tuning.");
            }
            if ((definition.ProjectileBehavior is AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment)
                && (definition.HealingAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.DamageType != AuthoritativeDamageType.Fire
                    || definition.ProjectileSpeed != 16
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0
                    || definition.RandomDamageRange != 10
                    || definition.DamageBonus != 1
                    || definition.DamageScale != 1
                    || definition.ResistantDamageDivisor != 4
                    || definition.ProjectileLifetime != 1
                    || definition.SecondaryProjectileLifetime != 255
                    || definition.FlameWaveControllerLifetime != 1
                    || definition.FlameWaveBranchBase != 2
                    || definition.FlameWaveBranchSpellLevelDivisor != 2
                    || definition.FlameWaveCharacterLevelDamageBonus != 1)) {
                throw new InvalidDataException(
                    "Flame Wave behavior requires the native moving fire-wave tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.FlameWaveSegment)
                throw new InvalidDataException(
                    "FlameWaveSegment is runtime-owned and cannot be selected by spell content.");

            if ((definition.ProjectileBehavior is AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment)
                && (definition.ProjectileSpeed != 32
                    || definition.AreaRadius != 0
                    || definition.ResistantDamageDivisor != 1
                    || definition.InfernoSegmentLifetimeStep != 1
                    || definition.RandomDamageRange != 2
                    || definition.DamageScale != 12
                    || definition.DamageBonus != 24)) {
                throw new InvalidDataException(
                    "Inferno behavior requires the native moving fire-stream tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.InfernoSegment)
                throw new InvalidDataException(
                    "InfernoSegment is runtime-owned and cannot be selected by spell content.");

            if ((definition.ProjectileBehavior is AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt)
                && (definition.ProjectileSpeed != 32
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Lightning
                    || definition.ResistantDamageDivisor != 4
                    || definition.ChainLightningSearchRadiusBonus != 3
                    || definition.ChainLightningControllerLifetime != 1
                    || definition.RandomDamageRange != 2
                    || definition.DamageBonus != 2
                    || definition.CharacterLevelRandomDamageDivisor != 1
                    || definition.DamageScale != 64
                    || definition.SecondaryProjectileLifetime != 6
                    || definition.SecondaryProjectileLifetimeSpellLevelDivisor != 2)) {
                throw new InvalidDataException(
                    "Chain Lightning behavior requires the native fan-out and Lightning tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.ChainLightningBolt)
                throw new InvalidDataException(
                    "ChainLightningBolt is runtime-owned and cannot be selected by spell content.");
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.HolyBolt
                && (definition.HealingAmount != 0
                    || definition.DamageAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.ProjectileSpeed <= 0
                    || definition.AreaRadius != 0
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.DamageType != AuthoritativeDamageType.Physical
                    || definition.RandomDamageRange <= 0
                    || definition.DamageBonus <= 0
                    || definition.CharacterLevelDamageDivisor <= 0
                    || definition.RandomDamageIterationsCharacterLevelDivisor != 0
                    || definition.RandomDamageIterationsBonus != 0
                    || definition.SpellEffectScaleDivisor != 0
                    || definition.ProjectileSpeedSpellLevelStep <= 0
                    || definition.ProjectileSpeedMaximumBonus < definition.ProjectileSpeedSpellLevelStep
                    || (long)definition.ProjectileSpeed
                        + definition.ProjectileSpeedMaximumBonus > 512
                    || definition.MagicDamageDivisor != 0
                    || definition.SpellLevelDamage != 0
                    || definition.ResistantDamageDivisor <= 0)) {
                throw new InvalidDataException(
                    "Holy Bolt behavior requires one moving physical-damage projectile with native undead-only scaling.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Fireball
                && (definition.HealingAmount != 0
                    || definition.DamageAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.ProjectileSpeed <= 0
                    || definition.ProjectileSpeedSpellLevelStep <= 0
                    || definition.ProjectileSpeedMaximumBonus
                        < definition.ProjectileSpeedSpellLevelStep
                    || (long)definition.ProjectileSpeed
                        + definition.ProjectileSpeedMaximumBonus > 512
                    || definition.AreaRadius != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.DamageType != AuthoritativeDamageType.Fire
                    || definition.RandomDamageRange <= 0
                    || definition.RandomDamageIterationsCharacterLevelDivisor != 0
                    || definition.RandomDamageIterationsBonus <= 0
                    || definition.CharacterLevelDamageDivisor <= 0
                    || definition.SpellEffectScaleDivisor <= 0
                    || definition.DamageScale <= 1
                    || definition.DamageBonus <= 0
                    || definition.ResistantDamageDivisor <= 1
                    || definition.ProjectileImpactLifetime <= 1)) {
                throw new InvalidDataException(
                    "Fireball behavior requires one level-scaled fire projectile with repeated random damage and a persistent area impact.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Lightning
                && (definition.ProjectileSpeed == 0
                    || definition.AreaRadius != 0
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.RandomDamageRange == 0
                    || definition.CharacterLevelRandomDamageDivisor == 0
                    || definition.DamageScale != 64
                    || definition.SecondaryProjectileLifetime == 0
                    || definition.SecondaryProjectileLifetimeSpellLevelDivisor == 0)) {
                throw new InvalidDataException(
                    "Lightning behavior requires one moving controller with random Q6 damage and secondary lifetime scaling.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.LightningSegment)
                throw new InvalidDataException("LightningSegment is runtime-owned and cannot be selected by spell content.");
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Flash
                && (definition.ProjectileSpeed != 0
                    || definition.AreaRadius != 1
                    || definition.ProjectileCount != 2
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.DamageAmount != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.RandomDamageRange == 0
                    || definition.DamageBonus == 0
                    || definition.RandomDamageIterationsCharacterLevelDivisor == 0
                    || definition.RandomDamageIterationsBonus == 0
                    || definition.CharacterLevelDamageDivisor == 0
                    || definition.SpellEffectScaleDivisor == 0
                    || definition.PostScaleDamagePercent == 0
                    || definition.DamageScale != 1)) {
                throw new InvalidDataException(
                    "Flash behavior requires two stationary, repeated random-damage layers with native scaling.");
            }
            if (definition.ProjectileBehavior is AuthoritativeProjectileBehavior.FlashBottom
                or AuthoritativeProjectileBehavior.FlashTop) {
                throw new InvalidDataException(
                    "Flash layer behaviors are runtime-owned and cannot be selected by spell content.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Healing
                && (definition.HealingAmount <= 0
                    || definition.RandomHealingRange == 0
                    || definition.CharacterLevelRandomHealingRange == 0
                    || definition.CharacterLevelHealing == 0
                    || definition.SpellLevelRandomHealingRange == 0
                    || definition.SpellLevelHealing == 0
                    || definition.HealingScale != 64
                    || definition.DamageAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0)) {
                throw new InvalidDataException(
                    "Healing behavior requires one immediate self effect with native random Q6 scaling.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.HealOther
                && (definition.HealingAmount <= 0
                    || definition.RandomHealingRange == 0
                    || definition.CharacterLevelRandomHealingRange == 0
                    || definition.CharacterLevelHealing == 0
                    || definition.SpellLevelRandomHealingRange == 0
                    || definition.SpellLevelHealing == 0
                    || definition.HealingScale != 64
                    || hasDamage
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.Range <= 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0)) {
                throw new InvalidDataException(
                    "Heal Other behavior requires one immediate living-player effect with native random Q6 scaling.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Teleport
                && (definition.HealingAmount != 0
                    || hasDamage
                    || definition.StatusEffectId != 0
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 2
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0)) {
                throw new InvalidDataException(
                    "Teleport behavior requires one stationary two-tick movement controller without gameplay effects.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Resurrection
                && (definition.HealingAmount != 0
                    || hasDamage
                    || definition.StatusEffectId != 0
                    || definition.Range <= 0
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0
                    || definition.ResurrectionLife <= 0
                    || definition.ResurrectionLifeScale <= 0
                    || definition.ResurrectionLifeScale > 64
                    || definition.ResurrectionSearchRadius <= 0
                    || definition.ResurrectionSearchRadius > DiabloCrawl.MaximumSupportedRadius)) {
                throw new InvalidDataException(
                    "Resurrection behavior requires one stationary player-target effect with positive life and crawl tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.ResurrectionBeam) {
                throw new InvalidDataException(
                    "ResurrectionBeam is runtime-owned and cannot be selected by spell content.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.ManaShield
                && (definition.HealingAmount != 0
                    || hasDamage
                    || definition.StatusEffectId != 0
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0
                    || definition.ManaShieldBaseDamageReductionDivisor <= 0
                    || definition.ManaShieldDamageReductionLevelStep <= 0
                    || definition.ManaShieldDamageReductionMaximumLevel <= 0
                    || (long)definition.ManaShieldBaseDamageReductionDivisor
                        - (long)definition.ManaShieldDamageReductionLevelStep
                            * definition.ManaShieldDamageReductionMaximumLevel <= 1)) {
                throw new InvalidDataException(
                    "Mana Shield behavior requires one immediate self effect with a safe positive damage-reduction reciprocal.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.StoneCurse
                && (definition.HealingAmount != 0
                    || hasDamage
                    || definition.StatusEffectId != 0
                    || definition.Range <= 0
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.ResistantDamageDivisor != 1
                    || definition.ProjectileImpactLifetime <= 0
                    || definition.StoneCurseBaseDuration <= 0
                    || definition.StoneCurseMaximumDuration
                        <= definition.StoneCurseBaseDuration
                    || definition.StoneCurseDurationScale <= 0
                    || (long)definition.StoneCurseMaximumDuration
                        * definition.StoneCurseDurationScale > uint.MaxValue
                    || definition.StoneCurseSearchRadius <= 0
                    || definition.StoneCurseSearchRadius
                        > DiabloCrawl.MaximumSupportedRadius)) {
                throw new InvalidDataException(
                    "Stone Curse behavior requires one stationary monster-target controller with safe duration and crawl tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Infravision
                && (definition.HealingAmount != 0
                    || hasDamage
                    || definition.StatusEffectId == 0
                    || definition.StatusDuration == 0
                    || definition.StatusMagnitude != 0
                    || definition.Range != 1
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.InfravisionDurationGrowthDivisor <= 1)) {
                throw new InvalidDataException(
                    "Infravision behavior requires one immediate self status with safe reciprocal duration growth.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Search
                && (definition.HealingAmount != 0
                    || hasDamage
                    || definition.StatusEffectId == 0
                    || definition.StatusDuration == 0
                    || definition.StatusMagnitude != 0
                    || definition.Range != 1
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.SearchCharacterLevelDurationMultiplier <= 0
                    || definition.SearchSpellLevelDurationMultiplier <= 0
                    || (ulong)definition.StatusDuration
                        + 50UL * (uint)definition.SearchCharacterLevelDurationMultiplier
                        + (ulong)AuthoritativePlayerLoadoutCatalog.MaximumNativeSpellLevel
                            * (uint)definition.SearchSpellLevelDurationMultiplier
                        >= int.MaxValue)) {
                throw new InvalidDataException(
                    "Search behavior requires one immediate self status with safe character- and spell-level duration scaling.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Phasing
                && (definition.HealingAmount != 0
                    || hasDamage
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.Range != 1
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 2
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.PhasingMinimumAxisOffset <= 0
                    || definition.PhasingMaximumAxisOffset
                        < definition.PhasingMinimumAxisOffset
                    || definition.PhasingMaximumAxisOffset > 32)) {
                throw new InvalidDataException(
                    "Phasing behavior requires one stationary two-tick movement controller with safe random-offset tuning.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.ChargedBolt
                && (definition.ProjectileCountLevelDivisor != 0
                    || definition.RandomMagicDamageDivisor != 0)) {
                throw new InvalidDataException(
                    "Charged Bolt tuning columns require ChargedBolt projectile behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Firebolt
                    or AuthoritativeProjectileBehavior.HolyBolt
                    or AuthoritativeProjectileBehavior.Lightning
                    or AuthoritativeProjectileBehavior.Flash
                    or AuthoritativeProjectileBehavior.Fireball
                    or AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.FireWallSegment
                    or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment
                    or AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment
                    or AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt)
                && (definition.RandomDamageRange != 0
                    || definition.DamageBonus != 0)) {
                throw new InvalidDataException(
                    "Native random damage columns require a supported projectile behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Firebolt
                    or AuthoritativeProjectileBehavior.BloodStar
                    or AuthoritativeProjectileBehavior.BoneSpirit)
                && (definition.MagicDamageDivisor != 0
                    || definition.SpellLevelDamage != 0
                    || definition.MagicDamageReductionDivisor != 0)) {
                throw new InvalidDataException(
                    "Magic and spell-level damage scaling requires Firebolt, BloodStar, or BoneSpirit projectile behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.BloodStar
                    or AuthoritativeProjectileBehavior.BoneSpirit)
                && definition.SelfDamage != 0) {
                throw new InvalidDataException(
                    "Immediate self-damage tuning requires BloodStar or BoneSpirit projectile behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Flash
                    or AuthoritativeProjectileBehavior.Fireball
                    or AuthoritativeProjectileBehavior.HolyBolt
                    or AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.FireWallSegment
                    or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment
                    or AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment
                    or AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt)
                && (definition.RandomDamageIterationsCharacterLevelDivisor != 0
                    || definition.RandomDamageIterationsBonus != 0
                    || definition.SpellEffectScaleDivisor != 0)) {
                throw new InvalidDataException(
                    "Repeated random-damage scaling requires a supported projectile behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Flash
                    or AuthoritativeProjectileBehavior.Fireball
                    or AuthoritativeProjectileBehavior.HolyBolt)
                && definition.CharacterLevelDamageDivisor != 0) {
                throw new InvalidDataException(
                    "Character-level damage scaling requires Flash, Fireball, or HolyBolt projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Flash
                && definition.PostScaleDamagePercent != 0) {
                throw new InvalidDataException(
                    "Post-scale percentage damage requires Flash projectile behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Healing
                    or AuthoritativeProjectileBehavior.HealOther)
                && (definition.RandomHealingRange != 0
                    || definition.CharacterLevelRandomHealingRange != 0
                    || definition.CharacterLevelHealing != 0
                    || definition.SpellLevelRandomHealingRange != 0
                    || definition.SpellLevelHealing != 0
                    || definition.HealingScale != 1)) {
                throw new InvalidDataException(
                    "Native random healing tuning requires Healing or HealOther behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Lightning
                    or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment
                    or AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment
                    or AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt)
                && (definition.CharacterLevelRandomDamageDivisor != 0
                    || definition.SecondaryProjectileLifetime != 0
                    || definition.SecondaryProjectileLifetimeSpellLevelDivisor != 0)) {
                throw new InvalidDataException(
                    "Secondary projectile lifetime tuning requires a controller behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Lightning
                    or AuthoritativeProjectileBehavior.Fireball
                    or AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.FireWallSegment
                    or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment
                    or AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment
                    or AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt)
                && definition.DamageScale != 1) {
                throw new InvalidDataException(
                    "Scaled native projectile damage requires Lightning, Fireball, Fire Wall, Flame Wave, Inferno, or Chain Lightning behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Fireball
                    or AuthoritativeProjectileBehavior.HolyBolt)
                && (definition.ProjectileSpeedSpellLevelStep != 0
                    || definition.ProjectileSpeedMaximumBonus != 0)) {
                throw new InvalidDataException(
                    "Spell-level projectile-speed tuning requires Fireball or HolyBolt behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.ChargedBolt
                    or AuthoritativeProjectileBehavior.Firebolt
                    or AuthoritativeProjectileBehavior.HolyBolt
                    or AuthoritativeProjectileBehavior.BloodStar
                    or AuthoritativeProjectileBehavior.BoneSpirit
                    or AuthoritativeProjectileBehavior.Lightning
                    or AuthoritativeProjectileBehavior.Flash
                    or AuthoritativeProjectileBehavior.StoneCurse
                    or AuthoritativeProjectileBehavior.Fireball
                    or AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.FireWallSegment
                    or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment
                    or AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment
                    or AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt)
                && (definition.ResistantDamageDivisor != 1
                    || definition.ProjectileImpactLifetime != 1)) {
                throw new InvalidDataException(
                    "Native projectile tuning requires a native projectile behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.FireWallSegment
                    or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment)
                && (definition.WallControlLifetime != 0
                    || definition.WallSegmentBaseDuration != 0
                    || definition.WallSegmentSpellLevelDurationStep != 0
                    || definition.WallSegmentCharacterLevelDurationStep != 0
                    || definition.WallSegmentDurationScale != 0
                    || definition.WallCharacterLevelDamageMultiplier != 0)) {
                throw new InvalidDataException(
                    "Fire Wall duration and character-damage tuning requires Fire Wall behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment)
                && (definition.FlameWaveControllerLifetime != 0
                    || definition.FlameWaveBranchBase != 0
                    || definition.FlameWaveBranchSpellLevelDivisor != 0
                    || definition.FlameWaveCharacterLevelDamageBonus != 0)) {
                throw new InvalidDataException(
                    "Flame Wave controller and character-damage tuning requires Flame Wave behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment)
                && definition.InfernoSegmentLifetimeStep != 0) {
                throw new InvalidDataException(
                    "Inferno segment lifetime tuning requires Inferno behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt)
                && (definition.ChainLightningSearchRadiusBonus != 0
                    || definition.ChainLightningControllerLifetime != 0)) {
                throw new InvalidDataException(
                    "Chain Lightning tuning requires Chain Lightning behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Resurrection
                && (definition.ResurrectionLife != 0
                    || definition.ResurrectionLifeScale != 1
                    || definition.ResurrectionSearchRadius != 0)) {
                throw new InvalidDataException(
                    "Resurrection tuning requires Resurrection projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.ManaShield
                && (definition.ManaShieldBaseDamageReductionDivisor != 0
                    || definition.ManaShieldDamageReductionLevelStep != 0
                    || definition.ManaShieldDamageReductionMaximumLevel != 0)) {
                throw new InvalidDataException(
                    "Mana Shield tuning requires ManaShield projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.StoneCurse
                && (definition.StoneCurseBaseDuration != 0
                    || definition.StoneCurseMaximumDuration != 0
                    || definition.StoneCurseDurationScale != 0
                    || definition.StoneCurseSearchRadius != 0)) {
                throw new InvalidDataException(
                    "Stone Curse tuning requires StoneCurse projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Infravision
                && definition.InfravisionDurationGrowthDivisor != 0) {
                throw new InvalidDataException(
                    "Infravision duration tuning requires Infravision projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Search
                && (definition.SearchCharacterLevelDurationMultiplier != 0
                    || definition.SearchSpellLevelDurationMultiplier != 0)) {
                throw new InvalidDataException(
                    "Search duration tuning requires Search projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Phasing
                && (definition.PhasingMinimumAxisOffset != 0
                    || definition.PhasingMaximumAxisOffset != 0)) {
                throw new InvalidDataException(
                    "Phasing offset tuning requires Phasing projectile behavior.");
            }
            if (definition.StatusEffectId != 0 && definition.StatusDuration == 0)
                throw new InvalidDataException("Status-bearing spells require a positive status duration.");
            if (!spells.TryAdd(definition.SpellId, definition))
                throw new InvalidDataException($"Spell {definition.SpellId} is defined more than once.");
        }
        if (spells.Count == 0)
            throw new InvalidDataException("The spell catalog cannot be empty.");
        if (spells.Values.Count(definition =>
                definition.ProjectileBehavior == AuthoritativeProjectileBehavior.ManaShield) > 1) {
            throw new InvalidDataException(
                "The spell catalog can define at most one Mana Shield behavior.");
        }
        if (spells.Values.Count(definition =>
                definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Infravision) > 1) {
            throw new InvalidDataException(
                "The spell catalog can define at most one Infravision behavior.");
        }
        if (spells.Values.Count(definition =>
                definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Search) > 1) {
            throw new InvalidDataException(
                "The spell catalog can define at most one Search behavior.");
        }
        if (spells.Values.Count(definition =>
                definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Phasing) > 1) {
            throw new InvalidDataException(
                "The spell catalog can define at most one Phasing behavior.");
        }
        if (spells.Values.Count(definition =>
                definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Fireball) > 1) {
            throw new InvalidDataException(
                "The spell catalog can define at most one Fireball behavior.");
        }
        if (spells.Values.Count(definition =>
                definition.ProjectileBehavior == AuthoritativeProjectileBehavior.HealOther) > 1) {
            throw new InvalidDataException(
                "The spell catalog can define at most one Heal Other behavior.");
        }
    }

    public IReadOnlyList<AuthoritativeSpellDefinition> Definitions => spells.Values.OrderBy(spell => spell.SpellId).ToArray();

    public bool TryGet(uint spellId, out AuthoritativeSpellDefinition definition) => spells.TryGetValue(spellId, out definition!);

    public static AuthoritativeSpellCatalog LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeSpellCatalog(table.Rows.Select(row => new AuthoritativeSpellDefinition(
            row.RequiredUInt32("spell_id"),
            row.RequiredInt32("mana_cost"),
            row.OptionalInt32("healing_amount"),
            row.OptionalUInt32("status_effect_id"),
            row.OptionalUInt32("status_duration"),
            row.OptionalInt32("status_magnitude")) {
            DamageAmount = row.OptionalInt32("damage_amount"),
            ManaAdjustment = row.OptionalInt32("mana_adjustment"),
            ManaAdjustmentDivisor = row.OptionalInt32("mana_adjustment_divisor", 1),
            CharacterLevelManaCost = row.OptionalInt32("character_level_mana_cost"),
            MinimumManaCost = row.OptionalInt32("minimum_mana_cost"),
            ManaCostScale = row.OptionalInt32("mana_cost_scale", 1),
            Range = row.OptionalInt32("range", 1),
            AreaRadius = row.OptionalInt32("area_radius"),
            DamageType = ParseDamageType(row),
            ProjectileSpeed = row.OptionalInt32("projectile_speed"),
            ProjectileLifetime = row.OptionalInt32("projectile_lifetime", 32),
            ProjectileCount = row.OptionalInt32("projectile_count", 1),
            ProjectileCountLevelDivisor = row.OptionalInt32("projectile_count_level_divisor"),
            ProjectileBehavior = ParseProjectileBehavior(row),
            RandomMagicDamageDivisor = row.OptionalInt32("random_magic_damage_divisor"),
            RandomDamageRange = row.OptionalInt32("random_damage_range"),
            MagicDamageDivisor = row.OptionalInt32("magic_damage_divisor"),
            MagicDamageReductionDivisor = row.OptionalInt32("magic_damage_reduction_divisor"),
            SpellLevelDamage = row.OptionalInt32("spell_level_damage"),
            DamageBonus = row.OptionalInt32("damage_bonus"),
            SelfDamage = row.OptionalInt32("self_damage"),
            CharacterLevelRandomDamageDivisor = row.OptionalInt32(
                "character_level_random_damage_divisor"),
            DamageScale = row.OptionalInt32("damage_scale", 1),
            ProjectileSpeedSpellLevelStep = row.OptionalInt32(
                "projectile_speed_spell_level_step"),
            ProjectileSpeedMaximumBonus = row.OptionalInt32(
                "projectile_speed_maximum_bonus"),
            RandomDamageIterationsCharacterLevelDivisor = row.OptionalInt32(
                "random_damage_iterations_character_level_divisor"),
            RandomDamageIterationsBonus = row.OptionalInt32(
                "random_damage_iterations_bonus"),
            CharacterLevelDamageDivisor = row.OptionalInt32(
                "character_level_damage_divisor"),
            SpellEffectScaleDivisor = row.OptionalInt32(
                "spell_effect_scale_divisor"),
            PostScaleDamagePercent = row.OptionalInt32(
                "post_scale_damage_percent"),
            RandomHealingRange = row.OptionalInt32("random_healing_range"),
            CharacterLevelRandomHealingRange = row.OptionalInt32(
                "character_level_random_healing_range"),
            CharacterLevelHealing = row.OptionalInt32("character_level_healing"),
            SpellLevelRandomHealingRange = row.OptionalInt32(
                "spell_level_random_healing_range"),
            SpellLevelHealing = row.OptionalInt32("spell_level_healing"),
            HealingScale = row.OptionalInt32("healing_scale", 1),
            SecondaryProjectileLifetime = row.OptionalInt32(
                "secondary_projectile_lifetime"),
            SecondaryProjectileLifetimeSpellLevelDivisor = row.OptionalInt32(
                "secondary_projectile_lifetime_spell_level_divisor"),
            WallControlLifetime = row.OptionalInt32("wall_control_lifetime"),
            WallSegmentBaseDuration = row.OptionalInt32("wall_segment_base_duration"),
            WallSegmentSpellLevelDurationStep = row.OptionalInt32(
                "wall_segment_spell_level_duration_step"),
            WallSegmentCharacterLevelDurationStep = row.OptionalInt32(
                "wall_segment_character_level_duration_step"),
            WallSegmentDurationScale = row.OptionalInt32("wall_segment_duration_scale"),
            WallCharacterLevelDamageMultiplier = row.OptionalInt32(
                "wall_character_level_damage_multiplier"),
            FlameWaveControllerLifetime = row.OptionalInt32(
                "flame_wave_controller_lifetime"),
            FlameWaveBranchBase = row.OptionalInt32("flame_wave_branch_base"),
            FlameWaveBranchSpellLevelDivisor = row.OptionalInt32(
                "flame_wave_branch_spell_level_divisor"),
            FlameWaveCharacterLevelDamageBonus = row.OptionalInt32(
                "flame_wave_character_level_damage_bonus"),
            InfernoSegmentLifetimeStep = row.OptionalInt32("inferno_segment_lifetime_step"),
            ChainLightningSearchRadiusBonus = row.OptionalInt32(
                "chain_lightning_search_radius_bonus"),
            ChainLightningControllerLifetime = row.OptionalInt32(
                "chain_lightning_controller_lifetime"),
            ResistantDamageDivisor = row.OptionalInt32("resistant_damage_divisor", 1),
            ProjectileImpactLifetime = row.OptionalInt32("projectile_impact_lifetime", 1),
            ResurrectionLife = row.OptionalInt32("resurrection_life"),
            ResurrectionLifeScale = row.OptionalInt32("resurrection_life_scale", 1),
            ResurrectionSearchRadius = row.OptionalInt32("resurrection_search_radius"),
            ManaShieldBaseDamageReductionDivisor = row.OptionalInt32(
                "mana_shield_base_damage_reduction_divisor"),
            ManaShieldDamageReductionLevelStep = row.OptionalInt32(
                "mana_shield_damage_reduction_level_step"),
            ManaShieldDamageReductionMaximumLevel = row.OptionalInt32(
                "mana_shield_damage_reduction_maximum_level"),
            StoneCurseBaseDuration = row.OptionalInt32("stone_curse_base_duration"),
            StoneCurseMaximumDuration = row.OptionalInt32("stone_curse_maximum_duration"),
            StoneCurseDurationScale = row.OptionalInt32("stone_curse_duration_scale"),
            StoneCurseSearchRadius = row.OptionalInt32("stone_curse_search_radius"),
            InfravisionDurationGrowthDivisor = row.OptionalInt32(
                "infravision_duration_growth_divisor"),
            SearchCharacterLevelDurationMultiplier = row.OptionalInt32(
                "search_character_level_duration_multiplier"),
            SearchSpellLevelDurationMultiplier = row.OptionalInt32(
                "search_spell_level_duration_multiplier"),
            PhasingMinimumAxisOffset = row.OptionalInt32(
                "phasing_minimum_axis_offset"),
            PhasingMaximumAxisOffset = row.OptionalInt32(
                "phasing_maximum_axis_offset"),
            StaffLevel = row.OptionalInt32("staff_level"),
        }));
    }

    private static AuthoritativeDamageType ParseDamageType(TsvRow row)
    {
        if (!row.TryGet("damage_type", out var value) || string.IsNullOrWhiteSpace(value))
            return AuthoritativeDamageType.Physical;
        if (Enum.TryParse<AuthoritativeDamageType>(value, ignoreCase: true, out var damageType))
            return damageType;
        throw new InvalidDataException($"Spell row {row.LineNumber} contains an unknown damage type '{value}'.");
    }

    private static AuthoritativeProjectileBehavior ParseProjectileBehavior(TsvRow row)
    {
        if (!row.TryGet("projectile_behavior", out var value)
            || string.IsNullOrWhiteSpace(value)) {
            return AuthoritativeProjectileBehavior.Targeted;
        }
        if (Enum.TryParse<AuthoritativeProjectileBehavior>(
                value,
                ignoreCase: true,
                out var behavior)) {
            return behavior;
        }
        throw new InvalidDataException(
            $"Spell row {row.LineNumber} contains an unknown projectile behavior '{value}'.");
    }
}
