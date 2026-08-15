using Devilution.Data;

namespace Devilution.Diablo;

/** Validated native difficulty modifiers supplied by the active combat pack. */
public sealed record DiabloDifficultyRules
{
    public static DiabloDifficultyRules NativeDefaults { get; } = new();

    public int NightmareLevelOffset { get; init; } = 15;
    public int HellLevelOffset { get; init; } = 30;
    public int NightmareToHitBonus { get; init; } = 85;
    public int HellToHitBonus { get; init; } = 120;
    public int NightmareArmorBonus { get; init; } = 50;
    public int HellArmorBonus { get; init; } = 80;
    public int NightmareDamageMultiplier { get; init; } = 2;
    public int NightmareDamageBonus { get; init; } = 2;
    public int HellDamageMultiplier { get; init; } = 4;
    public int HellDamageBonus { get; init; } = 6;
    public int NightmareExperienceMultiplier { get; init; } = 2;
    public int HellExperienceMultiplier { get; init; } = 4;
    public uint ExperienceBonus { get; init; } = 1000;
    public int NightmareHealthMultiplier { get; init; } = 3;
    public int HellHealthMultiplier { get; init; } = 4;
    public int NightmareDiabloHealthBonus { get; init; } = 100;
    public int NightmareHellfireSinglePlayerHealthBonus { get; init; } = 50;
    public int NightmareHellfireMultiplayerHealthBonus { get; init; } = 100;
    public int HellDiabloHealthBonus { get; init; } = 200;
    public int HellHellfireSinglePlayerHealthBonus { get; init; } = 100;
    public int HellHellfireMultiplayerHealthBonus { get; init; } = 200;

    public DiabloDifficultyRules Validate()
    {
        if (NightmareLevelOffset is < 0 or > 100
            || HellLevelOffset is < 0 or > 100
            || NightmareToHitBonus is < 0 or > 1000
            || HellToHitBonus is < 0 or > 1000
            || NightmareArmorBonus is < 0 or > 10_000
            || HellArmorBonus is < 0 or > 10_000
            || NightmareDamageMultiplier is < 1 or > 100
            || NightmareDamageBonus is < 0 or > 10_000
            || HellDamageMultiplier is < 1 or > 100
            || HellDamageBonus is < 0 or > 10_000
            || NightmareExperienceMultiplier is < 1 or > 100
            || HellExperienceMultiplier is < 1 or > 100
            || ExperienceBonus > 1_000_000
            || NightmareHealthMultiplier is < 1 or > 100
            || HellHealthMultiplier is < 1 or > 100
            || NightmareDiabloHealthBonus is < 0 or > 1_000_000
            || NightmareHellfireSinglePlayerHealthBonus is < 0 or > 1_000_000
            || NightmareHellfireMultiplayerHealthBonus is < 0 or > 1_000_000
            || HellDiabloHealthBonus is < 0 or > 1_000_000
            || HellHellfireSinglePlayerHealthBonus is < 0 or > 1_000_000
            || HellHellfireMultiplayerHealthBonus is < 0 or > 1_000_000) {
            throw new InvalidDataException("Difficulty tuning is invalid.");
        }
        return this;
    }

