using Devilution.Data;
using Devilution.Engine;
using Devilution.Shared;

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
    NovaController = 30,
    NovaBall = 31,
    ApocalypseController = 32,
    ApocalypseBoom = 33,
    RageController = 34,
    Elemental = 35,
    GuardianController = 36,
    Telekinesis = 37,
    TownPortal = 38,
    Reflect = 39,
    Magi = 40,
    Mana = 41,
    RingOfFireController = 42,
    ImmolationController = 43,
    Jester = 44,
    Berserk = 45,
    Warp = 46,
    LightningWallController = 47,
    Golem = 48,
    Etherealize = 49,
    /** Direct non-player magic missile emitted by a server-owned monster AI. */
    MonsterMagic = 50,
    /** Native monster Acid projectile, including its expiry splat transition. */
    Acid = 51,
    /** Native Acid expiry visual that creates a puddle on its final tick. */
    AcidSplat = 52,
    /** Native persistent Acid puddle that checks players every active tick. */
    AcidPuddle = 53,
    /** Native Snake Rhino charge controller that moves the source monster. */
    SnakeCharge = 54,
    /** Native Gloom Rhino controller that moves the source monster without a hit. */
    BatCharge = 55,
    /** Native Counselor Charged Bolt emitted by a monster-owned controller. */
    MonsterChargedBolt = 56,
    /** Native Hork Demon missile that creates a Hork Spawn on expiry. */
    HorkSpawn = 57,
    /** Native player bow missile, including configured delayed Multiple Arrows shots. */
    WeaponArrow = 58,
    /** Native monster Arrow missile emitted by GoatRanged and SkeletonRanged. */
    MonsterArrow = 59,
}

/** Special handling selected by validated consumable-spell content. */
public enum AuthoritativeConsumableBehavior
{
    Standard,
    RuneFire,
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
    /** Native `SpellID` identity retained separately from the stable content ID. */
    public uint NativeSpellId { get; init; }

    /** Optional server-owned presentation name supplied by the active content pack. */
    public string DisplayName { get; init; } = string.Empty;

    /** Native rune miscellaneous ID that resolves to this stable spell row. */
    public int ConsumableMiscId { get; init; }

    /** Optional legacy-compatible override for special consumable cast handling. */
    public AuthoritativeConsumableBehavior? ConsumableBehavior { get; init; }

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

    /** Inclusive upper bound for Mana's initial native random restoration roll. */
    public int ManaRandomRange { get; init; }

    /** Inclusive upper bound for Mana's character-level restoration rolls. */
    public int ManaCharacterLevelRandomRange { get; init; }

    /** Inclusive upper bound for Mana's learned-spell-level restoration rolls. */
    public int ManaSpellLevelRandomRange { get; init; }

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

    /** Native ten-way dispatch table consumed by the Jester behavior. */
    public IReadOnlyList<uint> JesterSpellIds { get; init; } = [];

    /** Native random damage is GenerateRnd(current magic / divisor) + 1. */
    public int RandomMagicDamageDivisor { get; init; }

    /** Exclusive upper bound passed to native GenerateRnd for base damage. */
    public int RandomDamageRange { get; init; }

    /** Divides current magic before it contributes to projectile damage. */
    public int MagicDamageDivisor { get; init; }

    /** Exclusive upper bound for Charged Bolt's native initial path roll. */
    public int ChargedBoltPathRandomRange { get; init; }

    /** Optional 16-entry steering pattern; empty preserves the native pattern. */
    public IReadOnlyList<int> ChargedBoltPathPattern { get; init; } = [];

    /** Optional nine-cell impact order; empty preserves the native Fireball order. */
    public IReadOnlyList<(int X, int Y)> FireballExplosionOffsets { get; init; } = [];

    /** Optional nine-cell burst order; empty preserves the native Elemental order. */
    public IReadOnlyList<(int X, int Y)> ElementalBurstOffsets { get; init; } = [];

    /** Optional six-cell lower Flash layer order; empty preserves the native order. */
    public IReadOnlyList<(int X, int Y)> FlashBottomOffsets { get; init; } = [];

    /** Optional three-cell upper Flash layer order; empty preserves the native order. */
    public IReadOnlyList<(int X, int Y)> FlashTopOffsets { get; init; } = [];

    /** Optional 48-cell Guardian firing arc; empty preserves the native order. */
    public IReadOnlyList<(int X, int Y)> GuardianArcOffsets { get; init; } = [];

    /** Monster-level penalty applied by Charged Bolt's native magic-hit roll. */
    public int ChargedBoltHitMonsterLevelScale { get; init; } = 2;

    /** Inclusive lower bound for Charged Bolt's native magic-hit roll. */
    public int ChargedBoltMinimumHitChance { get; init; } = 5;

    /** Inclusive upper bound for Charged Bolt's native magic-hit roll. */
    public int ChargedBoltMaximumHitChance { get; init; } = 95;

    /** Monster-level penalty applied by Holy Bolt's native magic-hit roll. */
    public int HolyBoltHitMonsterLevelScale { get; init; } = 2;

    /** Inclusive lower bound for Holy Bolt's native magic-hit roll. */
    public int HolyBoltMinimumHitChance { get; init; } = 5;

    /** Inclusive upper bound for Holy Bolt's native magic-hit roll. */
    public int HolyBoltMaximumHitChance { get; init; } = 95;

    /** Monster-level penalty applied by Firebolt's native magic-hit roll. */
    public int FireboltHitMonsterLevelScale { get; init; } = 2;

    /** Inclusive lower bound for Firebolt's native magic-hit roll. */
    public int FireboltMinimumHitChance { get; init; } = 5;

    /** Inclusive upper bound for Firebolt's native magic-hit roll. */
    public int FireboltMaximumHitChance { get; init; } = 95;

    /** Monster-level penalty applied by Bone Spirit's native magic-hit roll. */
    public int BoneSpiritHitMonsterLevelScale { get; init; } = 2;

    /** Inclusive lower bound for Bone Spirit's native magic-hit roll. */
    public int BoneSpiritMinimumHitChance { get; init; } = 5;

    /** Inclusive upper bound for Bone Spirit's native magic-hit roll. */
    public int BoneSpiritMaximumHitChance { get; init; } = 95;

    /** Divisor applied to a monster's current life when Bone Spirit hits. */
    public int BoneSpiritDamageHitPointsDivisor { get; init; } = 3;

    /** Divisor applied to a player's current life when Bone Spirit hits. */
    public int BoneSpiritPlayerDamageHitPointsDivisor { get; init; } = 3;

    /** Monster-level penalty for shared persistent-projectile magic-hit rolls. */
    public int PersistentProjectileHitMonsterLevelScale { get; init; } = 2;

    /** Inclusive lower bound for shared persistent-projectile magic-hit rolls. */
    public int PersistentProjectileMinimumHitChance { get; init; } = 5;

    /** Inclusive upper bound for shared persistent-projectile magic-hit rolls. */
    public int PersistentProjectileMaximumHitChance { get; init; } = 95;

    /** Character-level penalty for shared persistent-projectile player-hit rolls. */
    public int PersistentProjectileHitCharacterLevelScale { get; init; } = 2;

    /** Inclusive lower bound for shared persistent-projectile player-hit rolls. */
    public int PersistentProjectilePlayerMinimumHitChance { get; init; } = 5;

    /** Inclusive upper bound for shared persistent-projectile player-hit rolls. */
    public int PersistentProjectilePlayerMaximumHitChance { get; init; } = 95;

    /** Divisor applied to shared persistent-projectile player damage. */
    public int PersistentProjectilePlayerDamageDivisor { get; init; } = 2;

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

    /** Native Berserk magic-resistance success frequency; zero keeps legacy 2. */
    public int BerserkResistanceFrequency { get; init; }

    /** Divides character level before adding it to generated damage. */
    public int CharacterLevelDamageDivisor { get; init; }

    /** Each effective spell level adds the current damage divided by this value. */
    public int SpellEffectScaleDivisor { get; init; }

    /** Adds this percentage of scaled damage after spell-level scaling. */
    public int PostScaleDamagePercent { get; init; }

    /** Native monster Flash damage multiplier applied to the effective monster level. */
    public int FlashMonsterDamageLevelScale { get; init; } = 2;

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

    /** Optional ordered initial placement probes; empty preserves the native probes. */
    public IReadOnlyList<(int X, int Y)> ResurrectionInitialOffsets { get; init; } = [];

    /** Maximum native crawl radius used to find Teleport's destination. */
    public int TeleportSearchRadius { get; init; } = 5;

    /** Maximum native crawl radius used to find Warp's destination. */
    public int WarpSearchRadius { get; init; } = 5;

    /** Maximum native crawl radius used to place Town Portal. */
    public int TownPortalSearchRadius { get; init; } = 5;

    /** Native lifetime, in ticks, of a server-owned Town Portal. */
    public int TownPortalLifetime { get; init; } = 100;

    /** X coordinate of the first native Town Portal destination slot. */
    public int TownPortalDestinationPositionX { get; init; } = AuthoritativePortalRules.TownPositionX;

    /** Y coordinate shared by native Town Portal destination slots. */
    public int TownPortalDestinationPositionY { get; init; } = AuthoritativePortalRules.TownPositionY;

    /** Number of native Town Portal destination slots before wrapping. */
    public int TownPortalDestinationSlotCount { get; init; } = AuthoritativePortalRules.TownPortalSlotCount;

    /** X distance between native Town Portal destination slots. */
    public int TownPortalDestinationSlotSpacing { get; init; } = AuthoritativePortalRules.TownPortalSlotSpacing;

    /** Reciprocal base used by native Mana Shield damage conversion. */
    public int ManaShieldBaseDamageReductionDivisor { get; init; }

    /** Amount subtracted from the reciprocal for each learned spell level. */
    public int ManaShieldDamageReductionLevelStep { get; init; }

