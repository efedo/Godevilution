using Devilution.Data;

namespace Devilution.Diablo;

/** Native-compatible monster type metadata supplied by validated content. */
public sealed record AuthoritativeMonsterTypeDefinition(
    uint MonsterId,
    uint NativeMonsterId,
    string Name,
    int MinimumDungeonLevel,
    int MaximumDungeonLevel,
    int MonsterLevel,
    int MinimumHitPoints,
    int MaximumHitPoints,
    int ArmorClass,
    int MinimumDamage,
    int MaximumDamage,
    int MinimumSpecialDamage,
    int MaximumSpecialDamage,
    bool StoneCurseImmune,
    bool Undead,
    bool BerserkImmune)
{
    /** Native monster class capability used by demon-specific equipment. */
    public bool Demon { get; init; }

    /** Native monster class capability used by sword/mace damage modifiers. */
    public bool Animal { get; init; }

    /** Native monster ability that displaces a player after a successful hit. */
    public bool Knockback { get; init; }

    /** Native resistance percentages derived from the active monster data. */
    public int FireResistance { get; init; }
    public int LightningResistance { get; init; }
    public int MagicResistance { get; init; }
    public int AcidResistance { get; init; }

    /** Native aggro radius used by autonomous monster behavior. */
    public int AggroRange { get; init; } = 6;

    /** Native death-animation duration in authoritative game ticks. */
    public int DeathAnimationTicks { get; init; }

    /** Native experience awarded when this monster is defeated. */
    public uint ExperienceReward { get; init; }

    /** Native normal-attack chance to hit before level, difficulty, and armor adjustments. */
    public int ToHit { get; init; }

    /** Native special-attack chance to hit before level, difficulty, and armor adjustments. */
    public int ToHitSpecial { get; init; }

    /** Native MonsterAIID dispatch identity used by incremental authoritative AI ports. */
    public string NativeAi { get; init; } = "Zombie";

    /** Native intelligence value used by AI-specific probability bands. */
    public int Intelligence { get; init; }

    /** Native Fallen fear radius; zero preserves the legacy compatibility path. */
    public int FallenFearRadius { get; init; }

    /** Native Fallen fear level offset used by its retreat countdown. */
    public int FallenFearLevelOffset { get; init; }

    /** Native minimum Fallen fear countdown in authoritative ticks. */
    public int FallenFearMinimumTicks { get; init; }

    /** Native Sneak preferred-distance base before intelligence scaling. */
    public int SneakDistanceBase { get; init; }

    /** Native Sneak preferred-distance reduction per intelligence point. */
    public int SneakDistanceIntelligenceScale { get; init; }

    /** Native Sneak distance beyond its preferred band that ends retreat. */
    public int SneakRetreatReleaseDistance { get; init; }

    /** Native Sneak retreat duration after taking a successful hit. */
    public int SneakRetreatTicks { get; init; }

    /** Native Bat adjacent attack chance base before intelligence scaling. */
    public int BatAttackChanceBase { get; init; }

    /** Native Bat adjacent attack chance increase per intelligence point. */
    public int BatAttackChanceIntelligenceScale { get; init; }

    /** Native Bat post-hit retreat duration in authoritative ticks. */
    public int BatRetreatDurationTicks { get; init; }

    /** Native SkeletonMelee movement-delay chance base before intelligence scaling. */
    public int SkeletonMeleeDelayChanceBase { get; init; }

    /** Native SkeletonMelee movement-delay chance reduction per intelligence point. */
    public int SkeletonMeleeDelayChanceIntelligenceScale { get; init; }

    /** Native SkeletonMelee adjacent attack chance base before intelligence scaling. */
    public int SkeletonMeleeAttackChanceBase { get; init; }

    /** Native SkeletonMelee adjacent attack chance increase per intelligence point. */
    public int SkeletonMeleeAttackChanceIntelligenceScale { get; init; }

    /** Native GoatMelee movement admission chance base before intelligence scaling. */
    public int GoatMeleeMovementChanceBase { get; init; }

    /** Native GoatMelee movement admission chance increase per intelligence point. */
    public int GoatMeleeMovementChanceIntelligenceScale { get; init; }

    /** Native GoatMelee adjacent attack chance base before intelligence scaling. */
    public int GoatMeleeAttackChanceBase { get; init; }

    /** Native GoatMelee adjacent attack chance increase per intelligence point. */
    public int GoatMeleeAttackChanceIntelligenceScale { get; init; }

    /** Native GoatMelee wounded-health threshold for its special attack. */
    public int GoatMeleeSpecialHealthThresholdPercent { get; init; }

    /** Native GoatMelee random-group period for its wounded special attack. */
    public int GoatMeleeSpecialChanceGroupPeriod { get; init; }

    /** Native Fat movement admission chance base before intelligence scaling. */
    public int FatMovementChanceBase { get; init; }

    /** Native Fat movement admission chance increase per intelligence point. */
    public int FatMovementChanceIntelligenceScale { get; init; }

    /** Native Fat adjacent attack chance base before intelligence scaling. */
    public int FatAttackChanceBase { get; init; }

    /** Native Fat adjacent attack chance increase per intelligence point. */
    public int FatAttackChanceIntelligenceScale { get; init; }

    /** Native Fat special-attack lower-band chance base before intelligence scaling. */
    public int FatSpecialChanceBase { get; init; }

    /** Native Fat special-attack lower-band chance increase per intelligence point. */
    public int FatSpecialChanceIntelligenceScale { get; init; }

    /** Native Zombie action admission chance base before intelligence scaling. */
    public int ZombieActionChanceBase { get; init; }

    /** Native Zombie action admission chance increase per intelligence point. */
    public int ZombieActionChanceIntelligenceScale { get; init; }

    /** Native Zombie random-direction prepend chance base before intelligence scaling. */
    public int ZombieRandomDirectionChanceBase { get; init; }

    /** Native Zombie random-direction prepend chance increase per intelligence point. */
    public int ZombieRandomDirectionChanceIntelligenceScale { get; init; }

    /** Native minimum Zombie long-range movement distance. */
    public int ZombieMovementDistanceMinimum { get; init; }

    /** Native Zombie long-range movement distance base before intelligence scaling. */
    public int ZombieMovementDistanceBase { get; init; }

    /** Native Zombie long-range movement distance increase per intelligence point. */
    public int ZombieMovementDistanceIntelligenceScale { get; init; }

    /** Native SkeletonKing minimum distance required for summon admission. */
    public int SkeletonKingSummonMinimumDistance { get; init; }

    /** Native SkeletonKing summon chance base before intelligence scaling. */
    public int SkeletonKingSummonChanceBase { get; init; }

    /** Native SkeletonKing summon chance increase per intelligence point. */
    public int SkeletonKingSummonChanceIntelligenceScale { get; init; }

    /** Native HorkDemon adjacent attack chance base before intelligence scaling. */
    public int HorkDemonAttackChanceBase { get; init; }

    /** Native HorkDemon adjacent attack chance increase per intelligence point. */
    public int HorkDemonAttackChanceIntelligenceScale { get; init; }

    /** Native HorkDemon minimum distance required for Hork Spawn admission. */
    public int HorkDemonSpawnMinimumDistance { get; init; }

    /** Native HorkDemon Hork Spawn chance base before intelligence scaling. */
    public int HorkDemonSpawnChanceBase { get; init; }

    /** Native HorkDemon Hork Spawn chance increase per intelligence point. */
    public int HorkDemonSpawnChanceIntelligenceScale { get; init; }

    /** Native HorkDemon non-adjacent movement chance base before intelligence scaling. */
    public int HorkDemonMovementChanceBase { get; init; }

    /** Native HorkDemon non-adjacent movement chance increase per intelligence point. */
    public int HorkDemonMovementChanceIntelligenceScale { get; init; }

    /** Native Gargoyle safety-boundary base before intelligence scaling. */
    public int GargoyleSafeDistanceBase { get; init; }

    /** Native Gargoyle safety-boundary increase per intelligence point. */
    public int GargoyleSafeDistanceIntelligenceScale { get; init; }

    /** Native Gargoyle wounded-health threshold for self-healing. */
    public int GargoyleHealingHealthThresholdPercent { get; init; }

    /** Native Gargoyle self-heal divisor multiplier. */
    public int GargoyleHealDivisorMultiplier { get; init; }

    /** Native Gargoyle self-heal divisor random-range modulus. */
    public int GargoyleHealDivisorRandomRange { get; init; }

    /** Native Gargoyle self-heal divisor random offset. */
    public int GargoyleHealDivisorRandomOffset { get; init; }

    /** Native Scavenger corpse-eating duration in authoritative ticks. */
    public int ScavengerHealingDurationTicks { get; init; }

    /** Native Scavenger corpse search radius. */
    public int ScavengerCorpseSearchRadius { get; init; }

    /** Native Hellfire Scavenger healing maximum-health divisor. */
    public int ScavengerHellfireHealingDivisor { get; init; }

    /** Native base-game Scavenger healing amount per corpse-eating tick. */
    public int ScavengerBaseHealingAmount { get; init; }

    /** Native base-game Scavenger target-health percentage. */
    public int ScavengerBaseHealingTargetPercent { get; init; }

    /** Native Scavenger wounded-health threshold for corpse seeking. */
    public int ScavengerHealingHealthThresholdPercent { get; init; }

    /** Native SkeletonRanged minimum distance for retreat admission. */
    public int SkeletonRangedRetreatMinimumDistance { get; init; }

    /** Native SkeletonRanged retreat chance base before intelligence scaling. */
    public int SkeletonRangedRetreatChanceBase { get; init; }

    /** Native SkeletonRanged retreat chance increase per intelligence point. */
    public int SkeletonRangedRetreatChanceIntelligenceScale { get; init; }

    /** Native SkeletonRanged arrow attack chance base before intelligence scaling. */
    public int SkeletonRangedAttackChanceBase { get; init; }

    /** Native SkeletonRanged arrow attack chance increase per intelligence point. */
    public int SkeletonRangedAttackChanceIntelligenceScale { get; init; }

    /** Native Rhino minimum distance required for charge admission. */
    public int RhinoChargeMinimumDistance { get; init; }

    /** Native Rhino charge chance base before intelligence scaling. */
    public int RhinoChargeChanceBase { get; init; }

    /** Native Rhino charge chance increase per intelligence point. */
    public int RhinoChargeChanceIntelligenceScale { get; init; }
}

