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

/** Coverage promise made by a monster spawn-policy table. */
public enum AuthoritativeMonsterSpawnPolicyCoverageMode
{
    Partial,
    Complete,
}

/** Content-owned admission policy for ordinary procedural monster spawning. */
public sealed record AuthoritativeMonsterSpawnPolicyDefinition(
    uint MonsterId,
    AuthoritativeMonsterSpawnRole SpawnRole);

/** Validated monster spawn-role overrides supplied by the active content pack. */
public sealed class AuthoritativeMonsterSpawnPolicyCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "monster_id",
        "monster_symbol",
        "spawn_role",
        "coverage_mode",
    ];

    private readonly IReadOnlyDictionary<uint, AuthoritativeMonsterSpawnRole> roles;

    public AuthoritativeMonsterSpawnPolicyCatalog(
        IEnumerable<AuthoritativeMonsterSpawnPolicyDefinition> definitions,
        AuthoritativeMonsterSpawnPolicyCoverageMode coverageMode =
            AuthoritativeMonsterSpawnPolicyCoverageMode.Partial)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (!Enum.IsDefined(coverageMode))
            throw new ArgumentOutOfRangeException(nameof(coverageMode));
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
        CoverageMode = coverageMode;
    }

    /** Declared table coverage; complete tables are checked during activation. */
    public AuthoritativeMonsterSpawnPolicyCoverageMode CoverageMode { get; }

    public bool RequiresCompleteCoverage
        => CoverageMode == AuthoritativeMonsterSpawnPolicyCoverageMode.Complete;

    public IReadOnlyList<AuthoritativeMonsterSpawnPolicyDefinition> Definitions
        => roles
            .Select(entry => new AuthoritativeMonsterSpawnPolicyDefinition(entry.Key, entry.Value))
            .ToArray();

    /** Rejects policy fallbacks at the current production content boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production monster-spawn policy table '{table.SourcePath}' must use the complete ordered symbolic schema.");
        foreach (var row in table.Rows)
            foreach (var column in ProductionColumns)
                _ = row.Required(column);
    }

    /** Returns true when an identity may enter ordinary procedural scattering. */
    public bool IsOrdinary(uint monsterId)
        => !roles.TryGetValue(monsterId, out var role)
            || role == AuthoritativeMonsterSpawnRole.Ordinary;

    /** Applies explicit policy first, then preserves the legacy pack fallback. */
    public bool IsOrdinary(AuthoritativeMonsterTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return roles.TryGetValue(definition.MonsterId, out var role)
            ? role == AuthoritativeMonsterSpawnRole.Ordinary
            : IsLegacyOrdinary(definition);
    }

    /** Compatibility admission used when older packs omit the policy table. */
    public static bool IsLegacyOrdinary(AuthoritativeMonsterTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return !definition.Diablo
            && !string.Equals(definition.NativeAi, "Golem", StringComparison.Ordinal);
    }

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

    /** Ensures the active pack declares a role for every shipped monster type. */
    public void ValidateCompleteMonsterTypes(AuthoritativeMonsterTypeCatalog monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(monsterTypes);
        ValidateMonsterTypes(monsterTypes);
        var missing = monsterTypes.Definitions
            .Where(definition => !roles.ContainsKey(definition.MonsterId))
            .Select(definition => definition.MonsterId)
            .ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException(
                $"Monster spawn policy is missing identities: {string.Join(", ", missing)}.");
    }

    public static AuthoritativeMonsterSpawnPolicyCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var coverageMode = ParseCoverageMode(table);
        return new AuthoritativeMonsterSpawnPolicyCatalog(table.Rows.Select(row =>
            new AuthoritativeMonsterSpawnPolicyDefinition(
                AuthoritativeMonsterIdentity.Resolve(row, stableIds),
                ParseRole(row.Required("spawn_role"), row.LineNumber))),
            coverageMode);
    }

    private static AuthoritativeMonsterSpawnPolicyCoverageMode ParseCoverageMode(
        TsvTable table)
    {
        var declared = table.Rows
            .Select(row => row.TryGet("coverage_mode", out var value)
                ? value.Trim()
                : string.Empty)
            .ToArray();
        var values = declared
            .Where(value => value.Length != 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (values.Length == 0)
            return AuthoritativeMonsterSpawnPolicyCoverageMode.Partial;
        if (values.Length != 1 || declared.Any(value => value.Length == 0))
            throw new InvalidDataException(
                $"Monster spawn policy table '{table.SourcePath}' has inconsistent coverage_mode declarations.");
        if (Enum.TryParse<AuthoritativeMonsterSpawnPolicyCoverageMode>(
                values[0],
                ignoreCase: true,
                out var mode)
            && Enum.IsDefined(mode))
            return mode;
        throw new InvalidDataException(
            $"Monster spawn policy table '{table.SourcePath}' has unknown coverage mode '{values[0]}'.");
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
