using Devilution.Data;
using Devilution.Shared;

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
    /** Stable content symbol retained alongside the numeric compatibility ID. */
    public string StableSymbol { get; init; } = string.Empty;

    /** Native monster class capability used by demon-specific equipment. */
    public bool Demon { get; init; }

    /** Native monster class capability used by sword/mace damage modifiers. */
    public bool Animal { get; init; }

    /** Native monster ability that displaces a player after a successful hit. */
    public bool Knockback { get; init; }

    /** Native boss identity used by Diablo end-state and quest completion rules. */
    public bool Diablo { get; init; }

    /** Native resistance percentages derived from the active monster data. */
    public int FireResistance { get; init; }
    public int LightningResistance { get; init; }
    public int MagicResistance { get; init; }
    public int AcidResistance { get; init; }

    /** Native Hell difficulty resistance percentages, when distinct from Normal. */
    public int? FireResistanceHell { get; init; }
    public int? LightningResistanceHell { get; init; }
    public int? MagicResistanceHell { get; init; }
    public int? AcidResistanceHell { get; init; }

    /** Native aggro radius used by autonomous monster behavior. */
    public int AggroRange { get; init; } = 6;

    /** Native death-animation duration in authoritative game ticks. */
    public int DeathAnimationTicks { get; init; }

    /** Native standing-animation frame count used during InitMonster seeding. */
    public int StandAnimationFrames { get; init; }

    /** Native standing-animation ticks per frame used during InitMonster seeding. */
    public int StandAnimationTicksPerFrame { get; init; }

    /** Native experience awarded when this monster is defeated. */
    public uint ExperienceReward { get; init; }

    /** Native normal-attack chance to hit before level, difficulty, and armor adjustments. */
    public int ToHit { get; init; }

    /** Native special-attack chance to hit before level, difficulty, and armor adjustments. */
    public int ToHitSpecial { get; init; }

    /** Native MonsterAIID dispatch identity used by incremental authoritative AI ports. */
    public string NativeAi { get; init; } = "Zombie";

    /** Optional content-owned eligibility for Skeleton King summon selection. */
    public bool? SkeletonSummonCandidate { get; init; }

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

    /** Native Bat movement chance base before intelligence scaling. */
    public int BatMovementChanceBase { get; init; }

    /** Native Bat movement chance increase per intelligence point. */
    public int BatMovementChanceIntelligenceScale { get; init; }

    /** Native Bat fresh-mode movement chance bonus. */
    public int BatMovementFreshChanceBonus { get; init; }

    /** Native Bat post-hit retreat duration in authoritative ticks. */
    public int BatRetreatDurationTicks { get; init; }

    /** Native SkeletonMelee movement-delay chance base before intelligence scaling. */
    public int SkeletonMeleeDelayChanceBase { get; init; }

    /** Native SkeletonMelee movement-delay chance reduction per intelligence point. */
    public int SkeletonMeleeDelayChanceIntelligenceScale { get; init; }

    /** Native SkeletonMelee movement-delay base before intelligence reduction. */
    public int SkeletonMeleeMovementDelayBase { get; init; }

    /** Native SkeletonMelee movement-delay reduction per intelligence point. */
    public int SkeletonMeleeMovementDelayIntelligenceScale { get; init; }

    /** Native SkeletonMelee movement-delay exclusive random range. */
    public int SkeletonMeleeMovementDelayRandomRange { get; init; }

    /** Native SkeletonMelee failed-adjacent-attack delay base before intelligence reduction. */
    public int SkeletonMeleeAttackDelayBase { get; init; }

    /** Native SkeletonMelee failed-adjacent-attack delay reduction per intelligence point. */
    public int SkeletonMeleeAttackDelayIntelligenceScale { get; init; }

    /** Native SkeletonMelee failed-adjacent-attack delay exclusive random range. */
    public int SkeletonMeleeAttackDelayRandomRange { get; init; }

    /** Native AiAvoidance failed RoundWalk delay base. */
    public int AvoidanceDelayBase { get; init; }

    /** Native AiAvoidance failed RoundWalk exclusive random range. */
    public int AvoidanceDelayRandomRange { get; init; }

    /** Native Counselor failed-ranged-admission delay base before intelligence reduction. */
    public int CounselorDelayBase { get; init; }

    /** Native Counselor failed-ranged-admission delay reduction per intelligence point. */
    public int CounselorDelayIntelligenceScale { get; init; }

    /** Native Counselor failed-ranged-admission delay exclusive random range. */
    public int CounselorDelayRandomRange { get; init; }

    /** Native Counselor successful-attack recovery delay base. */
    public int CounselorAttackDelayBase { get; init; }

    /** Native Counselor successful-attack recovery exclusive random range. */
    public int CounselorAttackDelayRandomRange { get; init; }

    /** Native Counselor failed-ranged-admission movement chance percentage. */
    public int CounselorMoveChancePercent { get; init; }

    /** Native Counselor ranged-admission chance base before intelligence scaling. */
    public int CounselorAttackChanceBase { get; init; }

    /** Native Counselor ranged-admission chance increase per intelligence point. */
    public int CounselorAttackChanceIntelligenceScale { get; init; }

    /** Native Counselor adjacent Flash-admission chance base before intelligence scaling. */
    public int CounselorAdjacentAttackChanceBase { get; init; }

    /** Native Counselor adjacent Flash-admission chance increase per intelligence point. */
    public int CounselorAdjacentAttackChanceIntelligenceScale { get; init; }

    /** Native Mega delegated Skeleton movement-delay chance base. */
    public int MegaDelayChanceBase { get; init; }

    /** Native Mega delegated Skeleton movement-delay chance reduction per intelligence. */
    public int MegaDelayChanceIntelligenceScale { get; init; }

    /** Native Mega intermediate-distance movement chance base before intelligence scaling. */
    public int MegaIntermediateMovementChanceBase { get; init; }

    /** Native Mega intermediate-distance movement chance increase per intelligence. */
    public int MegaIntermediateMovementChanceIntelligenceScale { get; init; }

    /** Native SkeletonKing RoundWalk admission modulo for the movement branch. */
    public int SkeletonKingRoundWalkChanceModulo { get; init; }

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

    /** Native Fat fresh-mode movement chance bonus. */
    public int FatMovementFreshChanceBonus { get; init; }

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

    /** Native SkeletonKing movement-delay base before its exclusive random range. */
    public int SkeletonKingDelayBase { get; init; }

    /** Native SkeletonKing movement-delay exclusive random range. */
    public int SkeletonKingDelayRandomRange { get; init; }

    /** Native SkeletonKing movement admission chance base. */
    public int SkeletonKingMovementChanceBase { get; init; }

    /** Native SkeletonKing movement admission chance increase per intelligence point. */
    public int SkeletonKingMovementChanceIntelligenceScale { get; init; }

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

    /** Native HorkDemon far-distance RoundWalk admission modulo. */
    public int HorkDemonRoundWalkChanceModulo { get; init; }

    /** Native AiAvoidance minimum distance required to start its Move goal. */
    public int AiAvoidanceMovementMinimumDistance { get; init; }

    /** Native AiAvoidance Move-goal admission modulo. */
    public int AiAvoidanceMovementChanceModulo { get; init; }

    /** Native AiAvoidance fresh-mode movement chance bonus. */
    public int AiAvoidanceMovementFreshChanceBonus { get; init; }

    /** Native AiAvoidance RoundWalk chance base before intelligence scaling. */
    public int AiAvoidanceRoundWalkChanceBase { get; init; }

    /** Native AiAvoidance RoundWalk chance increase per intelligence point. */
    public int AiAvoidanceRoundWalkChanceIntelligenceScale { get; init; }

    /** Native Gargoyle safety-boundary base before intelligence scaling. */
    public int GargoyleSafeDistanceBase { get; init; }

    /** Native Gargoyle safety-boundary increase per intelligence point. */
    public int GargoyleSafeDistanceIntelligenceScale { get; init; }

    /** Native Gargoyle adjacent-attack chance base before intelligence scaling. */
    public int GargoyleAttackChanceBase { get; init; }

    /** Native Gargoyle adjacent-attack chance increase per intelligence point. */
    public int GargoyleAttackChanceIntelligenceScale { get; init; }

    /** Native Gargoyle avoidance-movement chance base before intelligence scaling. */
    public int GargoyleMovementChanceBase { get; init; }

    /** Native Gargoyle avoidance-movement chance increase per intelligence point. */
    public int GargoyleMovementChanceIntelligenceScale { get; init; }

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

    /** Native SkeletonRanged fresh-mode movement chance bonus. */
    public int SkeletonRangedMovementFreshChanceBonus { get; init; }

    /** Native Rhino minimum distance required for charge admission. */
    public int RhinoChargeMinimumDistance { get; init; }

    /** Native Rhino charge chance base before intelligence scaling. */
    public int RhinoChargeChanceBase { get; init; }

    /** Native Rhino charge chance increase per intelligence point. */
    public int RhinoChargeChanceIntelligenceScale { get; init; }

    /** Native Rhino ordinary movement admission chance base before intelligence scaling. */
    public int RhinoMovementChanceBase { get; init; }

    /** Native Rhino ordinary movement admission chance increase per intelligence point. */
    public int RhinoMovementChanceIntelligenceScale { get; init; }

    /** Native Rhino fresh-mode movement chance bonus. */
    public int RhinoMovementFreshChanceBonus { get; init; }

    /** Native Rhino movement-goal minimum distance. */
    public int RhinoMovementGoalMinimumDistance { get; init; }

    /** Native Rhino far-distance movement-goal admission modulo. */
    public int RhinoMovementGoalChanceModulo { get; init; }

    /** Native Rhino recovery-delay base before its exclusive random range. */
    public int RhinoDelayBase { get; init; }

    /** Native Rhino recovery-delay exclusive random range. */
    public int RhinoDelayRandomRange { get; init; }

    /** True when any complete migrated AI controller tuple is present. */
    public bool HasCompleteAiTuning
        => FallenFearRadius > 0
            && FallenFearLevelOffset > 0
            && FallenFearMinimumTicks > 0
            || SneakDistanceBase > 0
            && SneakDistanceIntelligenceScale > 0
            && SneakRetreatReleaseDistance > 0
            && SneakRetreatTicks > 0
            || BatAttackChanceBase > 0
            && BatAttackChanceIntelligenceScale > 0
            || BatMovementChanceBase > 0
            && BatMovementChanceIntelligenceScale > 0
            && BatMovementFreshChanceBonus > 0
            || SkeletonMeleeDelayChanceBase > 0
            && SkeletonMeleeDelayChanceIntelligenceScale > 0
            || SkeletonMeleeAttackChanceBase > 0
            && SkeletonMeleeAttackChanceIntelligenceScale > 0
            || GoatMeleeMovementChanceBase > 0
            && GoatMeleeMovementChanceIntelligenceScale > 0
            || GoatMeleeAttackChanceBase > 0
            && GoatMeleeAttackChanceIntelligenceScale > 0
            || GoatMeleeSpecialHealthThresholdPercent > 0
            && GoatMeleeSpecialChanceGroupPeriod > 0
            || FatMovementChanceBase > 0
            && FatMovementChanceIntelligenceScale > 0
            || FatAttackChanceBase > 0
            && FatAttackChanceIntelligenceScale > 0
            && FatSpecialChanceBase > 0
            && FatSpecialChanceIntelligenceScale > 0
            || ZombieActionChanceBase > 0
            && ZombieActionChanceIntelligenceScale > 0
            || ZombieRandomDirectionChanceBase > 0
            && ZombieRandomDirectionChanceIntelligenceScale > 0
            || ZombieMovementDistanceMinimum > 0
            && ZombieMovementDistanceBase > 0
            && ZombieMovementDistanceIntelligenceScale > 0
            || SkeletonKingSummonMinimumDistance > 0
            && SkeletonKingSummonChanceBase > 0
            && SkeletonKingSummonChanceIntelligenceScale > 0
            || HorkDemonAttackChanceBase > 0
            && HorkDemonAttackChanceIntelligenceScale > 0
            && HorkDemonSpawnMinimumDistance > 0
            && HorkDemonSpawnChanceBase > 0
            && HorkDemonSpawnChanceIntelligenceScale > 0
            && HorkDemonMovementChanceBase > 0
            && HorkDemonMovementChanceIntelligenceScale > 0
            || GargoyleSafeDistanceBase > 0
            && GargoyleSafeDistanceIntelligenceScale > 0
            || GargoyleAttackChanceBase > 0
            && GargoyleAttackChanceIntelligenceScale > 0
            || GargoyleMovementChanceBase > 0
            && GargoyleMovementChanceIntelligenceScale > 0
            || AiAvoidanceRoundWalkChanceBase > 0
            && AiAvoidanceRoundWalkChanceIntelligenceScale > 0
            || ScavengerHealingDurationTicks > 0
            && ScavengerCorpseSearchRadius > 0
            && ScavengerHellfireHealingDivisor > 0
            && ScavengerBaseHealingAmount > 0
            && ScavengerBaseHealingTargetPercent > 0
            || SkeletonRangedRetreatMinimumDistance > 0
            && SkeletonRangedRetreatChanceBase > 0
            && SkeletonRangedRetreatChanceIntelligenceScale > 0
            && SkeletonRangedAttackChanceBase > 0
            && SkeletonRangedAttackChanceIntelligenceScale > 0
            || CounselorAttackChanceBase > 0
            && CounselorAttackChanceIntelligenceScale > 0
            || CounselorAdjacentAttackChanceBase > 0
            && CounselorAdjacentAttackChanceIntelligenceScale > 0
            || RhinoChargeMinimumDistance > 0
            && RhinoChargeChanceBase > 0
            && RhinoChargeChanceIntelligenceScale > 0
            || RhinoMovementChanceBase > 0
            && RhinoMovementChanceIntelligenceScale > 0;
}