    public static DiabloDifficultyRules LoadTsv(TsvRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new DiabloDifficultyRules {
            NightmareLevelOffset = row.OptionalInt32("difficulty_nightmare_level_offset", 15),
            HellLevelOffset = row.OptionalInt32("difficulty_hell_level_offset", 30),
            NightmareToHitBonus = row.OptionalInt32("difficulty_nightmare_to_hit_bonus", 85),
            HellToHitBonus = row.OptionalInt32("difficulty_hell_to_hit_bonus", 120),
            NightmareArmorBonus = row.OptionalInt32("difficulty_nightmare_armor_bonus", 50),
            HellArmorBonus = row.OptionalInt32("difficulty_hell_armor_bonus", 80),
            NightmareDamageMultiplier = row.OptionalInt32("difficulty_nightmare_damage_multiplier", 2),
            NightmareDamageBonus = row.OptionalInt32("difficulty_nightmare_damage_bonus", 2),
            HellDamageMultiplier = row.OptionalInt32("difficulty_hell_damage_multiplier", 4),
            HellDamageBonus = row.OptionalInt32("difficulty_hell_damage_bonus", 6),
            NightmareExperienceMultiplier = row.OptionalInt32("difficulty_nightmare_experience_multiplier", 2),
            HellExperienceMultiplier = row.OptionalInt32("difficulty_hell_experience_multiplier", 4),
            ExperienceBonus = row.OptionalUInt32("difficulty_experience_bonus", 1000),
            NightmareHealthMultiplier = row.OptionalInt32("difficulty_nightmare_health_multiplier", 3),
            HellHealthMultiplier = row.OptionalInt32("difficulty_hell_health_multiplier", 4),
            NightmareDiabloHealthBonus = row.OptionalInt32("difficulty_nightmare_diablo_health_bonus", 100),
            NightmareHellfireSinglePlayerHealthBonus = row.OptionalInt32(
                "difficulty_nightmare_hellfire_single_player_health_bonus", 50),
            NightmareHellfireMultiplayerHealthBonus = row.OptionalInt32(
                "difficulty_nightmare_hellfire_multiplayer_health_bonus", 100),
            HellDiabloHealthBonus = row.OptionalInt32("difficulty_hell_diablo_health_bonus", 200),
            HellHellfireSinglePlayerHealthBonus = row.OptionalInt32(
                "difficulty_hell_hellfire_single_player_health_bonus", 100),
            HellHellfireMultiplayerHealthBonus = row.OptionalInt32(
                "difficulty_hell_hellfire_multiplayer_health_bonus", 200),
        }.Validate();
    }

    public int GetLevelOffset(DiabloDifficulty difficulty)
        => difficulty switch {
            DiabloDifficulty.Nightmare => NightmareLevelOffset,
            DiabloDifficulty.Hell => HellLevelOffset,
            _ => 0,
        };

    public int GetToHitBonus(DiabloDifficulty difficulty)
        => difficulty switch {
            DiabloDifficulty.Nightmare => NightmareToHitBonus,
            DiabloDifficulty.Hell => HellToHitBonus,
            _ => 0,
        };

    public int GetArmorBonus(DiabloDifficulty difficulty)
        => difficulty switch {
            DiabloDifficulty.Nightmare => NightmareArmorBonus,
            DiabloDifficulty.Hell => HellArmorBonus,
            _ => 0,
        };

    public int ApplyDamageScaling(int baseDamage, DiabloDifficulty difficulty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseDamage);
        return difficulty switch {
            // Native Nightmare computes 2 * (base + 2).
            DiabloDifficulty.Nightmare => checked(
                NightmareDamageMultiplier * (baseDamage + NightmareDamageBonus)),
            // Native Hell computes 4 * base + 6.
            DiabloDifficulty.Hell => checked(
                HellDamageMultiplier * baseDamage + HellDamageBonus),
            _ => baseDamage,
        };
    }

    public int GetExperienceMultiplier(DiabloDifficulty difficulty)
        => difficulty switch {
            DiabloDifficulty.Nightmare => NightmareExperienceMultiplier,
            DiabloDifficulty.Hell => HellExperienceMultiplier,
            _ => 1,
        };

    public int GetHealthMultiplier(DiabloDifficulty difficulty)
        => difficulty switch {
            DiabloDifficulty.Nightmare => NightmareHealthMultiplier,
            DiabloDifficulty.Hell => HellHealthMultiplier,
            _ => 1,
        };

    public int GetHealthBonus(
        DiabloDifficulty difficulty,
        DiabloGameVariant gameVariant,
        DiabloGameMode gameMode)
        => difficulty switch {
            DiabloDifficulty.Nightmare when gameVariant == DiabloGameVariant.Hellfire
                => gameMode == DiabloGameMode.Multiplayer
                    ? NightmareHellfireMultiplayerHealthBonus
                    : NightmareHellfireSinglePlayerHealthBonus,
            DiabloDifficulty.Nightmare => NightmareDiabloHealthBonus,
            DiabloDifficulty.Hell when gameVariant == DiabloGameVariant.Hellfire
                => gameMode == DiabloGameMode.Multiplayer
                    ? HellHellfireMultiplayerHealthBonus
                    : HellHellfireSinglePlayerHealthBonus,
            DiabloDifficulty.Hell => HellDiabloHealthBonus,
            _ => 0,
        };
}
