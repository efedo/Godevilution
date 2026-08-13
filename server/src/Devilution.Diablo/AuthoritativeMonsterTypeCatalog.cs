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

    /** Native resistance percentages derived from the active monster data. */
    public int FireResistance { get; init; }
    public int LightningResistance { get; init; }
    public int MagicResistance { get; init; }
    public int AcidResistance { get; init; }
}

/** Validated monster type metadata used to admit external encounter rows. */
public sealed class AuthoritativeMonsterTypeCatalog
{
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
                FireResistance = row.OptionalInt32("fire_resistance"),
                LightningResistance = row.OptionalInt32("lightning_resistance"),
                MagicResistance = row.OptionalInt32("magic_resistance"),
                AcidResistance = row.OptionalInt32("acid_resistance"),
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