/** Validated monster type metadata used to admit external encounter rows. */
public sealed class AuthoritativeMonsterTypeCatalog
{
    private static readonly HashSet<string> ValidNativeAis = new(StringComparer.Ordinal) {
        "Acid", "AcidUnique", "ArchLich", "Bat", "BoneDemon", "Butcher", "Counselor", "Diablo",
        "Fallen", "Fat", "FireBat", "FireMan", "Gargoyle", "Gharbad", "GoatMelee", "GoatRanged",
        "Golem", "HorkDemon", "Lachdanan", "Lich", "Magma", "Mega", "Necromorb", "Psychorb", "Rhino", "Scavenger",
        "SkeletonKing", "SkeletonMelee", "SkeletonRanged", "Snake", "Sneak", "Snotspill", "Storm",
        "Succubus", "Lazarus", "LazarusSuccubus", "Torchant", "Warlord", "Zhar", "Zombie",
    };

    private readonly Dictionary<uint, AuthoritativeMonsterTypeDefinition> definitions;
    private readonly Dictionary<uint, AuthoritativeMonsterTypeDefinition> definitionsByNativeId;
    private readonly HashSet<uint> ambiguousNativeIds;
    private readonly Dictionary<string, AuthoritativeMonsterTypeDefinition> definitionsByNativeAi;
    private readonly HashSet<string> ambiguousNativeAis;

