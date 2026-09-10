using Devilution.Data;
using Devilution.Domain.Protocol;

namespace Devilution.Diablo;

/**
 * Validated native unique-monster identity binding. A row may address one
 * static entity or exactly one generated unique target by monster/native-AI
 * selector.
 */
public sealed record AuthoritativeUniqueMonsterDefinition(
    uint EntityId,
    uint MonsterId,
    string Identity,
    string NativeAi = "")
{
    /** Optional native spawn health percentage applied when this identity is materialized. */
    public int? SpawnHealthPercent { get; init; }

    /** Native armor adjustment applied by an encounter-specific spawn transition. */
    public int SpawnArmorDelta { get; init; }

    /** Optional native resistance overrides applied by an encounter-specific spawn transition. */
    public int? SpawnFireResistance { get; init; }
    public int? SpawnLightningResistance { get; init; }
    public int? SpawnMagicResistance { get; init; }
    public int? SpawnAcidResistance { get; init; }
}

/** External unique identity catalog kept separate from base monster type metadata. */
public sealed class AuthoritativeUniqueMonsterCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "entity_id",
        "monster_id",
        "monster_symbol",
        "identity",
        "native_ai",
        "spawn_health_percent",
        "spawn_armor_delta",
        "spawn_fire_resistance",
        "spawn_lightning_resistance",
        "spawn_magic_resistance",
        "spawn_acid_resistance",
    ];

    private readonly IReadOnlyList<AuthoritativeUniqueMonsterDefinition> definitions;

    public AuthoritativeUniqueMonsterCatalog(
        IEnumerable<AuthoritativeUniqueMonsterDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Length == 0
            || values.Any(definition => (definition.EntityId == 0
                    && definition.MonsterId == 0
                    && string.IsNullOrWhiteSpace(definition.NativeAi))
                || string.IsNullOrWhiteSpace(definition.Identity)
                || !NativeTextLimits.IsIdentityWithinLimit(definition.Identity)
                || !NativeTextLimits.IsIdentityWithinLimit(definition.NativeAi)
                || definition.SpawnHealthPercent is < 1 or > 100
                || definition.SpawnArmorDelta is < -255 or > 255
                || definition.SpawnFireResistance is < -100 or > 100
                || definition.SpawnLightningResistance is < -100 or > 100
                || definition.SpawnMagicResistance is < -100 or > 100
                || definition.SpawnAcidResistance is < -100 or > 100)
            || values.Where(definition => definition.EntityId != 0)
                .GroupBy(definition => definition.EntityId)
                .Any(group => group.Count() != 1)
            || values.GroupBy(definition => definition.Identity, StringComparer.Ordinal)
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Unique monster definitions contain invalid or duplicate identities.");
        }
        this.definitions = values
            .OrderBy(definition => definition.EntityId)
            .ThenBy(definition => definition.MonsterId)
            .ThenBy(definition => definition.NativeAi, StringComparer.Ordinal)
            .ThenBy(definition => definition.Identity, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeUniqueMonsterDefinition> Definitions
        => definitions;

    /**
     * Validates explicit monster selectors against the active type catalog.
     * Native-AI-only selectors remain deferred because their generated target
     * may be supplied by a later level or set-piece materialization.
     */
    public void ValidateMonsterTypes(AuthoritativeMonsterTypeCatalog monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(monsterTypes);
        foreach (var definition in Definitions.Where(definition => definition.MonsterId != 0)) {
            if (!monsterTypes.TryGet(definition.MonsterId, out var monsterType))
                throw new InvalidDataException(
                    $"Unique monster {definition.Identity} references unknown monster {definition.MonsterId}.");
            if (definition.NativeAi.Length != 0
                && !string.Equals(
                    definition.NativeAi,
                    monsterType.NativeAi,
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Unique monster {definition.Identity} does not match native AI '{definition.NativeAi}'.");
        }
    }

    public void ApplyTo(IReadOnlyList<AuthoritativeCombatTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var targetsByEntity = targets.ToDictionary(target => target.EntityId);
        foreach (var definition in Definitions) {
            var matches = definition.EntityId != 0
                ? targetsByEntity.TryGetValue(definition.EntityId, out var target)
                    ? [target]
                    : Array.Empty<AuthoritativeCombatTarget>()
                : targets
                    .Where(target => target.IsUnique
                        && (definition.MonsterId == 0
                            || target.MonsterId == definition.MonsterId)
                        && (definition.NativeAi.Length == 0
                            || string.Equals(
                                target.NativeAi,
                                definition.NativeAi,
                                StringComparison.Ordinal)))
                    .ToArray();
            // Selector rows may target a procedurally materialized encounter
            // that is not part of the static pack. Static entity rows remain
            // strict; generated selectors are applied by ApplyToGenerated.
            if (definition.EntityId == 0 && matches.Length == 0)
                continue;
            if (matches.Length != 1)
                throw new InvalidDataException(
                    $"Unique monster {definition.Identity} must match exactly one unique encounter; found {matches.Length}.");
            var matchedTarget = matches[0];
            if ((definition.MonsterId != 0 && matchedTarget.MonsterId != definition.MonsterId)
                || (definition.NativeAi.Length != 0
                    && !string.Equals(
                        matchedTarget.NativeAi,
                        definition.NativeAi,
                        StringComparison.Ordinal)))
                throw new InvalidDataException(
                    $"Unique monster {definition.Identity} does not match its authored encounter selector.");
            if (!matchedTarget.IsUnique)
                throw new InvalidDataException(
                    $"Unique monster {definition.Identity} references a non-unique encounter.");
            if (matchedTarget.UniqueIdentity.Length != 0
                && !string.Equals(
                    matchedTarget.UniqueIdentity,
                    definition.Identity,
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Unique monster encounter {matchedTarget.EntityId} already has a conflicting identity.");
            matchedTarget.UniqueIdentity = definition.Identity;
            ApplySpawnModifiers(matchedTarget, definition);
        }
    }

    /** Applies selector rows to one generated encounter set when present. */
    public void ApplyToGenerated(IReadOnlyList<AuthoritativeCombatTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        foreach (var definition in Definitions.Where(definition => definition.EntityId == 0)) {
            var matches = targets
                .Where(target => target.IsUnique
                    && (definition.MonsterId == 0
                        || target.MonsterId == definition.MonsterId)
                    && (definition.NativeAi.Length == 0
                        || string.Equals(
                            target.NativeAi,
                            definition.NativeAi,
                            StringComparison.Ordinal)))
                .ToArray();
            if (matches.Length > 1)
                throw new InvalidDataException(
                    $"Generated unique monster {definition.Identity} must match at most one encounter; found {matches.Length}.");
            if (matches.Length == 0)
                continue;

            var matchedTarget = matches[0];
            if (matchedTarget.UniqueIdentity.Length != 0
                && !string.Equals(
                    matchedTarget.UniqueIdentity,
                    definition.Identity,
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Unique monster encounter {matchedTarget.EntityId} already has a conflicting identity.");
            matchedTarget.UniqueIdentity = definition.Identity;
            ApplySpawnModifiers(matchedTarget, definition);
        }
    }

    /** Applies one explicitly authored generated-placement identity. */
    public void ApplyToExplicit(
        AuthoritativeCombatTarget target,
        string identity)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        var definition = Definitions.SingleOrDefault(candidate =>
            string.Equals(candidate.Identity, identity, StringComparison.Ordinal));
        if (definition is null)
            throw new InvalidDataException(
                $"Unique monster identity '{identity}' is not defined.");
        if (definition.EntityId != 0)
            throw new InvalidDataException(
                $"Unique monster identity '{identity}' is not a generated selector.");
        if (!target.IsUnique
            || (definition.MonsterId != 0 && target.MonsterId != definition.MonsterId)
            || (definition.NativeAi.Length != 0
                && !string.Equals(
                    target.NativeAi,
                    definition.NativeAi,
                    StringComparison.Ordinal))) {
            throw new InvalidDataException(
                $"Unique monster identity '{identity}' does not match its authored target.");
        }
        if (target.UniqueIdentity.Length != 0) {
            if (!string.Equals(target.UniqueIdentity, identity, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Unique monster target {target.EntityId} already has a conflicting identity.");
            return;
        }
        target.UniqueIdentity = identity;
        ApplySpawnModifiers(target, definition);
    }

    private static void ApplySpawnModifiers(
        AuthoritativeCombatTarget target,
        AuthoritativeUniqueMonsterDefinition definition)
    {
        if (definition.SpawnHealthPercent is int healthPercent) {
            var maximum = checked(target.MaxHitPointsQ6 * healthPercent / 100);
            var current = checked(target.HitPointsQ6 * healthPercent / 100);
            target.RestoreHitPointsQ6(current, maximum);
        }
        target.ArmorClass = checked(target.ArmorClass + definition.SpawnArmorDelta);
        if (definition.SpawnFireResistance is int fire)
            target.FireResistance = fire;
        if (definition.SpawnLightningResistance is int lightning)
            target.LightningResistance = lightning;
        if (definition.SpawnMagicResistance is int magic)
            target.MagicResistance = magic;
        if (definition.SpawnAcidResistance is int acid)
            target.AcidResistance = acid;
    }

    /** Rejects partial unique-monster identity metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production unique-monster table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows)
            _ = row.Required("identity");
    }

    public static AuthoritativeUniqueMonsterCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeUniqueMonsterCatalog(table.Rows.Select(row =>
            new AuthoritativeUniqueMonsterDefinition(
                row.OptionalUInt32("entity_id"),
                AuthoritativeMonsterIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "monster_id",
                    "monster_symbol"),
                row.Required("identity"),
                row.TryGet("native_ai", out var nativeAi)
                    ? nativeAi.Trim()
                    : string.Empty) {
                SpawnHealthPercent = OptionalNullableInt32(row, "spawn_health_percent"),
                SpawnArmorDelta = row.OptionalInt32("spawn_armor_delta"),
                SpawnFireResistance = OptionalNullableInt32(row, "spawn_fire_resistance"),
                SpawnLightningResistance = OptionalNullableInt32(row, "spawn_lightning_resistance"),
                SpawnMagicResistance = OptionalNullableInt32(row, "spawn_magic_resistance"),
                SpawnAcidResistance = OptionalNullableInt32(row, "spawn_acid_resistance"),
            }));
    }

    private static int? OptionalNullableInt32(TsvRow row, string column)
    {
        if (!row.TryGet(column, out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }
        return row.OptionalInt32(column);
    }
}
