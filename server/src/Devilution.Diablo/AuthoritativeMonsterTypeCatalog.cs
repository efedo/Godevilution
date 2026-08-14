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

    /** Native SkeletonMelee movement-delay chance base before intelligence scaling. */
    public int SkeletonMeleeDelayChanceBase { get; init; }

    /** Native SkeletonMelee movement-delay chance reduction per intelligence point. */
    public int SkeletonMeleeDelayChanceIntelligenceScale { get; init; }

    /** Native GoatMelee movement admission chance base before intelligence scaling. */
    public int GoatMeleeMovementChanceBase { get; init; }

    /** Native GoatMelee movement admission chance increase per intelligence point. */
    public int GoatMeleeMovementChanceIntelligenceScale { get; init; }

    /** Native Fat movement admission chance base before intelligence scaling. */
    public int FatMovementChanceBase { get; init; }

    /** Native Fat movement admission chance increase per intelligence point. */
    public int FatMovementChanceIntelligenceScale { get; init; }

    /** Native Zombie action admission chance base before intelligence scaling. */
    public int ZombieActionChanceBase { get; init; }

    /** Native Zombie action admission chance increase per intelligence point. */
    public int ZombieActionChanceIntelligenceScale { get; init; }

    /** Native minimum Zombie long-range movement distance. */
    public int ZombieMovementDistanceMinimum { get; init; }

    /** Native Zombie long-range movement distance base before intelligence scaling. */
    public int ZombieMovementDistanceBase { get; init; }

    /** Native Zombie long-range movement distance increase per intelligence point. */
    public int ZombieMovementDistanceIntelligenceScale { get; init; }
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
                || (definition.NativeAi == "Bat"
                    && ((definition.BatAttackChanceBase != 0
                            || definition.BatAttackChanceIntelligenceScale != 0)
                        && (definition.BatAttackChanceBase <= 0
                            || definition.BatAttackChanceIntelligenceScale <= 0)))
                || (definition.NativeAi != "Bat"
                    && (definition.BatAttackChanceBase != 0
                        || definition.BatAttackChanceIntelligenceScale != 0))
                || definition.SkeletonMeleeDelayChanceBase is < 0 or > 100
                || definition.SkeletonMeleeDelayChanceIntelligenceScale is < 0 or > 100
                || (definition.NativeAi == "SkeletonMelee"
                    && ((definition.SkeletonMeleeDelayChanceBase != 0
                            || definition.SkeletonMeleeDelayChanceIntelligenceScale != 0)
                        && (definition.SkeletonMeleeDelayChanceBase <= 0
                            || definition.SkeletonMeleeDelayChanceIntelligenceScale <= 0)))
                || (definition.NativeAi != "SkeletonMelee"
                    && (definition.SkeletonMeleeDelayChanceBase != 0
                        || definition.SkeletonMeleeDelayChanceIntelligenceScale != 0))
                || definition.GoatMeleeMovementChanceBase is < 0 or > 100
                || definition.GoatMeleeMovementChanceIntelligenceScale is < 0 or > 100
                || (definition.NativeAi == "GoatMelee"
                    && ((definition.GoatMeleeMovementChanceBase != 0
                            || definition.GoatMeleeMovementChanceIntelligenceScale != 0)
                        && (definition.GoatMeleeMovementChanceBase <= 0
                            || definition.GoatMeleeMovementChanceIntelligenceScale <= 0)))
                || (definition.NativeAi != "GoatMelee"
                    && (definition.GoatMeleeMovementChanceBase != 0
                        || definition.GoatMeleeMovementChanceIntelligenceScale != 0))
                || definition.FatMovementChanceBase is < 0 or > 100
                || definition.FatMovementChanceIntelligenceScale is < 0 or > 100
                || (definition.NativeAi == "Fat"
                    && ((definition.FatMovementChanceBase != 0
                            || definition.FatMovementChanceIntelligenceScale != 0)
                        && (definition.FatMovementChanceBase <= 0
                            || definition.FatMovementChanceIntelligenceScale <= 0)))
                || (definition.NativeAi != "Fat"
                    && (definition.FatMovementChanceBase != 0
                        || definition.FatMovementChanceIntelligenceScale != 0))
                || definition.ZombieActionChanceBase is < 0 or > 100
                || definition.ZombieActionChanceIntelligenceScale is < 0 or > 100
                || (definition.NativeAi == "Zombie"
                    && ((definition.ZombieActionChanceBase != 0
                            || definition.ZombieActionChanceIntelligenceScale != 0)
                        && (definition.ZombieActionChanceBase <= 0
                            || definition.ZombieActionChanceIntelligenceScale <= 0)))
                || (definition.NativeAi != "Zombie"
                    && (definition.ZombieActionChanceBase != 0
                        || definition.ZombieActionChanceIntelligenceScale != 0))
                || definition.ZombieMovementDistanceMinimum is < 0 or > 64
                || definition.ZombieMovementDistanceBase is < 0 or > 64
                || definition.ZombieMovementDistanceIntelligenceScale is < 0 or > 64
                || (definition.NativeAi == "Zombie"
                    && ((definition.ZombieMovementDistanceMinimum != 0
                            || definition.ZombieMovementDistanceBase != 0
                            || definition.ZombieMovementDistanceIntelligenceScale != 0)
                        && (definition.ZombieMovementDistanceMinimum <= 0
                            || definition.ZombieMovementDistanceBase <= 0
                            || definition.ZombieMovementDistanceIntelligenceScale <= 0)))
                || (definition.NativeAi != "Zombie"
                    && (definition.ZombieMovementDistanceMinimum != 0
                        || definition.ZombieMovementDistanceBase != 0
                        || definition.ZombieMovementDistanceIntelligenceScale != 0))
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
                SkeletonMeleeDelayChanceBase = row.OptionalInt32("skeleton_melee_delay_chance_base"),
                SkeletonMeleeDelayChanceIntelligenceScale = row.OptionalInt32("skeleton_melee_delay_chance_intelligence_scale"),
                GoatMeleeMovementChanceBase = row.OptionalInt32("goat_melee_movement_chance_base"),
                GoatMeleeMovementChanceIntelligenceScale = row.OptionalInt32("goat_melee_movement_chance_intelligence_scale"),
                FatMovementChanceBase = row.OptionalInt32("fat_movement_chance_base"),
                FatMovementChanceIntelligenceScale = row.OptionalInt32("fat_movement_chance_intelligence_scale"),
                ZombieActionChanceBase = row.OptionalInt32("zombie_action_chance_base"),
                ZombieActionChanceIntelligenceScale = row.OptionalInt32("zombie_action_chance_intelligence_scale"),
                ZombieMovementDistanceMinimum = row.OptionalInt32("zombie_movement_distance_minimum"),
                ZombieMovementDistanceBase = row.OptionalInt32("zombie_movement_distance_base"),
                ZombieMovementDistanceIntelligenceScale = row.OptionalInt32("zombie_movement_distance_intelligence_scale"),
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