/** Validated monster type metadata used to admit external encounter rows. */
public sealed class AuthoritativeMonsterTypeCatalog
{
    private static readonly HashSet<string> ValidNativeAis = new(StringComparer.Ordinal) {
        "Acid", "AcidUnique", "ArchLich", "Bat", "BoneDemon", "Butcher", "Counselor", "Diablo",
        "Fallen", "Fat", "FireBat", "FireMan", "Gargoyle", "GoatMelee", "GoatRanged",
        "Golem", "HorkDemon", "Lich", "Magma", "Mega", "Necromorb", "Psychorb", "Rhino", "Scavenger",
        "SkeletonKing", "SkeletonMelee", "SkeletonRanged", "Snake", "Sneak", "Storm",
        "Succubus", "LazarusSuccubus", "Torchant", "Zombie",
    };

    private readonly Dictionary<uint, AuthoritativeMonsterTypeDefinition> definitions;
    private readonly Dictionary<uint, AuthoritativeMonsterTypeDefinition> definitionsByNativeId;
    private readonly HashSet<uint> ambiguousNativeIds;

    public AuthoritativeMonsterTypeCatalog(
        IEnumerable<AuthoritativeMonsterTypeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.definitions = new Dictionary<uint, AuthoritativeMonsterTypeDefinition>();
        definitionsByNativeId = new Dictionary<uint, AuthoritativeMonsterTypeDefinition>();
        ambiguousNativeIds = new HashSet<uint>();
        foreach (var definition in definitions) {
            if (definition.MonsterId == 0
                || string.IsNullOrWhiteSpace(definition.Name)
                || definition.MinimumDungeonLevel < 0
                || definition.MaximumDungeonLevel < definition.MinimumDungeonLevel
                || definition.MonsterLevel < 0
                || definition.MinimumHitPoints <= 0
                || definition.MaximumHitPoints < definition.MinimumHitPoints
                || definition.ArmorClass < 0
                || definition.MinimumDamage < 0
                || definition.MaximumDamage < definition.MinimumDamage
                || definition.MinimumSpecialDamage < 0
                || definition.MaximumSpecialDamage < definition.MinimumSpecialDamage
                || definition.FireResistance is < -100 or > 100
                || definition.LightningResistance is < -100 or > 100
                || definition.MagicResistance is < -100 or > 100
                || definition.AcidResistance is < -100 or > 100
                || definition.AggroRange is < 0 or > 64
                || definition.DeathAnimationTicks is < 0 or > 1024
                || definition.ExperienceReward > 1_000_000
                || definition.ToHit is < 0 or > 255
                || definition.ToHitSpecial is < 0 or > 255
                || definition.Intelligence is < 0 or > 255
                || !IsValidNativeAi(definition.NativeAi)
                || definition.FallenFearRadius is < 0 or > 64
                || definition.FallenFearLevelOffset is < 0 or > 255
                || definition.FallenFearMinimumTicks is < 0 or > 255
                || (definition.NativeAi == "Fallen"
                    && ((definition.FallenFearRadius != 0
                            || definition.FallenFearLevelOffset != 0
                            || definition.FallenFearMinimumTicks != 0)
                        && (definition.FallenFearRadius <= 0
                            || definition.FallenFearLevelOffset <= 0
                            || definition.FallenFearMinimumTicks <= 0)))
                || (definition.NativeAi != "Fallen"
                    && (definition.FallenFearRadius != 0
                        || definition.FallenFearLevelOffset != 0
                        || definition.FallenFearMinimumTicks != 0))
                || definition.SneakDistanceBase is < 0 or > 64
                || definition.SneakDistanceIntelligenceScale is < 0 or > 64
                || definition.SneakRetreatReleaseDistance is < 0 or > 64
                || definition.SneakRetreatTicks is < 0 or > 64
                || (definition.NativeAi == "Sneak"
                    && ((definition.SneakDistanceBase != 0
                            || definition.SneakDistanceIntelligenceScale != 0
                            || definition.SneakRetreatReleaseDistance != 0
                            || definition.SneakRetreatTicks != 0)
                        && (definition.SneakDistanceBase <= 0
                            || definition.SneakDistanceIntelligenceScale <= 0
                            || definition.SneakRetreatReleaseDistance <= 0
                            || definition.SneakRetreatTicks <= 0)))
                || (definition.NativeAi != "Sneak"
                    && (definition.SneakDistanceBase != 0
                        || definition.SneakDistanceIntelligenceScale != 0
                        || definition.SneakRetreatReleaseDistance != 0
                        || definition.SneakRetreatTicks != 0))
                || definition.BatAttackChanceBase is < 0 or > 100
                || definition.BatAttackChanceIntelligenceScale is < 0 or > 100
                || ((definition.BatAttackChanceBase != 0
                        || definition.BatAttackChanceIntelligenceScale != 0)
                    && (definition.BatAttackChanceBase <= 0
                        || definition.BatAttackChanceIntelligenceScale <= 0))
                || definition.BatRetreatDurationTicks is < 0 or > 64
                || definition.SkeletonMeleeDelayChanceBase is < 0 or > 100
                || definition.SkeletonMeleeDelayChanceIntelligenceScale is < 0 or > 100
                || ((definition.SkeletonMeleeDelayChanceBase != 0
                        || definition.SkeletonMeleeDelayChanceIntelligenceScale != 0)
                    && (definition.SkeletonMeleeDelayChanceBase <= 0
                        || definition.SkeletonMeleeDelayChanceIntelligenceScale <= 0))
                || definition.SkeletonMeleeAttackChanceBase is < 0 or > 100
                || definition.SkeletonMeleeAttackChanceIntelligenceScale is < 0 or > 100
                || ((definition.SkeletonMeleeAttackChanceBase != 0
                        || definition.SkeletonMeleeAttackChanceIntelligenceScale != 0)
                    && (definition.SkeletonMeleeAttackChanceBase <= 0
                        || definition.SkeletonMeleeAttackChanceIntelligenceScale <= 0))
                || definition.GoatMeleeMovementChanceBase is < 0 or > 100
                || definition.GoatMeleeMovementChanceIntelligenceScale is < 0 or > 100
                || ((definition.GoatMeleeMovementChanceBase != 0
                        || definition.GoatMeleeMovementChanceIntelligenceScale != 0)
                    && (definition.GoatMeleeMovementChanceBase <= 0
                        || definition.GoatMeleeMovementChanceIntelligenceScale <= 0))
                || definition.GoatMeleeAttackChanceBase is < 0 or > 100
                || definition.GoatMeleeAttackChanceIntelligenceScale is < 0 or > 100
                || ((definition.GoatMeleeAttackChanceBase != 0
                        || definition.GoatMeleeAttackChanceIntelligenceScale != 0)
                    && (definition.GoatMeleeAttackChanceBase <= 0
                        || definition.GoatMeleeAttackChanceIntelligenceScale <= 0))
                || definition.GoatMeleeSpecialHealthThresholdPercent is < 0 or > 100
                || definition.GoatMeleeSpecialChanceGroupPeriod is < 0 or > 256
                || ((definition.GoatMeleeSpecialHealthThresholdPercent != 0
                        || definition.GoatMeleeSpecialChanceGroupPeriod != 0)
                    && (definition.GoatMeleeSpecialHealthThresholdPercent <= 0
                        || definition.GoatMeleeSpecialChanceGroupPeriod <= 0))
                || definition.FatMovementChanceBase is < 0 or > 100
                || definition.FatMovementChanceIntelligenceScale is < 0 or > 100
                || ((definition.FatMovementChanceBase != 0
                        || definition.FatMovementChanceIntelligenceScale != 0)
                    && (definition.FatMovementChanceBase <= 0
                        || definition.FatMovementChanceIntelligenceScale <= 0))
                || definition.FatAttackChanceBase is < 0 or > 100
                || definition.FatAttackChanceIntelligenceScale is < 0 or > 100
                || definition.FatSpecialChanceBase is < 0 or > 100
                || definition.FatSpecialChanceIntelligenceScale is < 0 or > 100
                || ((definition.FatAttackChanceBase != 0
                        || definition.FatAttackChanceIntelligenceScale != 0
                        || definition.FatSpecialChanceBase != 0
                        || definition.FatSpecialChanceIntelligenceScale != 0)
                    && (definition.FatAttackChanceBase <= 0
                        || definition.FatAttackChanceIntelligenceScale <= 0
                        || definition.FatSpecialChanceBase <= 0
                        || definition.FatSpecialChanceIntelligenceScale <= 0))
                || definition.ZombieActionChanceBase is < 0 or > 100
                || definition.ZombieActionChanceIntelligenceScale is < 0 or > 100
                || ((definition.ZombieActionChanceBase != 0
                        || definition.ZombieActionChanceIntelligenceScale != 0)
                    && (definition.ZombieActionChanceBase <= 0
                        || definition.ZombieActionChanceIntelligenceScale <= 0))
                || definition.ZombieRandomDirectionChanceBase is < 0 or > 100
                || definition.ZombieRandomDirectionChanceIntelligenceScale is < 0 or > 100
                || ((definition.ZombieRandomDirectionChanceBase != 0
                        || definition.ZombieRandomDirectionChanceIntelligenceScale != 0)
                    && (definition.ZombieRandomDirectionChanceBase <= 0
                        || definition.ZombieRandomDirectionChanceIntelligenceScale <= 0))
                || definition.ZombieMovementDistanceMinimum is < 0 or > 64
                || definition.ZombieMovementDistanceBase is < 0 or > 64
                || definition.ZombieMovementDistanceIntelligenceScale is < 0 or > 64
                || ((definition.ZombieMovementDistanceMinimum != 0
                        || definition.ZombieMovementDistanceBase != 0
                        || definition.ZombieMovementDistanceIntelligenceScale != 0)
                    && (definition.ZombieMovementDistanceMinimum <= 0
                        || definition.ZombieMovementDistanceBase <= 0
                        || definition.ZombieMovementDistanceIntelligenceScale <= 0))
                || definition.SkeletonKingSummonMinimumDistance is < 0 or > 64
                || definition.SkeletonKingSummonChanceBase is < 0 or > 100
                || definition.SkeletonKingSummonChanceIntelligenceScale is < 0 or > 100
                || ((definition.SkeletonKingSummonMinimumDistance != 0
                        || definition.SkeletonKingSummonChanceBase != 0
                        || definition.SkeletonKingSummonChanceIntelligenceScale != 0)
                    && (definition.SkeletonKingSummonMinimumDistance <= 0
                        || definition.SkeletonKingSummonChanceBase <= 0
                        || definition.SkeletonKingSummonChanceIntelligenceScale <= 0))
                || definition.HorkDemonAttackChanceBase is < 0 or > 100
                || definition.HorkDemonAttackChanceIntelligenceScale is < 0 or > 100
                || definition.HorkDemonSpawnMinimumDistance is < 0 or > 64
                || definition.HorkDemonSpawnChanceBase is < 0 or > 100
                || definition.HorkDemonSpawnChanceIntelligenceScale is < 0 or > 100
                || definition.HorkDemonMovementChanceBase is < 0 or > 100
                || definition.HorkDemonMovementChanceIntelligenceScale is < 0 or > 100
                || ((definition.HorkDemonAttackChanceBase != 0
                        || definition.HorkDemonAttackChanceIntelligenceScale != 0
                        || definition.HorkDemonSpawnMinimumDistance != 0
                        || definition.HorkDemonSpawnChanceBase != 0
                        || definition.HorkDemonSpawnChanceIntelligenceScale != 0
                        || definition.HorkDemonMovementChanceBase != 0
                        || definition.HorkDemonMovementChanceIntelligenceScale != 0)
                    && (definition.HorkDemonAttackChanceBase <= 0
                        || definition.HorkDemonAttackChanceIntelligenceScale <= 0
                        || definition.HorkDemonSpawnMinimumDistance <= 0
                        || definition.HorkDemonSpawnChanceBase <= 0
                        || definition.HorkDemonSpawnChanceIntelligenceScale <= 0
                        || definition.HorkDemonMovementChanceBase <= 0
                        || definition.HorkDemonMovementChanceIntelligenceScale <= 0))
                || definition.GargoyleSafeDistanceBase is < 0 or > 64
                || definition.GargoyleSafeDistanceIntelligenceScale is < 0 or > 64
                || ((definition.GargoyleSafeDistanceBase != 0
                        || definition.GargoyleSafeDistanceIntelligenceScale != 0)
                    && (definition.GargoyleSafeDistanceBase <= 0
                        || definition.GargoyleSafeDistanceIntelligenceScale <= 0))
                || definition.GargoyleHealingHealthThresholdPercent is < 0 or > 100
                || definition.GargoyleHealDivisorMultiplier is < 0 or > 256
                || definition.GargoyleHealDivisorRandomRange is < 0 or > 256
                || definition.GargoyleHealDivisorRandomOffset is < 0 or > 256
                || ((definition.GargoyleHealDivisorMultiplier != 0
                        || definition.GargoyleHealDivisorRandomRange != 0
                        || definition.GargoyleHealDivisorRandomOffset != 0)
                    && (definition.GargoyleHealDivisorMultiplier <= 0
                        || definition.GargoyleHealDivisorRandomRange <= 0))
                || definition.ScavengerHealingDurationTicks is < 0 or > 64
                || definition.ScavengerCorpseSearchRadius is < 0 or > 64
                || definition.ScavengerHellfireHealingDivisor is < 0 or > 256
                || definition.ScavengerBaseHealingAmount is < 0 or > 256
                || definition.ScavengerBaseHealingTargetPercent is < 0 or > 100
                || definition.ScavengerHealingHealthThresholdPercent is < 0 or > 100
                || (definition.NativeAi == "Scavenger"
                    && ((definition.ScavengerHealingDurationTicks != 0
                            || definition.ScavengerCorpseSearchRadius != 0
                            || definition.ScavengerHellfireHealingDivisor != 0
                            || definition.ScavengerBaseHealingAmount != 0
                            || definition.ScavengerBaseHealingTargetPercent != 0)
                        && (definition.ScavengerHealingDurationTicks <= 0
                            || definition.ScavengerCorpseSearchRadius <= 0
                            || definition.ScavengerHellfireHealingDivisor <= 0
                            || definition.ScavengerBaseHealingAmount <= 0
                            || definition.ScavengerBaseHealingTargetPercent <= 0)))
                || (definition.NativeAi != "Scavenger"
                    && (definition.ScavengerHealingDurationTicks != 0
                        || definition.ScavengerCorpseSearchRadius != 0
                        || definition.ScavengerHellfireHealingDivisor != 0
                        || definition.ScavengerBaseHealingAmount != 0
                        || definition.ScavengerBaseHealingTargetPercent != 0))
                || (definition.NativeAi != "Scavenger"
                    && definition.ScavengerHealingHealthThresholdPercent != 0)
                || definition.SkeletonRangedRetreatMinimumDistance is < 0 or > 64
                || definition.SkeletonRangedRetreatChanceBase is < 0 or > 100
                || definition.SkeletonRangedRetreatChanceIntelligenceScale is < 0 or > 100
                || definition.SkeletonRangedAttackChanceBase is < 0 or > 100
                || definition.SkeletonRangedAttackChanceIntelligenceScale is < 0 or > 100
                || ((definition.SkeletonRangedRetreatMinimumDistance != 0
                        || definition.SkeletonRangedRetreatChanceBase != 0
                        || definition.SkeletonRangedRetreatChanceIntelligenceScale != 0
                        || definition.SkeletonRangedAttackChanceBase != 0
                        || definition.SkeletonRangedAttackChanceIntelligenceScale != 0)
                    && (definition.SkeletonRangedRetreatMinimumDistance <= 0
                        || definition.SkeletonRangedRetreatChanceBase <= 0
                        || definition.SkeletonRangedRetreatChanceIntelligenceScale <= 0
                        || definition.SkeletonRangedAttackChanceBase <= 0
                        || definition.SkeletonRangedAttackChanceIntelligenceScale <= 0))
                || definition.RhinoChargeMinimumDistance is < 0 or > 64
                || definition.RhinoChargeChanceBase is < 0 or > 100
                || definition.RhinoChargeChanceIntelligenceScale is < 0 or > 100
                || ((definition.RhinoChargeMinimumDistance != 0
                        || definition.RhinoChargeChanceBase != 0
                        || definition.RhinoChargeChanceIntelligenceScale != 0)
                    && (definition.RhinoChargeMinimumDistance <= 0
                        || definition.RhinoChargeChanceBase <= 0
                        || definition.RhinoChargeChanceIntelligenceScale <= 0))
                || !this.definitions.TryAdd(definition.MonsterId, definition)) {
                throw new InvalidDataException("Monster type definitions contain invalid or duplicate identities.");
            }
            if (!definitionsByNativeId.TryAdd(definition.NativeMonsterId, definition)) {
                definitionsByNativeId.Remove(definition.NativeMonsterId);
                ambiguousNativeIds.Add(definition.NativeMonsterId);
            }
        }
        if (this.definitions.Count == 0)
            throw new InvalidDataException("The monster type catalog cannot be empty.");
    }

