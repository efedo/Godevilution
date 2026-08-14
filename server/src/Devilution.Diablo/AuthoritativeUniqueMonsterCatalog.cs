using Devilution.Data;

namespace Devilution.Diablo;

/** Validated native unique-monster identities attached to static encounter entities. */
public sealed record AuthoritativeUniqueMonsterDefinition(
    uint EntityId,
    uint MonsterId,
    string Identity);

/** External unique identity catalog kept separate from base monster type metadata. */
public sealed class AuthoritativeUniqueMonsterCatalog
{
    private readonly IReadOnlyDictionary<uint, AuthoritativeUniqueMonsterDefinition> definitions;

    public AuthoritativeUniqueMonsterCatalog(
        IEnumerable<AuthoritativeUniqueMonsterDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Length == 0
            || values.Any(definition => definition.EntityId == 0
                || definition.MonsterId == 0
                || string.IsNullOrWhiteSpace(definition.Identity)
                || definition.Identity.Length > 128)
            || values.GroupBy(definition => definition.EntityId).Any(group => group.Count() != 1)
            || values.GroupBy(definition => definition.Identity, StringComparer.Ordinal)
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Unique monster definitions contain invalid or duplicate identities.");
        }
        this.definitions = values.ToDictionary(
            definition => definition.EntityId,
            definition => definition);
    }

    public IReadOnlyList<AuthoritativeUniqueMonsterDefinition> Definitions
        => definitions.Values.OrderBy(definition => definition.EntityId).ToArray();

    public void ApplyTo(IReadOnlyList<AuthoritativeCombatTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var targetsByEntity = targets.ToDictionary(target => target.EntityId);
        foreach (var definition in Definitions) {
            if (!targetsByEntity.TryGetValue(definition.EntityId, out var target))
                throw new InvalidDataException(
                    $"Unique monster {definition.Identity} references unknown entity {definition.EntityId}.");
            if (target.MonsterId != definition.MonsterId || !target.IsUnique)
                throw new InvalidDataException(
                    $"Unique monster {definition.Identity} does not match unique monster entity {definition.EntityId}.");
            target.UniqueIdentity = definition.Identity;
        }
    }

    public static AuthoritativeUniqueMonsterCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeUniqueMonsterCatalog(table.Rows.Select(row =>
            new AuthoritativeUniqueMonsterDefinition(
                row.RequiredUInt32("entity_id"),
                row.RequiredUInt32("monster_id"),
                row.Required("identity"))));
    }
}
