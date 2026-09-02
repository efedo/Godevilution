using Devilution.Data;

namespace Devilution.Diablo;

/** Purpose assigned to a monster identity when selecting procedural encounters. */
public enum AuthoritativeMonsterSpawnRole
{
    Ordinary,
    Summon,
    Boss,
    Quest,
}

/** Content-owned admission policy for ordinary procedural monster spawning. */
public sealed record AuthoritativeMonsterSpawnPolicyDefinition(
    uint MonsterId,
    AuthoritativeMonsterSpawnRole SpawnRole);

/** Validated monster spawn-role overrides supplied by the active content pack. */
public sealed class AuthoritativeMonsterSpawnPolicyCatalog
{
    private readonly IReadOnlyDictionary<uint, AuthoritativeMonsterSpawnRole> roles;

    public AuthoritativeMonsterSpawnPolicyCatalog(
        IEnumerable<AuthoritativeMonsterSpawnPolicyDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(definition => definition.MonsterId == 0
                || !Enum.IsDefined(definition.SpawnRole))
            || values.GroupBy(definition => definition.MonsterId)
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Monster spawn policies contain invalid or duplicate identities.");
        }
        roles = values
            .OrderBy(definition => definition.MonsterId)
            .ToDictionary(definition => definition.MonsterId, definition => definition.SpawnRole);
    }

    public IReadOnlyList<AuthoritativeMonsterSpawnPolicyDefinition> Definitions
        => roles
            .Select(entry => new AuthoritativeMonsterSpawnPolicyDefinition(entry.Key, entry.Value))
            .ToArray();

    /** Returns true when an identity may enter ordinary procedural scattering. */
    public bool IsOrdinary(uint monsterId)
        => !roles.TryGetValue(monsterId, out var role)
            || role == AuthoritativeMonsterSpawnRole.Ordinary;

    /** Ensures every policy row addresses an active monster type. */
    public void ValidateMonsterTypes(AuthoritativeMonsterTypeCatalog monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(monsterTypes);
        foreach (var monsterId in roles.Keys) {
            if (!monsterTypes.TryGet(monsterId, out _))
                throw new InvalidDataException(
                    $"Monster spawn policy references unknown monster {monsterId}.");
        }
    }

    public static AuthoritativeMonsterSpawnPolicyCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeMonsterSpawnPolicyCatalog(table.Rows.Select(row =>
            new AuthoritativeMonsterSpawnPolicyDefinition(
                AuthoritativeMonsterIdentity.Resolve(row, stableIds),
                ParseRole(row.Required("spawn_role"), row.LineNumber))));
    }

    private static AuthoritativeMonsterSpawnRole ParseRole(string value, int lineNumber)
    {
        if (Enum.TryParse<AuthoritativeMonsterSpawnRole>(value, ignoreCase: true, out var role)
            && Enum.IsDefined(role))
            return role;
        throw new InvalidDataException(
            $"Monster spawn policy row {lineNumber} has unknown spawn role '{value}'.");
    }
}