    public IReadOnlyList<AuthoritativeMonsterTypeDefinition> Definitions
        => definitions.Values.OrderBy(definition => definition.MonsterId).ToArray();

    public bool TryGet(uint monsterId, out AuthoritativeMonsterTypeDefinition definition)
        => definitions.TryGetValue(monsterId, out definition!);

    public static bool IsValidNativeAi(string nativeAi)
        => !string.IsNullOrWhiteSpace(nativeAi) && ValidNativeAis.Contains(nativeAi);

    /** Returns validated monster types whose native dungeon-level range includes the level. */
    public IReadOnlyList<AuthoritativeMonsterTypeDefinition> GetAvailableForDungeonLevel(
        int dungeonLevel)
    {
        if (dungeonLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(dungeonLevel));

        return Definitions
            .Where(definition => definition.MinimumDungeonLevel <= dungeonLevel
                && definition.MaximumDungeonLevel >= dungeonLevel)
            .ToArray();
    }

    /** Resolves an unambiguous native monster-table identity without exposing TSV parsing to the engine. */
    public bool TryGetNative(uint nativeMonsterId, out AuthoritativeMonsterTypeDefinition definition)
    {
        if (ambiguousNativeIds.Contains(nativeMonsterId)) {
            definition = null!;
            return false;
        }
        return definitionsByNativeId.TryGetValue(nativeMonsterId, out definition!);
    }

    public static AuthoritativeMonsterTypeCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeMonsterTypeCatalog(table.Rows.Select(row =>
            new AuthoritativeMonsterTypeDefinition(
                row.RequiredUInt32("monster_id"),
                row.RequiredUInt32("native_monster_id"),
                row.Required("name"),
                row.RequiredInt32("minimum_dungeon_level"),
                row.RequiredInt32("maximum_dungeon_level"),
                row.RequiredInt32("monster_level"),
                row.RequiredInt32("minimum_hit_points"),
                row.RequiredInt32("maximum_hit_points"),
                row.RequiredInt32("armor_class"),
                row.RequiredInt32("minimum_damage"),
                row.RequiredInt32("maximum_damage"),
                row.OptionalInt32("minimum_special_damage"),
                row.OptionalInt32("maximum_special_damage"),
                ParseFlag(row, "stone_curse_immune"),
                ParseFlag(row, "undead"),
                ParseFlag(row, "berserk_immune")) {
                Demon = ParseFlag(row, "demon"),
                Animal = ParseFlag(row, "animal"),
                Knockback = ParseFlag(row, "knockback"),
                FireResistance = row.OptionalInt32("fire_resistance"),
                LightningResistance = row.OptionalInt32("lightning_resistance"),
                MagicResistance = row.OptionalInt32("magic_resistance"),
                AcidResistance = row.OptionalInt32("acid_resistance"),
                AggroRange = row.OptionalInt32("aggro_range", 6),
                DeathAnimationTicks = row.OptionalInt32("death_animation_ticks"),
                ExperienceReward = row.OptionalUInt32("experience_reward"),
                ToHit = row.OptionalInt32("to_hit"),
                ToHitSpecial = row.OptionalInt32("to_hit_special"),
                NativeAi = row.TryGet("native_ai", out var nativeAi)
                    && !string.IsNullOrWhiteSpace(nativeAi)
                    ? nativeAi
                    : "Zombie",
                Intelligence = row.OptionalInt32("intelligence"),
                FallenFearRadius = row.OptionalInt32("fallen_fear_radius"),
                FallenFearLevelOffset = row.OptionalInt32("fallen_fear_level_offset"),
                FallenFearMinimumTicks = row.OptionalInt32("fallen_fear_minimum_ticks"),
                SneakDistanceBase = row.OptionalInt32("sneak_distance_base"),
                SneakDistanceIntelligenceScale = row.OptionalInt32("sneak_distance_intelligence_scale"),
                SneakRetreatReleaseDistance = row.OptionalInt32("sneak_retreat_release_distance"),
                SneakRetreatTicks = row.OptionalInt32("sneak_retreat_ticks"),
                BatAttackChanceBase = row.OptionalInt32("bat_attack_chance_base"),
                BatAttackChanceIntelligenceScale = row.OptionalInt32("bat_attack_chance_intelligence_scale"),
                BatRetreatDurationTicks = row.OptionalInt32("bat_retreat_duration_ticks"),
                SkeletonMeleeDelayChanceBase = row.OptionalInt32("skeleton_melee_delay_chance_base"),
                SkeletonMeleeDelayChanceIntelligenceScale = row.OptionalInt32("skeleton_melee_delay_chance_intelligence_scale"),
                SkeletonMeleeAttackChanceBase = row.OptionalInt32("skeleton_melee_attack_chance_base"),
                SkeletonMeleeAttackChanceIntelligenceScale = row.OptionalInt32("skeleton_melee_attack_chance_intelligence_scale"),
                GoatMeleeMovementChanceBase = row.OptionalInt32("goat_melee_movement_chance_base"),
                GoatMeleeMovementChanceIntelligenceScale = row.OptionalInt32("goat_melee_movement_chance_intelligence_scale"),
                GoatMeleeAttackChanceBase = row.OptionalInt32("goat_melee_attack_chance_base"),
                GoatMeleeAttackChanceIntelligenceScale = row.OptionalInt32("goat_melee_attack_chance_intelligence_scale"),
                GoatMeleeSpecialHealthThresholdPercent = row.OptionalInt32("goat_melee_special_health_threshold_percent"),
                GoatMeleeSpecialChanceGroupPeriod = row.OptionalInt32("goat_melee_special_chance_group_period"),
                FatMovementChanceBase = row.OptionalInt32("fat_movement_chance_base"),
                FatMovementChanceIntelligenceScale = row.OptionalInt32("fat_movement_chance_intelligence_scale"),
                FatAttackChanceBase = row.OptionalInt32("fat_attack_chance_base"),
                FatAttackChanceIntelligenceScale = row.OptionalInt32("fat_attack_chance_intelligence_scale"),
                FatSpecialChanceBase = row.OptionalInt32("fat_special_chance_base"),
                FatSpecialChanceIntelligenceScale = row.OptionalInt32("fat_special_chance_intelligence_scale"),
                ZombieActionChanceBase = row.OptionalInt32("zombie_action_chance_base"),
                ZombieActionChanceIntelligenceScale = row.OptionalInt32("zombie_action_chance_intelligence_scale"),
                ZombieRandomDirectionChanceBase = row.OptionalInt32("zombie_random_direction_chance_base"),
                ZombieRandomDirectionChanceIntelligenceScale = row.OptionalInt32("zombie_random_direction_chance_intelligence_scale"),
                ZombieMovementDistanceMinimum = row.OptionalInt32("zombie_movement_distance_minimum"),
                ZombieMovementDistanceBase = row.OptionalInt32("zombie_movement_distance_base"),
                ZombieMovementDistanceIntelligenceScale = row.OptionalInt32("zombie_movement_distance_intelligence_scale"),
                SkeletonKingSummonMinimumDistance = row.OptionalInt32("skeleton_king_summon_minimum_distance"),
                SkeletonKingSummonChanceBase = row.OptionalInt32("skeleton_king_summon_chance_base"),
                SkeletonKingSummonChanceIntelligenceScale = row.OptionalInt32("skeleton_king_summon_chance_intelligence_scale"),
                HorkDemonAttackChanceBase = row.OptionalInt32("hork_demon_attack_chance_base"),
                HorkDemonAttackChanceIntelligenceScale = row.OptionalInt32("hork_demon_attack_chance_intelligence_scale"),
                HorkDemonSpawnMinimumDistance = row.OptionalInt32("hork_demon_spawn_minimum_distance"),
                HorkDemonSpawnChanceBase = row.OptionalInt32("hork_demon_spawn_chance_base"),
                HorkDemonSpawnChanceIntelligenceScale = row.OptionalInt32("hork_demon_spawn_chance_intelligence_scale"),
                HorkDemonMovementChanceBase = row.OptionalInt32("hork_demon_movement_chance_base"),
                HorkDemonMovementChanceIntelligenceScale = row.OptionalInt32("hork_demon_movement_chance_intelligence_scale"),
                GargoyleSafeDistanceBase = row.OptionalInt32("gargoyle_safe_distance_base"),
                GargoyleSafeDistanceIntelligenceScale = row.OptionalInt32("gargoyle_safe_distance_intelligence_scale"),
                GargoyleHealingHealthThresholdPercent = row.OptionalInt32("gargoyle_healing_health_threshold_percent"),
                GargoyleHealDivisorMultiplier = row.OptionalInt32("gargoyle_heal_divisor_multiplier"),
                GargoyleHealDivisorRandomRange = row.OptionalInt32("gargoyle_heal_divisor_random_range"),
                GargoyleHealDivisorRandomOffset = row.OptionalInt32("gargoyle_heal_divisor_random_offset"),
                ScavengerHealingDurationTicks = row.OptionalInt32("scavenger_healing_duration_ticks"),
                ScavengerCorpseSearchRadius = row.OptionalInt32("scavenger_corpse_search_radius"),
                ScavengerHellfireHealingDivisor = row.OptionalInt32("scavenger_hellfire_healing_divisor"),
                ScavengerBaseHealingAmount = row.OptionalInt32("scavenger_base_healing_amount"),
                ScavengerBaseHealingTargetPercent = row.OptionalInt32("scavenger_base_healing_target_percent"),
                ScavengerHealingHealthThresholdPercent = row.OptionalInt32("scavenger_healing_health_threshold_percent"),
                SkeletonRangedRetreatMinimumDistance = row.OptionalInt32("skeleton_ranged_retreat_minimum_distance"),
                SkeletonRangedRetreatChanceBase = row.OptionalInt32("skeleton_ranged_retreat_chance_base"),
                SkeletonRangedRetreatChanceIntelligenceScale = row.OptionalInt32("skeleton_ranged_retreat_chance_intelligence_scale"),
                SkeletonRangedAttackChanceBase = row.OptionalInt32("skeleton_ranged_attack_chance_base"),
                SkeletonRangedAttackChanceIntelligenceScale = row.OptionalInt32("skeleton_ranged_attack_chance_intelligence_scale"),
                RhinoChargeMinimumDistance = row.OptionalInt32("rhino_charge_minimum_distance"),
                RhinoChargeChanceBase = row.OptionalInt32("rhino_charge_chance_base"),
                RhinoChargeChanceIntelligenceScale = row.OptionalInt32("rhino_charge_chance_intelligence_scale"),
            }));
    }

    private static bool ParseFlag(TsvRow row, string column)
    {
        var value = row.OptionalInt32(column);
        if (value is not (0 or 1))
            throw new InvalidDataException(
                $"Monster type row {row.LineNumber} has invalid {column} value {value}.");
        return value == 1;
    }
}