    /** Maximum learned spell level used by Mana Shield damage conversion. */
    public int ManaShieldDamageReductionMaximumLevel { get; init; }

    /** Native Reflect spell level used when a cast has no explicit level. */
    public int ReflectDefaultSpellLevel { get; init; }

    /** Optional inclusive reflected-damage percentage bounds. */
    public int ReflectDamagePercentMinimum { get; init; }

    public int ReflectDamagePercentMaximum { get; init; }

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

    /** Native Ring of Fire crawl radius around its caster. */
    public int RingOfFireSearchRadius { get; init; }

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

    /** Native Nova controller lifetime in simulation ticks. */
    public int NovaControllerLifetime { get; init; }

    /** Native Nova projectile radius in tiles. */
    public int NovaSearchRadius { get; init; }

    /** Number of Nova projectiles emitted by one controller. */
    public int NovaProjectileCount { get; init; }

    /** Native Immolation controller lifetime, radial search radius, and fan-out count. */
    public int ImmolationControllerLifetime { get; init; }
    public int ImmolationSearchRadius { get; init; }
    public int ImmolationProjectileCount { get; init; }

    /** Native Apocalypse scan radius in tiles. */
    public int ApocalypseSearchRadius { get; init; }

    /** Native Apocalypse controller animation lifetime. */
    public int ApocalypseControllerLifetime { get; init; }

    /** Native Rage active/cooldown lifetime before the character-level extension. */
    public int RageControllerLifetime { get; init; }

    /** Native Rage lifetime added for each character level. */
    public int RageCharacterLevelLifetimeScale { get; init; }

    /** Native Rage self-damage Q6 units applied per character level on expiry. */
    public int RageSelfDamageCharacterLevel { get; init; }

    /** Native Rage minimum life Q6 units required per character level to cast. */
    public int RageMinimumLifeQ6PerCharacterLevel { get; init; }

    /** Native Elemental lifetime used after its first destination is reached. */
    public int ElementalRetargetLifetime { get; init; }

    /** Native Elemental character-level damage multiplier before spell scaling. */
    public int ElementalCharacterLevelDamageMultiplier { get; init; }

    /** Native Guardian controller duration and targeting constants. */
    public int GuardianDurationTickScale { get; init; }
    public int GuardianMinimumDuration { get; init; }
    public int GuardianMaximumDuration { get; init; }
    public int GuardianCharacterLevelDurationDivisor { get; init; }
    public int GuardianSpawnSearchRadius { get; init; }
    public int GuardianTargetSearchRadius { get; init; }
    public int GuardianFireInterval { get; init; }
    public int GuardianProjectileSpeedLevelStep { get; init; }
    public int GuardianProjectileSpeedMaximumBonus { get; init; }

    /** Native Berserk target search and four damage-channel scaling constants. */
    public int BerserkSearchRadius { get; init; }

    public int BerserkDamagePercentBase { get; init; }

    public int BerserkDamagePercentRandomRange { get; init; }

    public int BerserkSpellLevelBonus { get; init; }

    /** Native Golem death-animation duration in simulation ticks. */
    public int GolemDeathAnimationTicks { get; init; }

    /** Native Golem summon health and damage formula terms. */
    /** Stable monster identity used by the Golem summon, when supplied by content. */
    public uint GolemMonsterId { get; init; }

    public int GolemHitPointsLevelBaseQ6 { get; init; } = 320;

    public int GolemHitPointsLevelMultiplier { get; init; } = 2;

    public int GolemHitPointsManaDivisor { get; init; } = 3;

    public int GolemDamageLevelMultiplier { get; init; } = 2;

    public int GolemMinimumDamageLevelOffset { get; init; } = 4;

    public int GolemMaximumDamageLevelOffset { get; init; } = 8;

    public int GolemArmorClass { get; init; } = 25;
}

public sealed class AuthoritativeSpellCatalog
{
    private const uint MaximumNativeSpellId = NativeCompatibilityLimits.MaximumNativeSpellId;
    public const int LegacyRuneFireConsumableMiscId = 47;
    private const int FirstRuneMiscId = LegacyRuneFireConsumableMiscId;
    private const int LastRuneMiscId = 51;
    private readonly Dictionary<uint, AuthoritativeSpellDefinition> spells;
    private readonly Dictionary<uint, AuthoritativeSpellDefinition> nativeSpells;
    private readonly Dictionary<int, AuthoritativeSpellDefinition> consumableSpells;

