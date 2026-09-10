using Devilution.Data;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Content-backed drop and encounter policy inherited by procedural encounters of one type. */
public sealed record AuthoritativeMonsterDropDefinition(
    uint MonsterId,
    uint DropItemId,
    uint DropUniqueItemId,
    int ItemLevel,
    bool OnlyGood,
    bool MonsterUnique,
    bool MonsterDiablo)
{
    public bool HasDrop => DropItemId != 0 || DropUniqueItemId != 0;
}

/** External encounter definitions for server-owned combat entities. */
public sealed class AuthoritativeMonsterCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "entity_id",
        "monster_id",
        "monster_symbol",
        "level_id",
        "level_symbol",
        "position_x",
        "position_y",
        "hit_points",
        "max_hit_points",
        "armor_class",
        "drop_item_entity_id",
        "drop_item_seed",
        "drop_item_price",
        "drop_item_id",
        "drop_item_symbol",
        "drop_item_level",
        "attack_damage",
        "aggro_range",
        "monster_level",
        "stone_curse_immune",
        "undead",
        "player_minion",
        "min_damage",
        "max_damage",
        "min_damage_special",
        "max_damage_special",
        "unique",
        "diablo",
        "berserk_immune",
        "leader_entity_id",
        "leader_relation",
        "pack_size",
    ];
    public AuthoritativeMonsterCatalog(
        IEnumerable<AuthoritativeCombatTarget> targets,
        IEnumerable<AuthoritativeMonsterDropDefinition>? dropDefinitions = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var values = targets.ToArray();
        if (values.Select(target => target.EntityId).Distinct().Count() != values.Length)
            throw new InvalidDataException("Monster entity IDs must be unique.");
        Targets = values.OrderBy(target => target.EntityId).ToArray();
        var drops = new Dictionary<uint, AuthoritativeMonsterDropDefinition>();
        foreach (var definition in dropDefinitions ?? []) {
            if (definition.MonsterId == 0
                || (definition.DropItemId != 0 && definition.DropUniqueItemId != 0)
                || definition.ItemLevel < 0
                || (!definition.HasDrop
                    && !definition.MonsterUnique
                    && !definition.MonsterDiablo)) {
                throw new InvalidDataException(
                    "Monster drop definitions contain invalid identities or bounds.");
            }
            if (drops.TryGetValue(definition.MonsterId, out var existing)
                && existing != definition) {
                throw new InvalidDataException(
                    $"Monster {definition.MonsterId} has conflicting procedural drop definitions.");
            }
            drops[definition.MonsterId] = definition;
        }
        DropDefinitions = drops.Values
            .OrderBy(definition => definition.MonsterId)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeCombatTarget> Targets { get; }

    public IReadOnlyList<AuthoritativeMonsterDropDefinition> DropDefinitions { get; }

    /** Validates authored monster levels and positions against world bounds. */
    public static void ValidateWorld(
        IEnumerable<AuthoritativeCombatTarget> targets,
        AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(world);
        foreach (var target in targets) {
            if (!world.ContainsLevel(target.LevelId))
                throw new InvalidDataException(
                    $"Monster {target.EntityId} references unknown level {target.LevelId}.");
            if (!world.ContainsPosition(target.LevelId, target.PositionX, target.PositionY))
                throw new InvalidDataException(
                    $"Monster {target.EntityId} contains a position outside its level.");
        }
    }

    public bool TryGetDropDefinition(
        uint monsterId,
        out AuthoritativeMonsterDropDefinition definition)
    {
        definition = DropDefinitions.SingleOrDefault(
            candidate => candidate.MonsterId == monsterId)!;
        return definition is not null;
    }

    /** Rejects partial authored monster metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production monster table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("entity_id");
            _ = row.Required("monster_id");
            _ = row.Required("monster_symbol");
            _ = row.Required("level_id");
            _ = row.Required("level_symbol");
            _ = row.Required("position_x");
            _ = row.Required("position_y");
            _ = row.Required("hit_points");
            _ = row.Required("max_hit_points");
            _ = row.Required("armor_class");
        }
    }

    public static AuthoritativeMonsterCatalog LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeItemCatalog? itemCatalog = null,
        AuthoritativeUniqueItemCatalog? uniqueItems = null,
        AuthoritativeMonsterTypeCatalog? monsterTypes = null,
        StableContentIdCatalog? stableIds = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var targets = table.Rows.Select(row => {
            var monsterId = AuthoritativeMonsterIdentity.Resolve(row, stableIds);
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
            var demon = ReadFlag(row, "demon", monsterType?.Demon ?? false);
            var animal = ReadFlag(row, "animal", monsterType?.Animal ?? false);
            var knockback = ReadFlag(row, "knockback", monsterType?.Knockback ?? false);
            var playerMinion = ReadFlag(row, "player_minion", false);
            var unique = ReadFlag(row, "unique", false);
            var diablo = ReadFlag(row, "diablo", false);
            var berserkImmune = ReadFlag(
                row,
                "berserk_immune",
                monsterType?.BerserkImmune ?? false);
            var deathAnimationTicks = row.TryGet("death_animation_ticks", out _)
                ? row.OptionalInt32("death_animation_ticks")
                : monsterType?.DeathAnimationTicks ?? 0;
            var experienceReward = row.TryGet("experience_reward", out _)
                ? row.OptionalUInt32("experience_reward")
                : monsterType?.ExperienceReward ?? 0;
            var toHit = row.TryGet("to_hit", out _)
                ? row.OptionalInt32("to_hit")
                : monsterType?.ToHit ?? 0;
            var toHitSpecial = row.TryGet("to_hit_special", out _)
                ? row.OptionalInt32("to_hit_special")
                : monsterType?.ToHitSpecial ?? 0;
            var nativeAi = row.TryGet("native_ai", out var nativeAiValue)
                && !string.IsNullOrWhiteSpace(nativeAiValue)
                ? nativeAiValue
                : monsterType?.NativeAi ?? string.Empty;
            var intelligence = row.TryGet("intelligence", out _)
                ? row.OptionalInt32("intelligence")
                : monsterType?.Intelligence ?? 0;
            if (deathAnimationTicks is < 0 or > NativeCompatibilityLimits.MaximumNativeMonsterDeathAnimationTicks) {
                throw new InvalidDataException(
                    $"Monster row {row.LineNumber} has an invalid death-animation duration.");
            }
            var dropItemId = AuthoritativeItemIdentity.ResolveOptional(
                row,
                stableIds,
                "drop_item_id",
                "drop_item_symbol");
            var dropUniqueItemId = AuthoritativeUniqueItemIdentity.ResolveOptional(
                row,
                stableIds,
                "drop_unique_item_id",
                "drop_unique_item_symbol");
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
                        row.OptionalBoolean("drop_only_good"),
                        spellBooks: spellBooks,
                        gameMode: gameMode);
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
            var leaderEntityId = row.OptionalUInt32("leader_entity_id");
            var leaderRelation = ParseLeaderRelation(row);
            var packSize = row.OptionalInt32("pack_size");
            return new AuthoritativeCombatTarget(
                row.RequiredUInt32("entity_id"),
                row.RequiredInt32("position_x"),
                row.RequiredInt32("position_y"),
                row.RequiredInt32("hit_points"),
                ReadInt32(row, "armor_class", monsterType?.ArmorClass ?? 0),
                row.RequiredInt32("max_hit_points"),
                AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "level_id",
                    "level_symbol"),
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
                demon: demon,
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
                diablo: diablo || (monsterType?.Diablo ?? false),
                berserkImmune: berserkImmune,
                deathAnimationTicks: deathAnimationTicks,
                experienceReward: experienceReward,
                toHit: toHit,
                toHitSpecial: toHitSpecial,
                nativeAi: nativeAi,
                intelligence: intelligence,
                animal: animal,
                knockback: knockback,
                leaderEntityId: leaderEntityId,
                leaderRelation: leaderRelation,
                packSize: packSize);
        }).ToArray();
        var dropDefinitions = table.Rows
            .Select(row => ParseDropDefinition(row, stableIds))
            .OfType<AuthoritativeMonsterDropDefinition>();
        return new AuthoritativeMonsterCatalog(targets, dropDefinitions);
    }

    private static AuthoritativeMonsterDropDefinition? ParseDropDefinition(
        TsvRow row,
        StableContentIdCatalog? stableIds)
    {
        var dropItemId = AuthoritativeItemIdentity.ResolveOptional(
            row,
            stableIds,
            "drop_item_id",
            "drop_item_symbol");
        var dropUniqueItemId = AuthoritativeUniqueItemIdentity.ResolveOptional(
            row,
            stableIds,
            "drop_unique_item_id",
            "drop_unique_item_symbol");
        var monsterUnique = ReadFlag(row, "unique", false);
        var monsterDiablo = ReadFlag(row, "diablo", false);
        if (dropItemId == 0
            && dropUniqueItemId == 0
            && !monsterUnique
            && !monsterDiablo)
            return null;
        if (dropItemId != 0 && dropUniqueItemId != 0)
            throw new InvalidDataException(
                $"Monster row {row.LineNumber} cannot reference both a base and unique drop.");
        var itemLevel = row.OptionalInt32(
            "drop_item_level",
            dropUniqueItemId != 0 ? 1 : 0);
        if (itemLevel < 0)
            throw new InvalidDataException(
                $"Monster row {row.LineNumber} has a negative drop item level.");
        return new AuthoritativeMonsterDropDefinition(
            AuthoritativeMonsterIdentity.Resolve(row, stableIds),
            dropItemId,
            dropUniqueItemId,
            itemLevel,
            ReadFlag(row, "drop_only_good", false),
            monsterUnique,
            monsterDiablo);
    }

    private static AuthoritativeMonsterLeaderRelation ParseLeaderRelation(TsvRow row)
    {
        if (!row.TryGet("leader_relation", out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return AuthoritativeMonsterLeaderRelation.None;
        }
        return value.Trim().ToLowerInvariant() switch {
            "leashed" => AuthoritativeMonsterLeaderRelation.Leashed,
            "separated" => AuthoritativeMonsterLeaderRelation.Separated,
            _ => throw new InvalidDataException(
                $"Monster row {row.LineNumber} contains an invalid leader relation '{value}'."),
        };
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
