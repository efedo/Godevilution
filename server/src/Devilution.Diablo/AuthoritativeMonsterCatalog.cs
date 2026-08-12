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
        AuthoritativeUniqueItemCatalog? uniqueItems = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeMonsterCatalog(table.Rows.Select(row => {
            var stoneCurseImmune = row.OptionalInt32("stone_curse_immune");
            if (stoneCurseImmune is not (0 or 1)) {
                throw new InvalidDataException(
                    $"Monster row {row.LineNumber} has invalid stone_curse_immune value {stoneCurseImmune}.");
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
            return new AuthoritativeCombatTarget(
                row.RequiredUInt32("entity_id"),
                row.RequiredInt32("position_x"),
                row.RequiredInt32("position_y"),
                row.RequiredInt32("hit_points"),
                row.RequiredInt32("armor_class"),
                row.RequiredInt32("max_hit_points"),
                row.RequiredUInt32("level_id"),
                row.RequiredUInt32("monster_id"),
                row.OptionalUInt32("drop_item_entity_id"),
                dropItemSeed,
                row.OptionalUInt32("drop_item_price"),
                dropItemState,
                attackDamage: row.OptionalInt32("attack_damage"),
                aggroRange: row.OptionalInt32("aggro_range"),
                fireResistance: row.OptionalInt32("fire_resistance"),
                lightningResistance: row.OptionalInt32("lightning_resistance"),
                magicResistance: row.OptionalInt32("magic_resistance"),
                monsterLevel: row.OptionalInt32("monster_level"),
                stoneCurseImmune: stoneCurseImmune == 1);
        }));
    }
}