    public AuthoritativeSpellCatalog(IEnumerable<AuthoritativeSpellDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        spells = new Dictionary<uint, AuthoritativeSpellDefinition>();
        nativeSpells = new Dictionary<uint, AuthoritativeSpellDefinition>();
        consumableSpells = new Dictionary<int, AuthoritativeSpellDefinition>();
        foreach (var definition in definitions.Select(NormalizeLegacyConsumableBehavior)) {
            if (definition.SpellId == 0 || definition.ManaCost <= 0 || definition.HealingAmount < 0 || definition.DamageAmount < 0)
                throw new InvalidDataException("Spell definitions require a non-zero ID, positive mana cost, and non-negative effects.");
            if (definition.DisplayName is null || definition.DisplayName.Contains('\0'))
                throw new InvalidDataException(
                    $"Spell {definition.SpellId} has invalid presentation metadata.");
            if (definition.NativeSpellId > MaximumNativeSpellId)
                throw new InvalidDataException(
                    $"Spell {definition.SpellId} has an invalid native spell ID {definition.NativeSpellId}.");
            if (definition.ConsumableMiscId != 0
                && (definition.ConsumableMiscId < FirstRuneMiscId
                    || definition.ConsumableMiscId > LastRuneMiscId)) {
                throw new InvalidDataException(
                    $"Spell {definition.SpellId} has an invalid consumable miscellaneous ID {definition.ConsumableMiscId}. "
                    + $"Only rune IDs {FirstRuneMiscId} through {LastRuneMiscId} are supported.");
            }
            if (definition.ConsumableBehavior is not null
                && !Enum.IsDefined(definition.ConsumableBehavior.Value))
                throw new InvalidDataException(
                    $"Spell {definition.SpellId} has an invalid consumable behavior.");
            if (definition.ManaAdjustment < 0
                || definition.ManaAdjustmentDivisor <= 0
                || definition.CharacterLevelManaCost < 0
                || definition.MinimumManaCost < 0
                || definition.ManaCostScale <= 0
                || definition.ManaCostScale > 64) {
                throw new InvalidDataException("Spell mana scaling is invalid.");
            }
            if (definition.ManaRandomRange is < 0 or > short.MaxValue
                || definition.ManaCharacterLevelRandomRange is < 0 or > short.MaxValue
                || definition.ManaSpellLevelRandomRange is < 0 or > short.MaxValue) {
                throw new InvalidDataException("Mana restoration random ranges are invalid.");
            }
            var hasManaRestorationTuning = definition.ManaRandomRange != 0
                || definition.ManaCharacterLevelRandomRange != 0
                || definition.ManaSpellLevelRandomRange != 0;
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Mana
                && hasManaRestorationTuning
                && (definition.ManaRandomRange <= 0
                    || definition.ManaCharacterLevelRandomRange <= 0
                    || definition.ManaSpellLevelRandomRange <= 0)) {
                throw new InvalidDataException(
                    "Mana behavior requires all native restoration random ranges when tuning is declared.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Mana
                && hasManaRestorationTuning) {
                throw new InvalidDataException(
                    "Mana restoration tuning requires Mana projectile behavior.");
            }
            if (definition.StaffLevel < -1)
                throw new InvalidDataException("Spell staff level cannot be less than the native unavailable sentinel.");
            if (definition.JesterSpellIds is null
                || definition.JesterSpellIds.Count is not (0 or 10)
                || definition.JesterSpellIds.Any(spellId => spellId == 0)) {
                throw new InvalidDataException(
                    $"Spell {definition.SpellId} has an invalid Jester dispatch table.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.WeaponArrow)
                throw new InvalidDataException(
                    "WeaponArrow is a player attack projectile and cannot be declared as a spell behavior.");
            var hasDamage = definition.DamageAmount > 0
                || definition.RandomMagicDamageDivisor > 0
                || definition.RandomDamageRange > 0
                || definition.CharacterLevelRandomDamageDivisor > 0
                || definition.MagicDamageDivisor > 0
                || definition.SpellLevelDamage > 0
                || definition.ProjectileBehavior is AuthoritativeProjectileBehavior.BoneSpirit
                    or AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.FireWallSegment
                    or AuthoritativeProjectileBehavior.LightningWallController
                    or AuthoritativeProjectileBehavior.RingOfFireController
                    or AuthoritativeProjectileBehavior.ImmolationController
                    or AuthoritativeProjectileBehavior.Jester
                    or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment
                    or AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment
                    or AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt
                    or AuthoritativeProjectileBehavior.NovaController
                    or AuthoritativeProjectileBehavior.NovaBall
                    or AuthoritativeProjectileBehavior.ApocalypseController
                    or AuthoritativeProjectileBehavior.ApocalypseBoom;
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
                || definition.ChargedBoltPathRandomRange < 0
                || definition.ChargedBoltHitMonsterLevelScale < 0
                || definition.ChargedBoltMinimumHitChance is < 0 or > 100
                || definition.ChargedBoltMaximumHitChance is < 0 or > 100
                || definition.HolyBoltHitMonsterLevelScale < 0
                || definition.HolyBoltMinimumHitChance is < 0 or > 100
                || definition.HolyBoltMaximumHitChance is < 0 or > 100
                || definition.FireboltHitMonsterLevelScale < 0
                || definition.FireboltMinimumHitChance is < 0 or > 100
                || definition.FireboltMaximumHitChance is < 0 or > 100
                || definition.BoneSpiritHitMonsterLevelScale < 0
                || definition.BoneSpiritMinimumHitChance is < 0 or > 100
                || definition.BoneSpiritMaximumHitChance is < 0 or > 100
                || definition.BoneSpiritDamageHitPointsDivisor <= 0
                || definition.BoneSpiritPlayerDamageHitPointsDivisor <= 0
                || definition.PersistentProjectileHitMonsterLevelScale < 0
                || definition.PersistentProjectileMinimumHitChance is < 0 or > 100
                || definition.PersistentProjectileMaximumHitChance is < 0 or > 100
                || definition.PersistentProjectileHitCharacterLevelScale < 0
                || definition.PersistentProjectilePlayerMinimumHitChance is < 0 or > 100
                || definition.PersistentProjectilePlayerMaximumHitChance is < 0 or > 100
                || definition.PersistentProjectilePlayerDamageDivisor <= 0
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
                || definition.RageControllerLifetime < 0
                || definition.RageCharacterLevelLifetimeScale < 0
                || definition.RageSelfDamageCharacterLevel < 0
                || definition.RageMinimumLifeQ6PerCharacterLevel < 0
                || definition.ElementalRetargetLifetime < 0
                || definition.ElementalCharacterLevelDamageMultiplier < 0
                || definition.GuardianDurationTickScale < 0
                || definition.GuardianMinimumDuration < 0
                || definition.GuardianMaximumDuration < 0
                || definition.GuardianCharacterLevelDurationDivisor < 0
                || definition.GuardianSpawnSearchRadius < 0
                || definition.GuardianTargetSearchRadius < 0
                || definition.GuardianFireInterval < 0
                || definition.GuardianProjectileSpeedLevelStep < 0
                || definition.GuardianProjectileSpeedMaximumBonus < 0
                || definition.ReflectDefaultSpellLevel < 0
                || definition.ReflectDamagePercentMinimum is < 0 or > 100
                || definition.ReflectDamagePercentMaximum is < 0 or > 100
                || definition.BerserkResistanceFrequency < 0
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
            if (definition.BerserkSearchRadius < 0
                || definition.BerserkDamagePercentBase < 0
                || definition.BerserkDamagePercentRandomRange < 0
                || definition.BerserkSpellLevelBonus < 0) {
                throw new InvalidDataException("Berserk tuning cannot be negative.");
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
                    || definition.AreaRadius != 0
                    || definition.ChargedBoltMaximumHitChance
                        < definition.ChargedBoltMinimumHitChance)) {
                throw new InvalidDataException(
                    "Charged Bolt behavior requires movement, random magic damage, and no area radius.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.ChargedBolt
                && definition.ChargedBoltPathPattern.Count is not (0 or 16)) {
                throw new InvalidDataException(
                    "Charged Bolt path tuning requires exactly sixteen steering entries when configured.");
            }
            if (definition.ChargedBoltPathPattern.Any(turn => turn is < -1 or > 1)) {
                throw new InvalidDataException(
                    "Charged Bolt path steering entries must be -1, 0, or 1.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Fireball
                && definition.FireballExplosionOffsets.Count is not (0 or 9)) {
                throw new InvalidDataException(
                    "Fireball explosion tuning requires exactly nine offsets when configured.");
            }
            if (definition.FireballExplosionOffsets.Any(offset =>
                    offset.X is < -1 or > 1 || offset.Y is < -1 or > 1)) {
                throw new InvalidDataException(
                    "Fireball explosion offsets must remain within the native adjacent-cell range.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Elemental
                && definition.ElementalBurstOffsets.Count is not (0 or 9)) {
                throw new InvalidDataException(
                    "Elemental burst tuning requires exactly nine offsets when configured.");
            }
            if (definition.ElementalBurstOffsets.Any(offset =>
                    offset.X is < -1 or > 1 || offset.Y is < -1 or > 1)) {
                throw new InvalidDataException(
                    "Elemental burst offsets must remain within the native adjacent-cell range.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Flash
                && definition.FlashBottomOffsets.Count is not (0 or 6)) {
                throw new InvalidDataException(
                    "Flash bottom-layer tuning requires exactly six offsets when configured.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Flash
                && definition.FlashTopOffsets.Count is not (0 or 3)) {
                throw new InvalidDataException(
                    "Flash top-layer tuning requires exactly three offsets when configured.");
            }
            if (definition.FlashBottomOffsets.Any(offset =>
                    offset.X is < -1 or > 1 || offset.Y is < -1 or > 1)
                || definition.FlashTopOffsets.Any(offset =>
                    offset.X is < -1 or > 1 || offset.Y is < -1 or > 1)) {
                throw new InvalidDataException(
                    "Flash layer offsets must remain within the native adjacent-cell range.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.GuardianController
                && definition.GuardianArcOffsets.Count is not (0 or 48)) {
                throw new InvalidDataException(
                    "Guardian arc tuning requires exactly 48 offsets when configured.");
            }
            if (definition.GuardianArcOffsets.Any(offset =>
                    offset.X is < 0 or > 6 || offset.Y is < 0 or > 6)) {
                throw new InvalidDataException(
                    "Guardian arc offsets must remain within the native six-cell range.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.HolyBolt
                && definition.HolyBoltMaximumHitChance < definition.HolyBoltMinimumHitChance) {
                throw new InvalidDataException(
                    "Holy Bolt hit chance bounds must be ordered.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Firebolt
                && definition.FireboltMaximumHitChance < definition.FireboltMinimumHitChance) {
                throw new InvalidDataException(
                    "Firebolt hit chance bounds must be ordered.");
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
                    || definition.SelfDamage != 6
                    || definition.BoneSpiritMaximumHitChance
                        < definition.BoneSpiritMinimumHitChance)) {
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
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.LightningWallController
                && (definition.HealingAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.DamageType != AuthoritativeDamageType.Lightning
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0
                    || definition.RandomDamageRange != 10
                    || definition.RandomDamageIterationsBonus != 2
                    || definition.DamageBonus != 2
                    || definition.DamageScale != 16
                    || definition.ResistantDamageDivisor != 4
                    || definition.WallControlLifetime != 7
                    || definition.WallSegmentBaseDuration != 255
                    || definition.WallSegmentSpellLevelDurationStep != 255
                    || definition.WallSegmentCharacterLevelDurationStep != 0
                    || definition.WallSegmentDurationScale != 1
                    || definition.WallCharacterLevelDamageMultiplier != 1)) {
                throw new InvalidDataException(
                    "Lightning Wall behavior requires the native seven-tick controller and persistent lightning-segment tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.RingOfFireController
                && (definition.HealingAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.DamageType != AuthoritativeDamageType.Fire
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0
                    || definition.RandomDamageRange != 10
                    || definition.RandomDamageIterationsBonus != 2
                    || definition.DamageBonus != 2
                    || definition.DamageScale != 8
                    || definition.ResistantDamageDivisor != 4
                    || definition.RingOfFireSearchRadius != 3
                    || definition.WallControlLifetime != 0
                    || definition.WallSegmentBaseDuration != 10
                    || definition.WallSegmentSpellLevelDurationStep != 10
                    || definition.WallSegmentCharacterLevelDurationStep != 1
                    || definition.WallSegmentDurationScale != 16
                    || definition.WallCharacterLevelDamageMultiplier != 1)) {
                throw new InvalidDataException(
                    "Ring of Fire behavior requires the native radius-three crawl and Fire Wall segment tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.ImmolationController
                && (definition.HealingAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.DamageType != AuthoritativeDamageType.Fire
                    || definition.ProjectileSpeed != 16
                    || definition.ProjectileSpeedSpellLevelStep != 1
                    || definition.ProjectileSpeedMaximumBonus != 34
                    || definition.ProjectileLifetime != 256
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0
                    || definition.RandomDamageRange != 6
                    || definition.RandomDamageIterationsBonus != 5
                    || definition.CharacterLevelDamageDivisor != 1
                    || definition.DamageBonus != 5
                    || definition.MagicDamageDivisor != 2
                    || definition.SpellEffectScaleDivisor != 8
                    || definition.DamageScale != 64
                    || definition.ResistantDamageDivisor != 4
                    || definition.ImmolationControllerLifetime != 1
                    || definition.ImmolationSearchRadius != 4
                    || definition.ImmolationProjectileCount is < 1 or > 36)) {
                throw new InvalidDataException(
                    "Immolation behavior requires the native moving fire-burst tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Jester
                && (definition.HealingAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0
                    || definition.RandomDamageRange != 0
                    || definition.RandomMagicDamageDivisor != 0
                    || definition.MagicDamageDivisor != 0
                    || definition.SpellLevelDamage != 0
                    || definition.DamageBonus != 0
                    || definition.CharacterLevelRandomDamageDivisor != 0
                    || definition.DamageScale != 1
                    || definition.ResistantDamageDivisor != 1
                    || definition.ProjectileImpactLifetime != 1)) {
                throw new InvalidDataException(
                    "Jester behavior requires the native targeted dispatch-only tuning.");
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
            if ((definition.ProjectileBehavior is AuthoritativeProjectileBehavior.NovaController
                    or AuthoritativeProjectileBehavior.NovaBall)
                && (definition.ProjectileSpeed != 16
                    || definition.ProjectileLifetime != 255
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Lightning
                    || definition.ResistantDamageDivisor != 4
                    || definition.NovaControllerLifetime != 1
                    || definition.NovaSearchRadius != 4
                    || definition.NovaProjectileCount is < 1 or > 36
                    || definition.RandomDamageRange != 6
                    || definition.RandomDamageIterationsBonus != 5
                    || definition.CharacterLevelDamageDivisor != 1
                    || definition.DamageBonus != 5
                    || definition.MagicDamageDivisor != 2
                    || definition.SpellEffectScaleDivisor != 8
                    || definition.DamageScale != 64)) {
                throw new InvalidDataException(
                    "Nova behavior requires the native one-tick radial Lightning fan-out tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.NovaBall)
                throw new InvalidDataException(
                    "NovaBall is runtime-owned and cannot be selected by spell content.");
            if ((definition.ProjectileBehavior is AuthoritativeProjectileBehavior.ApocalypseController
                    or AuthoritativeProjectileBehavior.ApocalypseBoom)
                && (definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 15
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ResistantDamageDivisor != 1
                    || definition.ApocalypseControllerLifetime != 255
                    || definition.ApocalypseSearchRadius != 8
                    || definition.RandomDamageRange != 6
                    || definition.RandomDamageIterationsCharacterLevelDivisor != 1
                    || definition.RandomDamageIterationsBonus != 0
                    || definition.CharacterLevelDamageDivisor != 0
                    || definition.DamageBonus != 0
                    || definition.DamageScale != 1)) {
                throw new InvalidDataException(
                    "Apocalypse behavior requires the native row-major Magic controller tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.ApocalypseBoom)
                throw new InvalidDataException(
                    "ApocalypseBoom is runtime-owned and cannot be selected by spell content.");
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.RageController
                && (definition.HealingAmount != 0
                    || definition.DamageAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 245
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Physical
                    || definition.RageControllerLifetime != 245
                    || definition.RageCharacterLevelLifetimeScale != 2
                    || definition.RageSelfDamageCharacterLevel != 6)) {
                throw new InvalidDataException(
                    "Rage behavior requires the native active/cooldown controller tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Elemental
                && (definition.HealingAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.Range != 1
                    || definition.AreaRadius != 1
                    || definition.DamageType != AuthoritativeDamageType.Fire
                    || definition.ProjectileSpeed != 16
                    || definition.ProjectileLifetime != 256
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.RandomDamageRange != 10
                    || definition.RandomDamageIterationsBonus != 2
                    || definition.DamageAmount != 4
                    || definition.MagicDamageDivisor != 2
                    || definition.SpellEffectScaleDivisor != 8
                    || definition.ResistantDamageDivisor != 4
                    || definition.ProjectileImpactLifetime != 14
                    || definition.ElementalRetargetLifetime != 255
                    || definition.ElementalCharacterLevelDamageMultiplier != 2)) {
                throw new InvalidDataException(
                    "Elemental behavior requires the native retargeting fire-burst tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.GuardianController
                && (definition.HealingAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.Range <= 0
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Fire
                    || definition.ProjectileSpeed != 16
                    || definition.ProjectileLifetime != 256
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.RandomDamageRange != 10
                    || definition.MagicDamageDivisor != 8
                    || definition.SpellLevelDamage != 1
                    || definition.DamageBonus != 1
                    || definition.ResistantDamageDivisor != 4
                    || definition.ProjectileImpactLifetime != 1
                    || definition.GuardianDurationTickScale != 16
                    || definition.GuardianMinimumDuration != 30
                    || definition.GuardianMaximumDuration != 30
                    || definition.GuardianCharacterLevelDurationDivisor != 2
                    || definition.GuardianSpawnSearchRadius != 5
                    || definition.GuardianTargetSearchRadius != 6
                    || definition.GuardianFireInterval != 16
                    || definition.GuardianProjectileSpeedLevelStep != 2
                    || definition.GuardianProjectileSpeedMaximumBonus != 47)) {
                throw new InvalidDataException(
                    "Guardian behavior requires the native turret and Firebolt tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Golem
                && (definition.HealingAmount != 0
                    || definition.DamageAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.Range <= 0
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Fire
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.StaffLevel != 9
                    || definition.GolemDeathAnimationTicks <= 0
                    || definition.GolemHitPointsLevelBaseQ6 <= 0
                    || definition.GolemHitPointsLevelMultiplier <= 0
                    || definition.GolemHitPointsManaDivisor <= 0
                    || definition.GolemDamageLevelMultiplier <= 0
                    || definition.GolemMinimumDamageLevelOffset < 0
                    || definition.GolemMaximumDamageLevelOffset < definition.GolemMinimumDamageLevelOffset
                    || definition.GolemArmorClass < 0)) {
                throw new InvalidDataException(
                    "Golem behavior requires the native targeted summon tuning.");
            }
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
                    || definition.DamageScale != 1
                    || definition.FlashMonsterDamageLevelScale <= 0)) {
                throw new InvalidDataException(
                    "Flash behavior requires two stationary, repeated random-damage layers with native scaling and a positive monster damage multiplier.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Flash
                && definition.FlashMonsterDamageLevelScale != 2) {
                throw new InvalidDataException(
                    "Flash monster damage tuning requires Flash projectile behavior.");
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
                    || definition.AreaRadius != 0
                    || definition.TeleportSearchRadius <= 0
                    || definition.TeleportSearchRadius > DeterministicGridCrawl.MaximumSupportedRadius)) {
                throw new InvalidDataException(
                    "Teleport behavior requires one stationary two-tick movement controller without gameplay effects.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.TownPortal
                && (definition.HealingAmount != 0
                    || hasDamage
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.Range <= 0
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.TownPortalSearchRadius <= 0
                    || definition.TownPortalSearchRadius > DeterministicGridCrawl.MaximumSupportedRadius
                    || definition.TownPortalLifetime <= 0
                    || definition.TownPortalDestinationPositionX < 0
                    || definition.TownPortalDestinationPositionY < 0
                    || definition.TownPortalDestinationSlotCount <= 0
                    || definition.TownPortalDestinationSlotSpacing < 0)) {
                throw new InvalidDataException(
                    "Town Portal behavior requires one targeted world transition without combat effects.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.TownPortal
                && (definition.TownPortalDestinationPositionX != AuthoritativePortalRules.TownPositionX
                    || definition.TownPortalDestinationPositionY != AuthoritativePortalRules.TownPositionY
                    || definition.TownPortalDestinationSlotCount != AuthoritativePortalRules.TownPortalSlotCount
                    || definition.TownPortalDestinationSlotSpacing != AuthoritativePortalRules.TownPortalSlotSpacing)) {
                throw new InvalidDataException(
                    "Town Portal destination tuning is only valid for Town Portal behavior.");
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
                    || definition.ResurrectionSearchRadius > DeterministicGridCrawl.MaximumSupportedRadius)) {
                throw new InvalidDataException(
                    "Resurrection behavior requires one stationary player-target effect with positive life and crawl tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Resurrection
                && definition.ResurrectionInitialOffsets.Count is not (0 or 8)) {
                throw new InvalidDataException(
                    "Resurrection initial placement requires exactly eight ordered probes when configured.");
            }
            if (definition.ResurrectionInitialOffsets.Any(offset =>
                    offset.X is < -DeterministicGridCrawl.MaximumSupportedRadius or > DeterministicGridCrawl.MaximumSupportedRadius
                    || offset.Y is < -DeterministicGridCrawl.MaximumSupportedRadius or > DeterministicGridCrawl.MaximumSupportedRadius)) {
                throw new InvalidDataException(
                    "Resurrection initial placement offsets exceed the supported crawl radius.");
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
            if (definition.ProjectileBehavior is (AuthoritativeProjectileBehavior.Magi
                or AuthoritativeProjectileBehavior.Mana)
                && (definition.HealingAmount != 0
                    || hasDamage
                    || definition.StatusEffectId != 0
                    || definition.Range != 1
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0)) {
                throw new InvalidDataException(
                    "Magi and Mana behaviors require one immediate self effect without combat or status effects.");
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
                        > DeterministicGridCrawl.MaximumSupportedRadius)) {
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
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Etherealize
                && (definition.HealingAmount != 0
                    || hasDamage
                    || definition.StatusEffectId == 0
                    || definition.StatusDuration != uint.MaxValue
                    || definition.StatusMagnitude != 0
                    || definition.Range != 1
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.StaffLevel != -1)) {
                throw new InvalidDataException(
                    "Etherealize behavior requires one level-scoped immediate self status.");
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
                    || definition.RandomMagicDamageDivisor != 0
                    || definition.ChargedBoltPathRandomRange != 0
                    || definition.ChargedBoltPathPattern.Count != 0
                    || definition.ChargedBoltHitMonsterLevelScale != 2
                    || definition.ChargedBoltMinimumHitChance != 5
                    || definition.ChargedBoltMaximumHitChance != 95)) {
                throw new InvalidDataException(
                    "Charged Bolt tuning columns require ChargedBolt projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Fireball
                && definition.FireballExplosionOffsets.Count != 0) {
                throw new InvalidDataException(
                    "Fireball explosion tuning requires Fireball projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Elemental
                && definition.ElementalBurstOffsets.Count != 0) {
                throw new InvalidDataException(
                    "Elemental burst tuning requires Elemental projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Flash
                && (definition.FlashBottomOffsets.Count != 0
                    || definition.FlashTopOffsets.Count != 0)) {
                throw new InvalidDataException(
                    "Flash layer tuning requires Flash projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.GuardianController
                && definition.GuardianArcOffsets.Count != 0) {
                throw new InvalidDataException(
                    "Guardian arc tuning requires Guardian projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.HolyBolt
                && (definition.HolyBoltHitMonsterLevelScale != 2
                    || definition.HolyBoltMinimumHitChance != 5
                    || definition.HolyBoltMaximumHitChance != 95)) {
                throw new InvalidDataException(
                    "Holy Bolt hit tuning columns require HolyBolt projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Firebolt
                && (definition.FireboltHitMonsterLevelScale != 2
                    || definition.FireboltMinimumHitChance != 5
                    || definition.FireboltMaximumHitChance != 95)) {
                throw new InvalidDataException(
                    "Firebolt hit tuning columns require Firebolt projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.BoneSpirit
                && (definition.BoneSpiritHitMonsterLevelScale != 2
                    || definition.BoneSpiritMinimumHitChance != 5
                    || definition.BoneSpiritMaximumHitChance != 95
                    || definition.BoneSpiritDamageHitPointsDivisor != 3
                    || definition.BoneSpiritPlayerDamageHitPointsDivisor != 3)) {
                throw new InvalidDataException(
                    "Bone Spirit hit tuning columns require BoneSpirit projectile behavior.");
            }
            if (definition.PersistentProjectileMaximumHitChance
                < definition.PersistentProjectileMinimumHitChance) {
                throw new InvalidDataException(
                    "Persistent projectile hit chance bounds must be ordered.");
            }
            if (definition.PersistentProjectilePlayerMaximumHitChance
                < definition.PersistentProjectilePlayerMinimumHitChance) {
                throw new InvalidDataException(
                    "Persistent projectile player-hit chance bounds must be ordered.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Firebolt
                    or AuthoritativeProjectileBehavior.HolyBolt
                    or AuthoritativeProjectileBehavior.Lightning
                    or AuthoritativeProjectileBehavior.Flash
                    or AuthoritativeProjectileBehavior.Fireball
                    or AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.FireWallSegment
                    or AuthoritativeProjectileBehavior.LightningWallController
                    or AuthoritativeProjectileBehavior.RingOfFireController
                    or AuthoritativeProjectileBehavior.ImmolationController
                    or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment
                    or AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment
                    or AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt
                    or AuthoritativeProjectileBehavior.NovaController
                    or AuthoritativeProjectileBehavior.NovaBall
                    or AuthoritativeProjectileBehavior.ApocalypseController
                    or AuthoritativeProjectileBehavior.ApocalypseBoom
                    or AuthoritativeProjectileBehavior.Elemental
                    or AuthoritativeProjectileBehavior.GuardianController)
                && (definition.RandomDamageRange != 0
                    || definition.DamageBonus != 0)) {
                throw new InvalidDataException(
                    "Native random damage columns require a supported projectile behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Firebolt
                    or AuthoritativeProjectileBehavior.BloodStar
                    or AuthoritativeProjectileBehavior.BoneSpirit
                    or AuthoritativeProjectileBehavior.Elemental
                    or AuthoritativeProjectileBehavior.GuardianController
                    or AuthoritativeProjectileBehavior.NovaController
                    or AuthoritativeProjectileBehavior.NovaBall
                    or AuthoritativeProjectileBehavior.ImmolationController)
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
                    or AuthoritativeProjectileBehavior.LightningWallController
                    or AuthoritativeProjectileBehavior.RingOfFireController
                     or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment
                    or AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment
                    or AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt
                    or AuthoritativeProjectileBehavior.NovaController
                    or AuthoritativeProjectileBehavior.NovaBall
                    or AuthoritativeProjectileBehavior.ImmolationController
                    or AuthoritativeProjectileBehavior.ApocalypseController
                    or AuthoritativeProjectileBehavior.ApocalypseBoom
                    or AuthoritativeProjectileBehavior.Elemental
                    or AuthoritativeProjectileBehavior.GuardianController)
                && (definition.RandomDamageIterationsCharacterLevelDivisor != 0
                    || definition.RandomDamageIterationsBonus != 0
                    || definition.SpellEffectScaleDivisor != 0)) {
                throw new InvalidDataException(
                    "Repeated random-damage scaling requires a supported projectile behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Flash
                    or AuthoritativeProjectileBehavior.Fireball
                    or AuthoritativeProjectileBehavior.HolyBolt
                    or AuthoritativeProjectileBehavior.NovaController
                    or AuthoritativeProjectileBehavior.NovaBall
                    or AuthoritativeProjectileBehavior.ImmolationController)
                && definition.CharacterLevelDamageDivisor != 0) {
                throw new InvalidDataException(
                    "Character-level damage scaling requires Flash, Fireball, HolyBolt, or Nova projectile behavior.");
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
                    or AuthoritativeProjectileBehavior.ChainLightningBolt
                    or AuthoritativeProjectileBehavior.NovaController
                    or AuthoritativeProjectileBehavior.NovaBall
                    or AuthoritativeProjectileBehavior.ImmolationController
                    or AuthoritativeProjectileBehavior.ApocalypseController
                    or AuthoritativeProjectileBehavior.ApocalypseBoom)
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
                    or AuthoritativeProjectileBehavior.LightningWallController
                    or AuthoritativeProjectileBehavior.RingOfFireController
                     or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment
                    or AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment
                    or AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt
                    or AuthoritativeProjectileBehavior.NovaController
                    or AuthoritativeProjectileBehavior.NovaBall
                    or AuthoritativeProjectileBehavior.ImmolationController
                    or AuthoritativeProjectileBehavior.ApocalypseController
                    or AuthoritativeProjectileBehavior.ApocalypseBoom)
                && definition.DamageScale != 1) {
                throw new InvalidDataException(
                    "Scaled native projectile damage requires Lightning, Fireball, Fire Wall, Flame Wave, Inferno, Chain Lightning, or Nova behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.Fireball
                    or AuthoritativeProjectileBehavior.HolyBolt
                    or AuthoritativeProjectileBehavior.ImmolationController)
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
                    or AuthoritativeProjectileBehavior.LightningWallController
                    or AuthoritativeProjectileBehavior.RingOfFireController
                     or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment
                    or AuthoritativeProjectileBehavior.InfernoController
                    or AuthoritativeProjectileBehavior.InfernoSegment
                    or AuthoritativeProjectileBehavior.ChainLightningController
                    or AuthoritativeProjectileBehavior.ChainLightningBolt
                    or AuthoritativeProjectileBehavior.NovaController
                    or AuthoritativeProjectileBehavior.NovaBall
                    or AuthoritativeProjectileBehavior.ImmolationController
                    or AuthoritativeProjectileBehavior.ApocalypseController
                    or AuthoritativeProjectileBehavior.ApocalypseBoom
                    or AuthoritativeProjectileBehavior.Elemental
                    or AuthoritativeProjectileBehavior.GuardianController)
                && (definition.ResistantDamageDivisor != 1
                    || definition.ProjectileImpactLifetime != 1)) {
                throw new InvalidDataException(
                    "Native projectile tuning requires a native projectile behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.FireWallController
                    or AuthoritativeProjectileBehavior.FireWallSegment
                    or AuthoritativeProjectileBehavior.LightningWallController
                    or AuthoritativeProjectileBehavior.RingOfFireController
                    or AuthoritativeProjectileBehavior.FlameWaveController
                    or AuthoritativeProjectileBehavior.FlameWaveSegment)
                && (definition.WallControlLifetime != 0
                    || definition.WallSegmentBaseDuration != 0
                    || definition.WallSegmentSpellLevelDurationStep != 0
                    || definition.WallSegmentCharacterLevelDurationStep != 0
                    || definition.WallSegmentDurationScale != 0
                    || definition.WallCharacterLevelDamageMultiplier != 0)) {
                throw new InvalidDataException(
                    "Fire Wall duration and character-damage tuning requires Fire Wall or Ring of Fire behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.RingOfFireController
                && definition.RingOfFireSearchRadius != 0) {
                throw new InvalidDataException(
                    "Ring of Fire crawl tuning requires RingOfFireController behavior.");
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
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.NovaController
                    or AuthoritativeProjectileBehavior.NovaBall)
                && (definition.NovaControllerLifetime != 0
                    || definition.NovaSearchRadius != 0
                    || definition.NovaProjectileCount != 0)) {
                throw new InvalidDataException(
                    "Nova tuning requires Nova behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.ImmolationController
                && (definition.ImmolationControllerLifetime != 0
                    || definition.ImmolationSearchRadius != 0
                    || definition.ImmolationProjectileCount != 0)) {
                throw new InvalidDataException(
                    "Immolation tuning requires Immolation behavior.");
            }
            if (definition.ProjectileBehavior is not (
                    AuthoritativeProjectileBehavior.ApocalypseController
                    or AuthoritativeProjectileBehavior.ApocalypseBoom)
                && (definition.ApocalypseControllerLifetime != 0
                    || definition.ApocalypseSearchRadius != 0)) {
                throw new InvalidDataException(
                    "Apocalypse tuning requires Apocalypse behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.RageController
                && (definition.RageControllerLifetime != 0
                    || definition.RageCharacterLevelLifetimeScale != 0
                    || definition.RageSelfDamageCharacterLevel != 0
                    || definition.RageMinimumLifeQ6PerCharacterLevel != 0)) {
                throw new InvalidDataException(
                    "Rage tuning requires Rage behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Reflect
                && (definition.ReflectDefaultSpellLevel != 0
                    || definition.ReflectDamagePercentMinimum != 0
                    || definition.ReflectDamagePercentMaximum != 0)) {
                throw new InvalidDataException(
                    "Reflect tuning requires Reflect behavior.");
            }
            if ((definition.ReflectDamagePercentMinimum != 0
                    || definition.ReflectDamagePercentMaximum != 0)
                && (definition.ReflectDamagePercentMinimum <= 0
                    || definition.ReflectDamagePercentMaximum < definition.ReflectDamagePercentMinimum)) {
                throw new InvalidDataException(
                    "Reflect damage percentage bounds are invalid.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Elemental
                && (definition.ElementalRetargetLifetime != 0
                    || definition.ElementalCharacterLevelDamageMultiplier != 0)) {
                throw new InvalidDataException(
                    "Elemental tuning requires Elemental behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.GuardianController
                && (definition.GuardianDurationTickScale != 0
                    || definition.GuardianMinimumDuration != 0
                    || definition.GuardianMaximumDuration != 0
                    || definition.GuardianCharacterLevelDurationDivisor != 0
                    || definition.GuardianSpawnSearchRadius != 0
                    || definition.GuardianTargetSearchRadius != 0
                    || definition.GuardianFireInterval != 0
                    || definition.GuardianProjectileSpeedLevelStep != 0
                    || definition.GuardianProjectileSpeedMaximumBonus != 0)) {
                throw new InvalidDataException(
                    "Guardian tuning requires Guardian behavior.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Berserk
                && (definition.HealingAmount != 0
                    || definition.DamageAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.Range <= 0
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 1
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.BerserkSearchRadius <= 0
                    || definition.BerserkDamagePercentBase != 120
                    || definition.BerserkDamagePercentRandomRange != 10
                    || definition.BerserkSpellLevelBonus != 1)) {
                throw new InvalidDataException(
                    "Berserk behavior requires the native monster-target mutation tuning.");
            }
            if (definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Warp
                && (definition.HealingAmount != 0
                    || definition.DamageAmount != 0
                    || definition.StatusEffectId != 0
                    || definition.StatusDuration != 0
                    || definition.StatusMagnitude != 0
                    || definition.Range <= 0
                    || definition.AreaRadius != 0
                    || definition.DamageType != AuthoritativeDamageType.Magic
                    || definition.ProjectileSpeed != 0
                    || definition.ProjectileLifetime != 2
                    || definition.ProjectileCount != 1
                    || definition.ProjectileCountLevelDivisor != 0
                    || definition.WarpSearchRadius <= 0
                    || definition.WarpSearchRadius > DeterministicGridCrawl.MaximumSupportedRadius)) {
                throw new InvalidDataException(
                    "Warp behavior requires the native two-tick level transition tuning.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Berserk
                && (definition.BerserkSearchRadius != 0
                    || definition.BerserkDamagePercentBase != 0
                    || definition.BerserkDamagePercentRandomRange != 0
                    || definition.BerserkSpellLevelBonus != 0
                    || definition.BerserkResistanceFrequency != 0)) {
                throw new InvalidDataException(
                    "Berserk tuning requires Berserk behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Resurrection
                && (definition.ResurrectionLife != 0
                    || definition.ResurrectionLifeScale != 1
                    || definition.ResurrectionSearchRadius != 0
                    || definition.ResurrectionInitialOffsets.Count != 0)) {
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
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Golem
                && definition.GolemDeathAnimationTicks != 0) {
                throw new InvalidDataException(
                    "Golem death-animation tuning requires Golem projectile behavior.");
            }
            if (definition.ProjectileBehavior != AuthoritativeProjectileBehavior.Golem
                && definition.GolemMonsterId != 0) {
                throw new InvalidDataException(
                    "Golem summon identity requires Golem projectile behavior.");
            }
            if (definition.StatusEffectId != 0 && definition.StatusDuration == 0)
                throw new InvalidDataException("Status-bearing spells require a positive status duration.");
            if (!spells.TryAdd(definition.SpellId, definition))
                throw new InvalidDataException($"Spell {definition.SpellId} is defined more than once.");
            if (definition.NativeSpellId != 0
                && !nativeSpells.TryAdd(definition.NativeSpellId, definition)) {
                throw new InvalidDataException(
                    $"Native spell ID {definition.NativeSpellId} is defined more than once.");
            }
            if (definition.ConsumableMiscId != 0
                && !consumableSpells.TryAdd(definition.ConsumableMiscId, definition)) {
                throw new InvalidDataException(
                    $"Consumable miscellaneous ID {definition.ConsumableMiscId} is defined more than once.");
            }
        }
        if (spells.Count == 0)
            throw new InvalidDataException("The spell catalog cannot be empty.");
        foreach (var jester in spells.Values.Where(definition =>
                     definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Jester)) {
            if (jester.JesterSpellIds.Any(spellId => !spells.ContainsKey(spellId))) {
                throw new InvalidDataException(
                    $"Spell {jester.SpellId} references an unknown Jester dispatch spell.");
            }
        }
        var visitingJesterIds = new HashSet<uint>();
        var visitedJesterIds = new HashSet<uint>();
        foreach (var jester in spells.Values.Where(definition =>
                     definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Jester)) {
            if (HasJesterDispatchCycle(jester.SpellId)) {
                throw new InvalidDataException(
                    $"Jester dispatch graph contains a cycle at spell {jester.SpellId}.");
            }
        }

        bool HasJesterDispatchCycle(uint spellId)
        {
            if (visitedJesterIds.Contains(spellId))
                return false;
            if (!visitingJesterIds.Add(spellId))
                return true;

            foreach (var dispatchId in spells[spellId].JesterSpellIds) {
                if (spells[dispatchId].ProjectileBehavior == AuthoritativeProjectileBehavior.Jester
                    && HasJesterDispatchCycle(dispatchId)) {
                    return true;
                }
            }

            visitingJesterIds.Remove(spellId);
            visitedJesterIds.Add(spellId);
            return false;
        }
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
                definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Etherealize) > 1) {
                throw new InvalidDataException(
                    "The spell catalog can define at most one Etherealize behavior.");
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

    /** Translates the legacy Rune of Fire misc ID at the data boundary. */
    private static AuthoritativeSpellDefinition NormalizeLegacyConsumableBehavior(
        AuthoritativeSpellDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.ConsumableBehavior is null
            && definition.ConsumableMiscId == LegacyRuneFireConsumableMiscId
            ? definition with { ConsumableBehavior = AuthoritativeConsumableBehavior.RuneFire }
            : definition;
    }

    public IReadOnlyList<AuthoritativeSpellDefinition> Definitions => spells.Values.OrderBy(spell => spell.SpellId).ToArray();

    public bool TryGet(uint spellId, out AuthoritativeSpellDefinition definition) => spells.TryGetValue(spellId, out definition!);

    /**
     * Requires production content to supply every native ordering consumed by
     * the authoritative projectile handlers. Direct compatibility callers may
     * continue to use the embedded native defaults.
     */
    public void ValidateExternalizedNativePatterns()
    {
        foreach (var definition in Definitions) {
            switch (definition.ProjectileBehavior) {
                case AuthoritativeProjectileBehavior.ChargedBolt:
                    RequirePattern(definition, definition.ChargedBoltPathPattern.Count, 16, "charged_bolt_path_pattern");
                    break;
                case AuthoritativeProjectileBehavior.Fireball:
                    RequirePattern(definition, definition.FireballExplosionOffsets.Count, 9, "fireball_explosion_offsets");
                    break;
                case AuthoritativeProjectileBehavior.Elemental:
                    RequirePattern(definition, definition.ElementalBurstOffsets.Count, 9, "elemental_burst_offsets");
                    break;
                case AuthoritativeProjectileBehavior.Flash:
                    RequirePattern(definition, definition.FlashBottomOffsets.Count, 6, "flash_bottom_offsets");
                    RequirePattern(definition, definition.FlashTopOffsets.Count, 3, "flash_top_offsets");
                    break;
                case AuthoritativeProjectileBehavior.GuardianController:
                    RequirePattern(definition, definition.GuardianArcOffsets.Count, 48, "guardian_arc_offsets");
                    break;
                case AuthoritativeProjectileBehavior.Resurrection:
                    RequirePattern(definition, definition.ResurrectionInitialOffsets.Count, 8, "resurrection_initial_offsets");
                    break;
            }
        }
    }

    private static void RequirePattern(
        AuthoritativeSpellDefinition definition,
        int actualCount,
        int expectedCount,
        string column)
    {
        if (actualCount != expectedCount)
            throw new InvalidDataException(
                $"Spell {definition.SpellId} ({definition.DisplayName}) requires {expectedCount} entries in {column} for production activation.");
    }

    /** Validates stable spell and class-ability symbols against active catalogs. */
    public void ValidateStableReferences(
        StableContentIdCatalog stableIds,
        IEnumerable<uint>? additionalAbilityIds = null)
    {
        ArgumentNullException.ThrowIfNull(stableIds);
        var activeAbilityIds = additionalAbilityIds?.ToHashSet() ?? [];
        foreach (var entry in stableIds.Entries) {
            if (string.Equals(entry.Kind, "ability", StringComparison.Ordinal)) {
                // Search, Rage, and Identify are dual-role native identities:
                // partial class-loadout overlays may omit their capability row
                // while the corresponding active spell remains available.
                if (!activeAbilityIds.Contains(entry.NumericId)
                    && !TryGet(entry.NumericId, out _))
                    throw new InvalidDataException(
                        $"Stable ability symbol '{entry.Symbol}' references unknown ability {entry.NumericId}.");
                continue;
            }
            if (string.Equals(entry.Kind, "spell", StringComparison.Ordinal)
                && !TryGet(entry.NumericId, out _)
                && !activeAbilityIds.Contains(entry.NumericId)) {
                throw new InvalidDataException(
                    $"Stable spell symbol '{entry.Symbol}' references unknown spell {entry.NumericId}.");
            }
        }
    }

    /** Validates content-owned monster identities used by summon spell rows. */
    public void ValidateMonsterReferences(AuthoritativeMonsterTypeCatalog monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(monsterTypes);
        foreach (var definition in Definitions.Where(definition =>
                     definition.ProjectileBehavior == AuthoritativeProjectileBehavior.Golem
                     && definition.GolemMonsterId != 0)) {
            if (!monsterTypes.TryGet(definition.GolemMonsterId, out _))
                throw new InvalidDataException(
                    $"Golem spell {definition.SpellId} references unknown monster {definition.GolemMonsterId}.");
        }
    }

    /** Resolves a native source identity to its stable authoritative spell row. */
    public bool TryGetByNativeId(uint nativeSpellId, out AuthoritativeSpellDefinition definition)
        => nativeSpells.TryGetValue(nativeSpellId, out definition!);

    /** Resolves a native rune miscellaneous ID to its stable authoritative spell row. */
    public bool TryGetByConsumableMiscId(int miscId, out AuthoritativeSpellDefinition definition)
        => consumableSpells.TryGetValue(miscId, out definition!);

    public static AuthoritativeSpellCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeSpellCatalog(table.Rows.Select(row => new AuthoritativeSpellDefinition(
            ResolveSpellId(row, stableIds),
            row.RequiredInt32("mana_cost"),
            row.OptionalInt32("healing_amount"),
            row.OptionalUInt32("status_effect_id"),
            row.OptionalUInt32("status_duration"),
            row.OptionalInt32("status_magnitude")) {
            NativeSpellId = row.OptionalUInt32("native_spell_id"),
            DisplayName = row.TryGet("display_name", out var displayName)
                ? displayName
                : string.Empty,
            ConsumableMiscId = row.OptionalInt32("consumable_misc_id"),
            ConsumableBehavior = ParseConsumableBehavior(row),
            DamageAmount = row.OptionalInt32("damage_amount"),
            ManaAdjustment = row.OptionalInt32("mana_adjustment"),
            ManaAdjustmentDivisor = row.OptionalInt32("mana_adjustment_divisor", 1),
            CharacterLevelManaCost = row.OptionalInt32("character_level_mana_cost"),
            MinimumManaCost = row.OptionalInt32("minimum_mana_cost"),
            ManaCostScale = row.OptionalInt32("mana_cost_scale", 1),
            ManaRandomRange = row.OptionalInt32("mana_random_range"),
            ManaCharacterLevelRandomRange = row.OptionalInt32(
                "mana_character_level_random_range"),
            ManaSpellLevelRandomRange = row.OptionalInt32(
                "mana_spell_level_random_range"),
            Range = row.OptionalInt32("range", 1),
            AreaRadius = row.OptionalInt32("area_radius"),
            DamageType = ParseDamageType(row),
            ProjectileSpeed = row.OptionalInt32("projectile_speed"),
            ProjectileLifetime = row.OptionalInt32("projectile_lifetime", 32),
            ProjectileCount = row.OptionalInt32("projectile_count", 1),
            ProjectileCountLevelDivisor = row.OptionalInt32("projectile_count_level_divisor"),
            ProjectileBehavior = ParseProjectileBehavior(row),
            JesterSpellIds = ParseJesterSpellIds(row, stableIds),
            RandomMagicDamageDivisor = row.OptionalInt32("random_magic_damage_divisor"),
            RandomDamageRange = row.OptionalInt32("random_damage_range"),
            MagicDamageDivisor = row.OptionalInt32("magic_damage_divisor"),
            ChargedBoltPathRandomRange = row.OptionalInt32(
                "charged_bolt_path_random_range"),
            ChargedBoltPathPattern = ParseChargedBoltPathPattern(row),
            FireballExplosionOffsets = ParseFireballExplosionOffsets(row),
            ElementalBurstOffsets = ParseElementalBurstOffsets(row),
            FlashBottomOffsets = ParseFlashLayerOffsets(row, "flash_bottom_offsets", "Flash bottom-layer"),
            FlashTopOffsets = ParseFlashLayerOffsets(row, "flash_top_offsets", "Flash top-layer"),
            GuardianArcOffsets = ParseGuardianArcOffsets(row),
            ChargedBoltHitMonsterLevelScale = row.OptionalInt32(
                "charged_bolt_hit_monster_level_scale",
                2),
            ChargedBoltMinimumHitChance = row.OptionalInt32(
                "charged_bolt_minimum_hit_chance",
                5),
            ChargedBoltMaximumHitChance = row.OptionalInt32(
                "charged_bolt_maximum_hit_chance",
                95),
            HolyBoltHitMonsterLevelScale = row.OptionalInt32(
                "holy_bolt_hit_monster_level_scale",
                2),
            HolyBoltMinimumHitChance = row.OptionalInt32(
                "holy_bolt_minimum_hit_chance",
                5),
            HolyBoltMaximumHitChance = row.OptionalInt32(
                "holy_bolt_maximum_hit_chance",
                95),
            FireboltHitMonsterLevelScale = row.OptionalInt32(
                "firebolt_hit_monster_level_scale",
                2),
            FireboltMinimumHitChance = row.OptionalInt32(
                "firebolt_minimum_hit_chance",
                5),
            FireboltMaximumHitChance = row.OptionalInt32(
                "firebolt_maximum_hit_chance",
                95),
            BoneSpiritHitMonsterLevelScale = row.OptionalInt32(
                "bone_spirit_hit_monster_level_scale",
                2),
            BoneSpiritMinimumHitChance = row.OptionalInt32(
                "bone_spirit_minimum_hit_chance",
                5),
            BoneSpiritMaximumHitChance = row.OptionalInt32(
                "bone_spirit_maximum_hit_chance",
                95),
            BoneSpiritDamageHitPointsDivisor = row.OptionalInt32(
                "bone_spirit_damage_hit_points_divisor",
                3),
            BoneSpiritPlayerDamageHitPointsDivisor = row.OptionalInt32(
                "bone_spirit_player_damage_hit_points_divisor",
                3),
            PersistentProjectileHitMonsterLevelScale = row.OptionalInt32(
                "persistent_projectile_hit_monster_level_scale",
                2),
            PersistentProjectileMinimumHitChance = row.OptionalInt32(
                "persistent_projectile_minimum_hit_chance",
                5),
            PersistentProjectileMaximumHitChance = row.OptionalInt32(
                "persistent_projectile_maximum_hit_chance",
                95),
            PersistentProjectileHitCharacterLevelScale = row.OptionalInt32(
                "persistent_projectile_hit_character_level_scale",
                2),
            PersistentProjectilePlayerMinimumHitChance = row.OptionalInt32(
                "persistent_projectile_player_minimum_hit_chance",
                5),
            PersistentProjectilePlayerMaximumHitChance = row.OptionalInt32(
                "persistent_projectile_player_maximum_hit_chance",
                95),
            PersistentProjectilePlayerDamageDivisor = row.OptionalInt32(
                "persistent_projectile_player_damage_divisor",
                2),
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
            FlashMonsterDamageLevelScale = row.OptionalInt32(
                "flash_monster_damage_level_scale",
                2),
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
            RingOfFireSearchRadius = row.OptionalInt32("ring_of_fire_search_radius"),
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
            NovaControllerLifetime = row.OptionalInt32("nova_controller_lifetime"),
            NovaSearchRadius = row.OptionalInt32("nova_search_radius"),
            NovaProjectileCount = row.OptionalInt32("nova_projectile_count"),
            ImmolationControllerLifetime = row.OptionalInt32("immolation_controller_lifetime"),
            ImmolationSearchRadius = row.OptionalInt32("immolation_search_radius"),
            ImmolationProjectileCount = row.OptionalInt32("immolation_projectile_count"),
            ApocalypseControllerLifetime = row.OptionalInt32("apocalypse_controller_lifetime"),
            ApocalypseSearchRadius = row.OptionalInt32("apocalypse_search_radius"),
            RageControllerLifetime = row.OptionalInt32("rage_controller_lifetime"),
            RageCharacterLevelLifetimeScale = row.OptionalInt32(
                "rage_character_level_lifetime_scale"),
            RageSelfDamageCharacterLevel = row.OptionalInt32(
                "rage_self_damage_character_level"),
            RageMinimumLifeQ6PerCharacterLevel = row.OptionalInt32(
                "rage_minimum_life_q6_per_character_level"),
            ReflectDefaultSpellLevel = row.OptionalInt32(
                "reflect_default_spell_level"),
            ReflectDamagePercentMinimum = row.OptionalInt32(
                "reflect_damage_percent_minimum"),
            ReflectDamagePercentMaximum = row.OptionalInt32(
                "reflect_damage_percent_maximum"),
            ElementalRetargetLifetime = row.OptionalInt32(
                "elemental_retarget_lifetime"),
            ElementalCharacterLevelDamageMultiplier = row.OptionalInt32(
                "elemental_character_level_damage_multiplier"),
            GuardianDurationTickScale = row.OptionalInt32("guardian_duration_tick_scale"),
            GuardianMinimumDuration = row.OptionalInt32("guardian_minimum_duration"),
            GuardianMaximumDuration = row.OptionalInt32("guardian_maximum_duration"),
            GuardianCharacterLevelDurationDivisor = row.OptionalInt32(
                "guardian_character_level_duration_divisor"),
            GuardianSpawnSearchRadius = row.OptionalInt32("guardian_spawn_search_radius"),
            GuardianTargetSearchRadius = row.OptionalInt32("guardian_target_search_radius"),
            GuardianFireInterval = row.OptionalInt32("guardian_fire_interval"),
            GuardianProjectileSpeedLevelStep = row.OptionalInt32(
                "guardian_projectile_speed_level_step"),
            GuardianProjectileSpeedMaximumBonus = row.OptionalInt32(
                "guardian_projectile_speed_maximum_bonus"),
            BerserkSearchRadius = row.OptionalInt32("berserk_search_radius"),
            BerserkDamagePercentBase = row.OptionalInt32("berserk_damage_percent_base"),
            BerserkDamagePercentRandomRange = row.OptionalInt32("berserk_damage_percent_random_range"),
            BerserkSpellLevelBonus = row.OptionalInt32("berserk_spell_level_bonus"),
            BerserkResistanceFrequency = row.OptionalInt32(
                "berserk_resistance_frequency"),
            GolemDeathAnimationTicks = row.OptionalInt32("golem_death_animation_ticks"),
            GolemMonsterId = AuthoritativeMonsterIdentity.ResolveOptional(
                row,
                stableIds,
                "golem_monster_id",
                "golem_monster_symbol"),
            GolemHitPointsLevelBaseQ6 = row.OptionalInt32(
                "golem_hit_points_level_base_q6", 320),
            GolemHitPointsLevelMultiplier = row.OptionalInt32(
                "golem_hit_points_level_multiplier", 2),
            GolemHitPointsManaDivisor = row.OptionalInt32(
                "golem_hit_points_mana_divisor", 3),
            GolemDamageLevelMultiplier = row.OptionalInt32(
                "golem_damage_level_multiplier", 2),
            GolemMinimumDamageLevelOffset = row.OptionalInt32(
                "golem_minimum_damage_level_offset", 4),
            GolemMaximumDamageLevelOffset = row.OptionalInt32(
                "golem_maximum_damage_level_offset", 8),
            GolemArmorClass = row.OptionalInt32("golem_armor_class", 25),
            ResistantDamageDivisor = row.OptionalInt32("resistant_damage_divisor", 1),
            ProjectileImpactLifetime = row.OptionalInt32("projectile_impact_lifetime", 1),
            ResurrectionLife = row.OptionalInt32("resurrection_life"),
            ResurrectionLifeScale = row.OptionalInt32("resurrection_life_scale", 1),
            ResurrectionSearchRadius = row.OptionalInt32("resurrection_search_radius"),
            ResurrectionInitialOffsets = ParseResurrectionInitialOffsets(row),
            TeleportSearchRadius = row.OptionalInt32("teleport_search_radius", 5),
            WarpSearchRadius = row.OptionalInt32("warp_search_radius", 5),
            TownPortalSearchRadius = row.OptionalInt32("town_portal_search_radius", 5),
            TownPortalLifetime = row.OptionalInt32("town_portal_lifetime", 100),
            TownPortalDestinationPositionX = row.OptionalInt32(
                "town_portal_destination_position_x",
                AuthoritativePortalRules.TownPositionX),
            TownPortalDestinationPositionY = row.OptionalInt32(
                "town_portal_destination_position_y",
                AuthoritativePortalRules.TownPositionY),
            TownPortalDestinationSlotCount = row.OptionalInt32(
                "town_portal_destination_slot_count",
                AuthoritativePortalRules.TownPortalSlotCount),
            TownPortalDestinationSlotSpacing = row.OptionalInt32(
                "town_portal_destination_slot_spacing",
                AuthoritativePortalRules.TownPortalSlotSpacing),
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

    private static uint ResolveSpellId(TsvRow row, StableContentIdCatalog? stableIds)
    {
        var numericId = row.RequiredUInt32("spell_id");
        if (!row.TryGet("spell_symbol", out var symbol)
            || string.IsNullOrWhiteSpace(symbol))
            return numericId;
        if (stableIds is null) {
            if (numericId == 0)
                throw new InvalidDataException(
                    $"Spell row {row.LineNumber} requires stable spell IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("spell", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Spell row {row.LineNumber} references unknown stable spell symbol '{symbol}'.",
                exception);
        }
        if (numericId != 0 && numericId != resolvedId)
            throw new InvalidDataException(
                $"Spell row {row.LineNumber} maps spell {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }

    private static AuthoritativeDamageType ParseDamageType(TsvRow row)
    {
        if (!row.TryGet("damage_type", out var value) || string.IsNullOrWhiteSpace(value))
            return AuthoritativeDamageType.Physical;
        if (Enum.TryParse<AuthoritativeDamageType>(value, ignoreCase: true, out var damageType))
            return damageType;
        throw new InvalidDataException($"Spell row {row.LineNumber} contains an unknown damage type '{value}'.");
    }

    private static IReadOnlyList<uint> ParseJesterSpellIds(
        TsvRow row,
        StableContentIdCatalog? stableIds)
    {
        var numericIds = ParseJesterSpellIds(row);
        if (!row.TryGet("jester_spell_symbols", out var value)
            || string.IsNullOrWhiteSpace(value)
            || value == "0")
            return numericIds;

        var entries = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var symbolicIds = new List<uint>(entries.Length);
        foreach (var entry in entries) {
            if (stableIds is null) {
                if (numericIds.Count == 0)
                    throw new InvalidDataException(
                        $"Spell row {row.LineNumber} requires stable spell IDs to resolve Jester symbol '{entry}'.");
                return numericIds;
            }

            try {
                symbolicIds.Add(stableIds.Resolve("spell", entry));
            } catch (KeyNotFoundException exception) {
                throw new InvalidDataException(
                    $"Spell row {row.LineNumber} references unknown Jester spell symbol '{entry}'.",
                    exception);
            }
        }

        if (numericIds.Count != 0 && numericIds.Count != symbolicIds.Count)
            throw new InvalidDataException(
                $"Spell row {row.LineNumber} has {numericIds.Count} Jester spell IDs but "
                + $"{symbolicIds.Count} Jester spell symbols.");
        if (numericIds.Count != 0
            && numericIds.Zip(symbolicIds).Any(pair => pair.First != pair.Second))
            throw new InvalidDataException(
                $"Spell row {row.LineNumber} contains mismatched Jester spell IDs and symbols.");
        return symbolicIds;
    }

    private static IReadOnlyList<uint> ParseJesterSpellIds(TsvRow row)
    {
        if (!row.TryGet("jester_spell_ids", out var value)
            || string.IsNullOrWhiteSpace(value)
            || value == "0")
            return [];
        var entries = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var spellIds = new List<uint>(entries.Length);
        foreach (var entry in entries) {
            if (!uint.TryParse(entry, out var spellId) || spellId == 0)
                throw new InvalidDataException(
                    $"Spell row {row.LineNumber} contains an invalid Jester spell ID '{entry}'.");
            spellIds.Add(spellId);
        }
        return spellIds;
    }

    private static IReadOnlyList<(int X, int Y)> ParseResurrectionInitialOffsets(TsvRow row)
    {
        if (!row.TryGet("resurrection_initial_offsets", out var value)
            || string.IsNullOrWhiteSpace(value))
            return [];

        var entries = value.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var offsets = new (int X, int Y)[entries.Length];
        for (var index = 0; index < entries.Length; index++) {
            var components = entries[index].Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (components.Length != 2
                || !int.TryParse(components[0], out var x)
                || !int.TryParse(components[1], out var y)) {
                throw new InvalidDataException(
                    $"Spell row {row.LineNumber} contains an invalid Resurrection initial placement offset.");
            }
            offsets[index] = (x, y);
        }
        return offsets;
    }

    private static IReadOnlyList<int> ParseChargedBoltPathPattern(TsvRow row)
    {
        if (!row.TryGet("charged_bolt_path_pattern", out var value)
            || string.IsNullOrWhiteSpace(value))
            return [];

        var entries = value.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pattern = new int[entries.Length];
        for (var index = 0; index < entries.Length; index++) {
            if (!int.TryParse(entries[index], out pattern[index]))
                throw new InvalidDataException(
                    $"Spell row {row.LineNumber} contains an invalid Charged Bolt path steering entry.");
        }
        return pattern;
    }

    private static IReadOnlyList<(int X, int Y)> ParseFireballExplosionOffsets(TsvRow row)
    {
        if (!row.TryGet("fireball_explosion_offsets", out var value)
            || string.IsNullOrWhiteSpace(value))
            return [];

        var entries = value.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var offsets = new (int X, int Y)[entries.Length];
        for (var index = 0; index < entries.Length; index++) {
            var components = entries[index].Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (components.Length != 2
                || !int.TryParse(components[0], out var x)
                || !int.TryParse(components[1], out var y)) {
                throw new InvalidDataException(
                    $"Spell row {row.LineNumber} contains an invalid Fireball explosion offset.");
            }
            offsets[index] = (x, y);
        }
        return offsets;
    }

    private static IReadOnlyList<(int X, int Y)> ParseElementalBurstOffsets(TsvRow row)
    {
        if (!row.TryGet("elemental_burst_offsets", out var value)
            || string.IsNullOrWhiteSpace(value))
            return [];

        var entries = value.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var offsets = new (int X, int Y)[entries.Length];
        for (var index = 0; index < entries.Length; index++) {
            var components = entries[index].Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (components.Length != 2
                || !int.TryParse(components[0], out var x)
                || !int.TryParse(components[1], out var y)) {
                throw new InvalidDataException(
                    $"Spell row {row.LineNumber} contains an invalid Elemental burst offset.");
            }
            offsets[index] = (x, y);
        }
        return offsets;
    }

    private static IReadOnlyList<(int X, int Y)> ParseFlashLayerOffsets(
        TsvRow row,
        string columnName,
        string description)
    {
        if (!row.TryGet(columnName, out var value)
            || string.IsNullOrWhiteSpace(value))
            return [];

        var entries = value.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var offsets = new (int X, int Y)[entries.Length];
        for (var index = 0; index < entries.Length; index++) {
            var components = entries[index].Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (components.Length != 2
                || !int.TryParse(components[0], out var x)
                || !int.TryParse(components[1], out var y)) {
                throw new InvalidDataException(
                    $"Spell row {row.LineNumber} contains an invalid {description} offset.");
            }
            offsets[index] = (x, y);
        }
        return offsets;
    }

    private static IReadOnlyList<(int X, int Y)> ParseGuardianArcOffsets(TsvRow row)
    {
        if (!row.TryGet("guardian_arc_offsets", out var value)
            || string.IsNullOrWhiteSpace(value))
            return [];

        var entries = value.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var offsets = new (int X, int Y)[entries.Length];
        for (var index = 0; index < entries.Length; index++) {
            var components = entries[index].Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (components.Length != 2
                || !int.TryParse(components[0], out var x)
                || !int.TryParse(components[1], out var y)) {
                throw new InvalidDataException(
                    $"Spell row {row.LineNumber} contains an invalid Guardian arc offset.");
            }
            offsets[index] = (x, y);
        }
        return offsets;
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

    private static AuthoritativeConsumableBehavior? ParseConsumableBehavior(TsvRow row)
    {
        if (!row.TryGet("consumable_behavior", out var value))
            return null;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException(
                $"Column 'consumable_behavior' is empty on TSV row {row.LineNumber}.");
        if (Enum.TryParse<AuthoritativeConsumableBehavior>(
                value,
                ignoreCase: true,
                out var behavior)
            && Enum.IsDefined(behavior))
            return behavior;
        throw new InvalidDataException(
            $"Spell row {row.LineNumber} contains an unknown consumable behavior '{value}'.");
    }
}