    public AuthoritativeMonsterTypeCatalog(
        IEnumerable<AuthoritativeMonsterTypeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.definitions = new Dictionary<uint, AuthoritativeMonsterTypeDefinition>();
        definitionsByNativeId = new Dictionary<uint, AuthoritativeMonsterTypeDefinition>();
        ambiguousNativeIds = new HashSet<uint>();
        definitionsByNativeAi = new Dictionary<string, AuthoritativeMonsterTypeDefinition>(StringComparer.Ordinal);
        ambiguousNativeAis = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions) {
            if (definition.MonsterId == 0
                || definition.NativeMonsterId > AuthoritativeLevel.NativeMaximumMonsterTypeId
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
                || definition.FireResistanceHell is < -100 or > 100
                || definition.LightningResistanceHell is < -100 or > 100
                || definition.MagicResistanceHell is < -100 or > 100
                || definition.AcidResistanceHell is < -100 or > 100
                || definition.AggroRange is < 0 or > 64
                || definition.DeathAnimationTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDeathAnimationTicks
                || definition.StandAnimationFrames is < 0 or > 127
                || definition.StandAnimationTicksPerFrame is < 0 or > 127
                || definition.ExperienceReward > 1_000_000
                || definition.ToHit is < 0 or > 255
                || definition.ToHitSpecial is < 0 or > 255
                || definition.Intelligence is < 0 or > 255
                || !IsValidNativeAi(definition.NativeAi)
                || definition.FallenFearRadius is < 0 or > 64
                || definition.FallenFearLevelOffset is < 0 or > 255
                || definition.FallenFearMinimumTicks is < 0 or > 255
                || ((definition.FallenFearRadius != 0
                        || definition.FallenFearLevelOffset != 0
                        || definition.FallenFearMinimumTicks != 0)
                    && (definition.FallenFearRadius <= 0
                        || definition.FallenFearLevelOffset <= 0
                        || definition.FallenFearMinimumTicks <= 0))
                || definition.SneakDistanceBase is < 0 or > 64
                || definition.SneakDistanceIntelligenceScale is < 0 or > 64
                || definition.SneakRetreatReleaseDistance is < 0 or > 64
                || definition.SneakRetreatTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterShortTimerTicks
                || ((definition.SneakDistanceBase != 0
                        || definition.SneakDistanceIntelligenceScale != 0
                        || definition.SneakRetreatReleaseDistance != 0
                        || definition.SneakRetreatTicks != 0)
                    && (definition.SneakDistanceBase <= 0
                        || definition.SneakDistanceIntelligenceScale <= 0
                        || definition.SneakRetreatReleaseDistance <= 0
                        || definition.SneakRetreatTicks <= 0))
                || definition.BatAttackChanceBase is < 0 or > 100
                || definition.BatAttackChanceIntelligenceScale is < 0 or > 100
                || ((definition.BatAttackChanceBase != 0
                        || definition.BatAttackChanceIntelligenceScale != 0)
                    && (definition.BatAttackChanceBase <= 0
                        || definition.BatAttackChanceIntelligenceScale <= 0))
                || definition.BatMovementChanceBase is < 0 or > 100
                || definition.BatMovementChanceIntelligenceScale is < 0 or > 100
                || definition.BatMovementFreshChanceBonus is < 0 or > 100
                || ((definition.BatMovementChanceBase != 0
                        || definition.BatMovementChanceIntelligenceScale != 0
                        || definition.BatMovementFreshChanceBonus != 0)
                    && (definition.BatMovementChanceBase <= 0
                        || definition.BatMovementChanceIntelligenceScale <= 0
                        || definition.BatMovementFreshChanceBonus <= 0))
                || (definition.NativeAi != "Bat"
                    && (definition.BatMovementChanceBase != 0
                        || definition.BatMovementChanceIntelligenceScale != 0
                        || definition.BatMovementFreshChanceBonus != 0))
                || definition.BatRetreatDurationTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterRetreatTicks
                || definition.SkeletonMeleeDelayChanceBase is < 0 or > 100
                || definition.SkeletonMeleeDelayChanceIntelligenceScale is < 0 or > 100
                || ((definition.SkeletonMeleeDelayChanceBase != 0
                        || definition.SkeletonMeleeDelayChanceIntelligenceScale != 0)
                    && (definition.SkeletonMeleeDelayChanceBase <= 0
                        || definition.SkeletonMeleeDelayChanceIntelligenceScale <= 0))
                || definition.SkeletonMeleeMovementDelayBase < 0
                || definition.SkeletonMeleeMovementDelayIntelligenceScale < 0
                || definition.SkeletonMeleeMovementDelayRandomRange < 0
                || (definition.SkeletonMeleeMovementDelayBase != 0
                    || definition.SkeletonMeleeMovementDelayIntelligenceScale != 0
                    || definition.SkeletonMeleeMovementDelayRandomRange != 0)
                    && (definition.NativeAi is not ("SkeletonMelee" or "Fallen" or "Snotspill" or "Warlord")
                        || definition.SkeletonMeleeMovementDelayRandomRange <= 0)
                || definition.SkeletonMeleeAttackDelayBase < 0
                || definition.SkeletonMeleeAttackDelayIntelligenceScale < 0
                || definition.SkeletonMeleeAttackDelayRandomRange < 0
                || (definition.SkeletonMeleeAttackDelayBase != 0
                    || definition.SkeletonMeleeAttackDelayIntelligenceScale != 0
                    || definition.SkeletonMeleeAttackDelayRandomRange != 0)
                    && (definition.NativeAi is not ("SkeletonMelee" or "Fallen" or "Snotspill" or "Warlord")
                        || definition.SkeletonMeleeAttackDelayRandomRange <= 0)
                || definition.AvoidanceDelayBase is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || definition.AvoidanceDelayRandomRange is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || (definition.AvoidanceDelayBase != 0
                    || definition.AvoidanceDelayRandomRange != 0)
                    && (definition.NativeAi is not ("GoatMelee" or "Gargoyle" or "Gharbad")
                        || definition.AvoidanceDelayBase <= 0
                        || definition.AvoidanceDelayRandomRange <= 0)
                || definition.CounselorDelayBase is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || definition.CounselorDelayIntelligenceScale is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || definition.CounselorDelayRandomRange is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || (definition.CounselorDelayBase != 0
                    || definition.CounselorDelayIntelligenceScale != 0
                    || definition.CounselorDelayRandomRange != 0)
                    && (definition.NativeAi is not ("Counselor" or "Zhar" or "Lazarus")
                        || definition.CounselorDelayBase <= 0
                        || definition.CounselorDelayRandomRange <= 0)
                || definition.CounselorAttackDelayBase is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || definition.CounselorAttackDelayRandomRange is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterAiTimerTicks
                || (definition.CounselorAttackDelayBase != 0
                    || definition.CounselorAttackDelayRandomRange != 0)
                    && (definition.NativeAi is not ("Counselor" or "Zhar" or "Lazarus")
                        || definition.CounselorAttackDelayBase <= 0
                        || definition.CounselorAttackDelayRandomRange <= 0)
                || definition.CounselorMoveChancePercent is < 0 or > 100
                || definition.CounselorMoveChancePercent != 0
                    && definition.NativeAi is not ("Counselor" or "Zhar" or "Lazarus")
                || definition.CounselorAttackChanceBase is < 0 or > 100
                || definition.CounselorAttackChanceIntelligenceScale is < 0 or > 100
                || (definition.CounselorAttackChanceBase != 0
                    || definition.CounselorAttackChanceIntelligenceScale != 0)
                    && (definition.NativeAi is not ("Counselor" or "Zhar" or "Lazarus")
                        || definition.CounselorAttackChanceBase <= 0
                        || definition.CounselorAttackChanceIntelligenceScale <= 0)
                || definition.CounselorAdjacentAttackChanceBase is < 0 or > 100
                || definition.CounselorAdjacentAttackChanceIntelligenceScale is < 0 or > 100
                || (definition.CounselorAdjacentAttackChanceBase != 0
                    || definition.CounselorAdjacentAttackChanceIntelligenceScale != 0)
                    && (definition.NativeAi is not ("Counselor" or "Zhar" or "Lazarus")
                        || definition.CounselorAdjacentAttackChanceBase <= 0
                        || definition.CounselorAdjacentAttackChanceIntelligenceScale <= 0)
                || definition.MegaDelayChanceBase is < 0 or > 100
                || definition.MegaDelayChanceIntelligenceScale is < 0 or > 100
                || (definition.MegaDelayChanceBase != 0
                    || definition.MegaDelayChanceIntelligenceScale != 0)
                    && (definition.NativeAi != "Mega"
                        || definition.MegaDelayChanceBase <= 0
                        || definition.MegaDelayChanceIntelligenceScale <= 0)
                || definition.MegaIntermediateMovementChanceBase is < 0 or > 100
                || definition.MegaIntermediateMovementChanceIntelligenceScale is < 0 or > 100
                || (definition.MegaIntermediateMovementChanceBase != 0
                    || definition.MegaIntermediateMovementChanceIntelligenceScale != 0)
                    && (definition.NativeAi != "Mega"
                        || definition.MegaIntermediateMovementChanceBase <= 0
                        || definition.MegaIntermediateMovementChanceIntelligenceScale <= 0)
                || definition.SkeletonKingRoundWalkChanceModulo is < 0 or > 100
                || definition.SkeletonKingRoundWalkChanceModulo != 0
                    && (definition.NativeAi != "SkeletonKing"
                        || definition.SkeletonKingRoundWalkChanceModulo <= 0)
                || definition.HorkDemonRoundWalkChanceModulo is < 0 or > 100
                || definition.HorkDemonRoundWalkChanceModulo != 0
                    && (definition.NativeAi != "HorkDemon"
                        || definition.HorkDemonRoundWalkChanceModulo <= 0)
                || definition.AiAvoidanceMovementMinimumDistance is < 0 or > 100
                || definition.AiAvoidanceMovementChanceModulo is < 0 or > 100
                || definition.AiAvoidanceMovementFreshChanceBonus is < 0 or > 100
                || definition.AiAvoidanceRoundWalkChanceBase is < 0 or > 100
                || definition.AiAvoidanceRoundWalkChanceIntelligenceScale is < 0 or > 100
                || (definition.AiAvoidanceMovementMinimumDistance != 0
                    || definition.AiAvoidanceMovementChanceModulo != 0)
                    && (definition.NativeAi is not ("GoatMelee" or "Gargoyle" or "Gharbad")
                        || definition.AiAvoidanceMovementMinimumDistance < 2
                        || definition.AiAvoidanceMovementChanceModulo <= 0)
                || definition.AiAvoidanceMovementFreshChanceBonus != 0
                    && definition.NativeAi is not ("GoatMelee" or "Gargoyle" or "Gharbad")
                || (definition.AiAvoidanceRoundWalkChanceBase != 0
                    || definition.AiAvoidanceRoundWalkChanceIntelligenceScale != 0)
                    && (definition.NativeAi is not ("GoatMelee" or "Gargoyle" or "Gharbad")
                        || definition.AiAvoidanceRoundWalkChanceBase <= 0
                        || definition.AiAvoidanceRoundWalkChanceIntelligenceScale <= 0)
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
                || definition.FatMovementFreshChanceBonus is < 0 or > 100
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
                || definition.SkeletonKingDelayBase < 0
                || definition.SkeletonKingDelayRandomRange < 0
                || (definition.SkeletonKingDelayBase != 0
                    || definition.SkeletonKingDelayRandomRange != 0)
                    && (definition.NativeAi != "SkeletonKing"
                        || definition.SkeletonKingDelayRandomRange <= 0)
                || definition.SkeletonKingMovementChanceBase is < 0 or > 100
                || definition.SkeletonKingMovementChanceIntelligenceScale is < 0 or > 100
                || (definition.SkeletonKingMovementChanceBase != 0
                    || definition.SkeletonKingMovementChanceIntelligenceScale != 0)
                    && (definition.NativeAi != "SkeletonKing"
                        || definition.SkeletonKingMovementChanceBase <= 0
                        || definition.SkeletonKingMovementChanceIntelligenceScale <= 0)
                || definition.RhinoDelayBase < 0
                || definition.RhinoDelayRandomRange < 0
                || (definition.RhinoDelayBase != 0
                    || definition.RhinoDelayRandomRange != 0)
                    && (definition.NativeAi != "Rhino"
                        || definition.RhinoDelayRandomRange <= 0)
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
                || definition.GargoyleAttackChanceBase is < 0 or > 100
                || definition.GargoyleAttackChanceIntelligenceScale is < 0 or > 100
                || ((definition.GargoyleAttackChanceBase != 0
                        || definition.GargoyleAttackChanceIntelligenceScale != 0)
                    && (definition.GargoyleAttackChanceBase <= 0
                        || definition.GargoyleAttackChanceIntelligenceScale <= 0))
                || definition.GargoyleMovementChanceBase is < 0 or > 100
                || definition.GargoyleMovementChanceIntelligenceScale is < 0 or > 100
                || ((definition.GargoyleMovementChanceBase != 0
                        || definition.GargoyleMovementChanceIntelligenceScale != 0)
                    && (definition.GargoyleMovementChanceBase <= 0
                        || definition.GargoyleMovementChanceIntelligenceScale <= 0))
                || definition.GargoyleHealingHealthThresholdPercent is < 0 or > 100
                || definition.GargoyleHealDivisorMultiplier is < 0 or > 256
                || definition.GargoyleHealDivisorRandomRange is < 0 or > 256
                || definition.GargoyleHealDivisorRandomOffset is < 0 or > 256
                || ((definition.GargoyleHealDivisorMultiplier != 0
                        || definition.GargoyleHealDivisorRandomRange != 0
                        || definition.GargoyleHealDivisorRandomOffset != 0)
                    && (definition.GargoyleHealDivisorMultiplier <= 0
                        || definition.GargoyleHealDivisorRandomRange <= 0))
                || definition.ScavengerHealingDurationTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterShortTimerTicks
                || definition.ScavengerCorpseSearchRadius is < 0 or > 64
                || definition.ScavengerHellfireHealingDivisor is < 0 or > 256
                || definition.ScavengerBaseHealingAmount is < 0 or > 256
                || definition.ScavengerBaseHealingTargetPercent is < 0 or > 100
                || definition.ScavengerHealingHealthThresholdPercent is < 0 or > 100
                || ((definition.ScavengerHealingDurationTicks != 0
                        || definition.ScavengerCorpseSearchRadius != 0
                        || definition.ScavengerHellfireHealingDivisor != 0
                        || definition.ScavengerBaseHealingAmount != 0
                        || definition.ScavengerBaseHealingTargetPercent != 0)
                    && (definition.ScavengerHealingDurationTicks <= 0
                        || definition.ScavengerCorpseSearchRadius <= 0
                        || definition.ScavengerHellfireHealingDivisor <= 0
                        || definition.ScavengerBaseHealingAmount <= 0
                        || definition.ScavengerBaseHealingTargetPercent <= 0))
                || definition.SkeletonRangedRetreatMinimumDistance is < 0 or > 64
                || definition.SkeletonRangedRetreatChanceBase is < 0 or > 100
                || definition.SkeletonRangedRetreatChanceIntelligenceScale is < 0 or > 100
                || definition.SkeletonRangedAttackChanceBase is < 0 or > 100
                || definition.SkeletonRangedAttackChanceIntelligenceScale is < 0 or > 100
                || definition.SkeletonRangedMovementFreshChanceBonus is < 0 or > 100
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
                || (definition.SkeletonRangedMovementFreshChanceBonus != 0
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
                || definition.RhinoMovementChanceBase is < 0 or > 100
                || definition.RhinoMovementChanceIntelligenceScale is < 0 or > 100
                || definition.RhinoMovementFreshChanceBonus is < 0 or > 100
                || ((definition.RhinoMovementChanceBase != 0
                        || definition.RhinoMovementChanceIntelligenceScale != 0)
                    && (definition.RhinoMovementChanceBase <= 0
                        || definition.RhinoMovementChanceIntelligenceScale <= 0))
                || (definition.RhinoMovementFreshChanceBonus != 0
                    && (definition.RhinoMovementChanceBase <= 0
                        || definition.RhinoMovementChanceIntelligenceScale <= 0))
                || definition.RhinoMovementGoalMinimumDistance is < 0 or > 100
                || definition.RhinoMovementGoalChanceModulo is < 0 or > 100
                || (definition.RhinoMovementGoalMinimumDistance != 0
                    || definition.RhinoMovementGoalChanceModulo != 0)
                    && (definition.NativeAi != "Rhino"
                        || definition.RhinoMovementGoalMinimumDistance < 2
                        || definition.RhinoMovementGoalChanceModulo <= 0)
                || !this.definitions.TryAdd(definition.MonsterId, definition)) {
                throw new InvalidDataException("Monster type definitions contain invalid or duplicate identities.");
            }
            if (!definitionsByNativeId.TryAdd(definition.NativeMonsterId, definition)) {
                definitionsByNativeId.Remove(definition.NativeMonsterId);
                ambiguousNativeIds.Add(definition.NativeMonsterId);
            }
            if (!definitionsByNativeAi.TryAdd(definition.NativeAi, definition)) {
                definitionsByNativeAi.Remove(definition.NativeAi);
                ambiguousNativeAis.Add(definition.NativeAi);
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

    /** Reports whether a native identity maps to more than one active stable row. */
    public bool IsNativeMonsterIdAmbiguous(uint nativeMonsterId)
        => ambiguousNativeIds.Contains(nativeMonsterId);

    /**
     * Resolves a native monster selector while retaining the numeric stable-ID
     * compatibility path used by older authored content. Ambiguous native IDs
     * never fall through to a stable row because that would select an arbitrary
     * identity from a composed catalog.
     */
    public bool TryResolveNativeMonsterSelector(
        uint selector,
        out AuthoritativeMonsterTypeDefinition definition)
    {
        if (TryGetNative(selector, out definition))
            return true;
        if (IsNativeMonsterIdAmbiguous(selector)) {
            definition = null!;
            return false;
        }
        return TryGet(selector, out definition!);
    }

    /** Resolves an unambiguous native MonsterAI identity without exposing numeric IDs to callers. */
    public bool TryGetNativeAi(string nativeAi, out AuthoritativeMonsterTypeDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeAi);
        if (ambiguousNativeAis.Contains(nativeAi)) {
            definition = null!;
            return false;
        }
        return definitionsByNativeAi.TryGetValue(nativeAi, out definition!);
    }

    public static AuthoritativeMonsterTypeCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeMonsterTypeCatalog(table.Rows.Select(row =>
            new AuthoritativeMonsterTypeDefinition(
                AuthoritativeMonsterIdentity.Resolve(row, stableIds),
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
                row.OptionalBoolean("stone_curse_immune"),
                row.OptionalBoolean("undead"),
                row.OptionalBoolean("berserk_immune")) {
                StableSymbol = AuthoritativeMonsterIdentity.ReadSymbol(row),
                Demon = row.OptionalBoolean("demon"),
                Animal = row.OptionalBoolean("animal"),
                Knockback = row.OptionalBoolean("knockback"),
                Diablo = row.OptionalBoolean("diablo"),
                FireResistance = row.OptionalInt32("fire_resistance"),
                LightningResistance = row.OptionalInt32("lightning_resistance"),
                MagicResistance = row.OptionalInt32("magic_resistance"),
                AcidResistance = row.OptionalInt32("acid_resistance"),
                FireResistanceHell = ReadOptionalResistance(row, "fire_resistance_hell"),
                LightningResistanceHell = ReadOptionalResistance(row, "lightning_resistance_hell"),
                MagicResistanceHell = ReadOptionalResistance(row, "magic_resistance_hell"),
                AcidResistanceHell = ReadOptionalResistance(row, "acid_resistance_hell"),
                AggroRange = row.OptionalInt32("aggro_range", 6),
                DeathAnimationTicks = row.OptionalInt32("death_animation_ticks"),
                StandAnimationFrames = row.OptionalInt32("stand_animation_frames"),
                StandAnimationTicksPerFrame = row.OptionalInt32("stand_animation_ticks_per_frame"),
                ExperienceReward = row.OptionalUInt32("experience_reward"),
                ToHit = row.OptionalInt32("to_hit"),
                ToHitSpecial = row.OptionalInt32("to_hit_special"),
                NativeAi = row.TryGet("native_ai", out var nativeAi)
                    && !string.IsNullOrWhiteSpace(nativeAi)
                    ? nativeAi
                    : "Zombie",
                SkeletonSummonCandidate = row.OptionalNullableBoolean("skeleton_summon_candidate"),
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
                BatMovementChanceBase = row.OptionalInt32("bat_movement_chance_base"),
                BatMovementChanceIntelligenceScale = row.OptionalInt32("bat_movement_chance_intelligence_scale"),
                BatMovementFreshChanceBonus = row.OptionalInt32("bat_movement_fresh_chance_bonus"),
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
                FatMovementFreshChanceBonus = row.OptionalInt32("fat_movement_fresh_chance_bonus"),
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
                SkeletonKingDelayBase = row.OptionalInt32("skeleton_king_delay_base"),
                SkeletonKingDelayRandomRange = row.OptionalInt32("skeleton_king_delay_random_range"),
                SkeletonKingMovementChanceBase = row.OptionalInt32("skeleton_king_movement_chance_base"),
                SkeletonKingMovementChanceIntelligenceScale = row.OptionalInt32("skeleton_king_movement_chance_intelligence_scale"),
                HorkDemonAttackChanceBase = row.OptionalInt32("hork_demon_attack_chance_base"),
                HorkDemonAttackChanceIntelligenceScale = row.OptionalInt32("hork_demon_attack_chance_intelligence_scale"),
                HorkDemonSpawnMinimumDistance = row.OptionalInt32("hork_demon_spawn_minimum_distance"),
                HorkDemonSpawnChanceBase = row.OptionalInt32("hork_demon_spawn_chance_base"),
                HorkDemonSpawnChanceIntelligenceScale = row.OptionalInt32("hork_demon_spawn_chance_intelligence_scale"),
                HorkDemonMovementChanceBase = row.OptionalInt32("hork_demon_movement_chance_base"),
                HorkDemonMovementChanceIntelligenceScale = row.OptionalInt32("hork_demon_movement_chance_intelligence_scale"),
                GargoyleSafeDistanceBase = row.OptionalInt32("gargoyle_safe_distance_base"),
                GargoyleSafeDistanceIntelligenceScale = row.OptionalInt32("gargoyle_safe_distance_intelligence_scale"),
                GargoyleAttackChanceBase = row.OptionalInt32("gargoyle_attack_chance_base"),
                GargoyleAttackChanceIntelligenceScale = row.OptionalInt32("gargoyle_attack_chance_intelligence_scale"),
                GargoyleMovementChanceBase = row.OptionalInt32("gargoyle_movement_chance_base"),
                GargoyleMovementChanceIntelligenceScale = row.OptionalInt32("gargoyle_movement_chance_intelligence_scale"),
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
                SkeletonRangedMovementFreshChanceBonus = row.OptionalInt32("skeleton_ranged_movement_fresh_chance_bonus"),
                RhinoChargeMinimumDistance = row.OptionalInt32("rhino_charge_minimum_distance"),
                RhinoChargeChanceBase = row.OptionalInt32("rhino_charge_chance_base"),
                RhinoChargeChanceIntelligenceScale = row.OptionalInt32("rhino_charge_chance_intelligence_scale"),
                RhinoMovementChanceBase = row.OptionalInt32("rhino_movement_chance_base"),
                RhinoMovementChanceIntelligenceScale = row.OptionalInt32("rhino_movement_chance_intelligence_scale"),
                RhinoMovementFreshChanceBonus = row.OptionalInt32("rhino_movement_fresh_chance_bonus"),
                RhinoMovementGoalMinimumDistance = row.OptionalInt32("rhino_movement_goal_minimum_distance"),
                RhinoMovementGoalChanceModulo = row.OptionalInt32("rhino_movement_goal_chance_modulo"),
                RhinoDelayBase = row.OptionalInt32("rhino_delay_base"),
                RhinoDelayRandomRange = row.OptionalInt32("rhino_delay_random_range"),
                SkeletonMeleeMovementDelayBase = row.OptionalInt32("skeleton_melee_movement_delay_base"),
                SkeletonMeleeMovementDelayIntelligenceScale = row.OptionalInt32("skeleton_melee_movement_delay_intelligence_scale"),
                SkeletonMeleeMovementDelayRandomRange = row.OptionalInt32("skeleton_melee_movement_delay_random_range"),
                SkeletonMeleeAttackDelayBase = row.OptionalInt32("skeleton_melee_attack_delay_base"),
                SkeletonMeleeAttackDelayIntelligenceScale = row.OptionalInt32("skeleton_melee_attack_delay_intelligence_scale"),
                SkeletonMeleeAttackDelayRandomRange = row.OptionalInt32("skeleton_melee_attack_delay_random_range"),
                AvoidanceDelayBase = row.OptionalInt32("avoidance_delay_base"),
                AvoidanceDelayRandomRange = row.OptionalInt32("avoidance_delay_random_range"),
                CounselorDelayBase = row.OptionalInt32("counselor_delay_base"),
                CounselorDelayIntelligenceScale = row.OptionalInt32("counselor_delay_intelligence_scale"),
                CounselorDelayRandomRange = row.OptionalInt32("counselor_delay_random_range"),
                CounselorAttackDelayBase = row.OptionalInt32("counselor_attack_delay_base"),
                CounselorAttackDelayRandomRange = row.OptionalInt32("counselor_attack_delay_random_range"),
                CounselorMoveChancePercent = row.OptionalInt32("counselor_move_chance_percent"),
                CounselorAttackChanceBase = row.OptionalInt32("counselor_attack_chance_base"),
                CounselorAttackChanceIntelligenceScale = row.OptionalInt32("counselor_attack_chance_intelligence_scale"),
                CounselorAdjacentAttackChanceBase = row.OptionalInt32("counselor_adjacent_attack_chance_base"),
                CounselorAdjacentAttackChanceIntelligenceScale = row.OptionalInt32("counselor_adjacent_attack_chance_intelligence_scale"),
                MegaDelayChanceBase = row.OptionalInt32("mega_delay_chance_base"),
                MegaDelayChanceIntelligenceScale = row.OptionalInt32("mega_delay_chance_intelligence_scale"),
                MegaIntermediateMovementChanceBase = row.OptionalInt32("mega_intermediate_movement_chance_base"),
                MegaIntermediateMovementChanceIntelligenceScale = row.OptionalInt32("mega_intermediate_movement_chance_intelligence_scale"),
                SkeletonKingRoundWalkChanceModulo = row.OptionalInt32("skeleton_king_round_walk_chance_modulo"),
                HorkDemonRoundWalkChanceModulo = row.OptionalInt32("hork_demon_round_walk_chance_modulo"),
                AiAvoidanceMovementMinimumDistance = row.OptionalInt32("ai_avoidance_movement_minimum_distance"),
                AiAvoidanceMovementChanceModulo = row.OptionalInt32("ai_avoidance_movement_chance_modulo"),
                AiAvoidanceMovementFreshChanceBonus = row.OptionalInt32("ai_avoidance_movement_fresh_chance_bonus"),
                AiAvoidanceRoundWalkChanceBase = row.OptionalInt32("ai_avoidance_round_walk_chance_base"),
                AiAvoidanceRoundWalkChanceIntelligenceScale = row.OptionalInt32("ai_avoidance_round_walk_chance_intelligence_scale"),
            }));
    }

    private static int? ReadOptionalResistance(TsvRow row, string column)
        => row.TryGet(column, out _) ? row.OptionalInt32(column) : null;
}
