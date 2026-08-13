using Devilution.Data;

namespace Devilution.Diablo;

/** External encounter definitions for server-owned combat entities. */
public sealed class AuthoritativeMonsterCatalog
{
    public AuthoritativeMonsterCatalog(IEnumerable<AuthoritativeCombatTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var values = targets.ToArray();
        if (values.Select(target => target.EntityId).Distinct().Count() != values.Length)
            throw new InvalidDataException("Monster entity IDs must be unique.");
        Targets = values.OrderBy(target => target.EntityId).ToArray();
    }

    public IReadOnlyList<AuthoritativeCombatTarget> Targets { get; }

    public static AuthoritativeMonsterCatalog LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeItemCatalog? itemCatalog = null,
        AuthoritativeUniqueItemCatalog? uniqueItems = null,
        AuthoritativeMonsterTypeCatalog? monsterTypes = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeMonsterCatalog(table.Rows.Select(row => {
            var monsterId = row.RequiredUInt32("monster_id");
            AuthoritativeMonsterTypeDefinition? monsterType = null;
            if (monsterTypes is not null) {
                if (!monsterTypes.TryGet(monsterId, out var resolvedMonsterType))
                    throw new InvalidDataException(
                        $"Monster row {row.LineNumber} references unknown monster type {monsterId}.");
                monsterType = resolvedMonsterType;
            }
            var stoneCurseImmune = ReadFlag(
                row,
                "stone_curse_immune",
                monsterType?.StoneCurseImmune ?? false);
            var undead = ReadFlag(row, "undead", monsterType?.Undead ?? false);
            var playerMinion = ReadFlag(row, "player_minion", false);
            var unique = ReadFlag(row, "unique", false);
            var diablo = ReadFlag(row, "diablo", false);
            var berserkImmune = ReadFlag(
                row,
                "berserk_immune",
                monsterType?.BerserkImmune ?? false);
            var deathAnimationTicks = row.OptionalInt32("death_animation_ticks");
            if (deathAnimationTicks is < 0 or > 1024) {
                throw new InvalidDataException(
                    $"Monster row {row.LineNumber} has an invalid death-animation duration.");
            }
            var dropItemId = row.OptionalUInt32("drop_item_id");
            var dropUniqueItemId = row.OptionalUInt32("drop_unique_item_id");
            var dropItemSeed = row.OptionalUInt32("drop_item_seed");
            AuthoritativeItemState? dropItemState = null;
            if (dropItemId != 0 || dropUniqueItemId != 0) {
                if (itemCatalog is null)
                    throw new InvalidDataException($"Monster row {row.LineNumber} references a catalog drop without an item catalog.");
                if (dropItemSeed == 0)
                    throw new InvalidDataException($"Monster row {row.LineNumber} has a drop item without a seed.");
                if (dropItemId != 0 && dropUniqueItemId != 0)
                    throw new InvalidDataException($"Monster row {row.LineNumber} cannot reference both a base and unique drop.");
                dropItemState = dropUniqueItemId != 0
                    ? uniqueItems is null
                        ? throw new InvalidDataException($"Monster row {row.LineNumber} references a unique drop without a unique catalog.")
                        : itemCatalog.GenerateUnique(dropUniqueItemId, dropItemSeed, row.OptionalInt32("drop_item_level", 1))
                    : itemCatalog.GenerateDrop(
                        dropItemId,
                        dropItemSeed,
                        row.OptionalInt32("drop_item_level"),
                        row.OptionalInt32("drop_only_good") != 0);
            }
            var fireResistance = row.TryGet("fire_resistance", out _)
                ? row.OptionalInt32("fire_resistance")
                : monsterType?.FireResistance ?? 0;
            var lightningResistance = row.TryGet("lightning_resistance", out _)
                ? row.OptionalInt32("lightning_resistance")
                : monsterType?.LightningResistance ?? 0;
            var magicResistance = row.TryGet("magic_resistance", out _)
                ? row.OptionalInt32("magic_resistance")
                : monsterType?.MagicResistance ?? 0;
            var acidResistance = row.TryGet("acid_resistance", out _)
                ? row.OptionalInt32("acid_resistance")
                : monsterType?.AcidResistance ?? 0;
            return new AuthoritativeCombatTarget(
                row.RequiredUInt32("entity_id"),
                row.RequiredInt32("position_x"),
                row.RequiredInt32("position_y"),
                row.RequiredInt32("hit_points"),
                ReadInt32(row, "armor_class", monsterType?.ArmorClass ?? 0),
                row.RequiredInt32("max_hit_points"),
                row.RequiredUInt32("level_id"),
                monsterId,
                row.OptionalUInt32("drop_item_entity_id"),
                dropItemSeed,
                row.OptionalUInt32("drop_item_price"),
                dropItemState,
                attackDamage: ReadInt32(
                    row,
                    "attack_damage",
                    monsterType?.MinimumDamage ?? 0),
                aggroRange: row.OptionalInt32("aggro_range"),
                fireResistance: fireResistance,
                lightningResistance: lightningResistance,
                magicResistance: magicResistance,
                acidResistance: acidResistance,
                monsterLevel: ReadInt32(row, "monster_level", monsterType?.MonsterLevel ?? 0),
                stoneCurseImmune: stoneCurseImmune,
                undead: undead,
                playerMinion: playerMinion,
                minDamage: ReadInt32(
                    row,
                    "min_damage",
                    monsterType?.MinimumDamage ?? ReadInt32(row, "attack_damage", 0)),
                maxDamage: ReadInt32(
                    row,
                    "max_damage",
                    monsterType?.MaximumDamage ?? ReadInt32(row, "attack_damage", 0)),
                minDamageSpecial: ReadInt32(
                    row,
                    "min_damage_special",
                    monsterType?.MinimumSpecialDamage ?? ReadInt32(row, "attack_damage", 0)),
                maxDamageSpecial: ReadInt32(
                    row,
                    "max_damage_special",
                    monsterType?.MaximumSpecialDamage ?? ReadInt32(row, "attack_damage", 0)),
                unique: unique,
                diablo: diablo,
                berserkImmune: berserkImmune,
                deathAnimationTicks: deathAnimationTicks);
        }));
    }

    private static int ReadInt32(TsvRow row, string column, int defaultValue)
        => row.OptionalInt32(column, defaultValue);

    private static bool ReadFlag(TsvRow row, string column, bool defaultValue)
    {
        var value = row.OptionalInt32(column, defaultValue ? 1 : 0);
        if (value is not (0 or 1))
            throw new InvalidDataException(
                $"Monster row {row.LineNumber} has invalid {column} value {value}.");
        return value == 1;
    }
}
