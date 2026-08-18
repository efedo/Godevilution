using Devilution.Data;

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
    string NativeAi = "");

/** External unique identity catalog kept separate from base monster type metadata. */
public sealed class AuthoritativeUniqueMonsterCatalog
{
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
                || definition.Identity.Length > 128
                || definition.NativeAi.Length > 128)
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
        }
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
                    : string.Empty)));
    }
}
