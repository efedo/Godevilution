using Devilution.Data;

namespace Devilution.Diablo;

/** Native ranged-AI branch selected by a validated monster missile mapping. */
public enum AuthoritativeMonsterRangedAttackMode
{
    Ranged,
    RangedAvoidance,
}

/** Validated native AI-to-missile mappings supplied by the active content pack. */
public sealed record AuthoritativeMonsterMissileDefinition(
    string NativeAi,
    string MissileId,
    AuthoritativeProjectileBehavior ProjectileBehavior,
    AuthoritativeMonsterRangedAttackMode AttackMode = AuthoritativeMonsterRangedAttackMode.Ranged,
    int AvoidanceProbabilityPercent = 100,
    uint MonsterId = 0);

/** Externalized native monster missile identities used by server-owned AI. */
public sealed class AuthoritativeMonsterMissileCatalog
{
    private readonly Dictionary<string, AuthoritativeMonsterMissileDefinition> definitions;

    public AuthoritativeMonsterMissileCatalog(
        IEnumerable<AuthoritativeMonsterMissileDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.definitions = new Dictionary<string, AuthoritativeMonsterMissileDefinition>(
            StringComparer.Ordinal);
        foreach (var definition in definitions) {
            if (!AuthoritativeMonsterTypeCatalog.IsValidNativeAi(definition.NativeAi)
                || string.IsNullOrWhiteSpace(definition.MissileId)
                || !Enum.IsDefined(definition.ProjectileBehavior)
                || !Enum.IsDefined(definition.AttackMode)
                || definition.AvoidanceProbabilityPercent is < 1 or > 100
                || !this.definitions.TryAdd(
                    CreateKey(definition.NativeAi, definition.MonsterId),
                    definition)) {
                throw new InvalidDataException(
                    "Monster missile definitions contain invalid or duplicate identities.");
            }
        }
        if (this.definitions.Count == 0)
            throw new InvalidDataException("The monster missile catalog cannot be empty.");
    }

    public IReadOnlyList<AuthoritativeMonsterMissileDefinition> Definitions
        => definitions.Values
            .OrderBy(definition => definition.NativeAi)
            .ThenBy(definition => definition.MonsterId)
            .ToArray();

    public bool TryGet(
        string nativeAi,
        out AuthoritativeMonsterMissileDefinition definition)
        => definitions.TryGetValue(CreateKey(nativeAi, 0), out definition!);

    /** Resolves a monster-specific row first, then its native-AI wildcard. */
    public bool TryGet(
        string nativeAi,
        uint monsterId,
        out AuthoritativeMonsterMissileDefinition definition)
        => definitions.TryGetValue(CreateKey(nativeAi, monsterId), out definition!)
            || definitions.TryGetValue(CreateKey(nativeAi, 0), out definition!);

    public static AuthoritativeMonsterMissileCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeMonsterMissileCatalog(table.Rows.Select(row => {
            var behaviorText = row.Required("projectile_behavior");
            if (!Enum.TryParse<AuthoritativeProjectileBehavior>(
                    behaviorText,
                    ignoreCase: false,
                    out var behavior)
                || !Enum.IsDefined(behavior)) {
                throw new InvalidDataException(
                    $"Monster missile row {row.LineNumber} contains unknown projectile behavior "
                    + $"'{behaviorText}'.");
            }
            return new AuthoritativeMonsterMissileDefinition(
                row.Required("native_ai"),
                row.Required("missile_id"),
                behavior,
                ParseAttackMode(row),
                row.OptionalInt32("avoidance_probability_percent", 100),
                row.OptionalUInt32("monster_id"));
        }));
    }

    private static string CreateKey(string nativeAi, uint monsterId)
        => $"{nativeAi}\u001F{monsterId}";

    private static AuthoritativeMonsterRangedAttackMode ParseAttackMode(TsvRow row)
    {
        if (!row.TryGet("attack_mode", out var text)
            || string.IsNullOrWhiteSpace(text))
            return AuthoritativeMonsterRangedAttackMode.Ranged;
        if (!Enum.TryParse<AuthoritativeMonsterRangedAttackMode>(
                text,
                ignoreCase: false,
                out var mode)
            || !Enum.IsDefined(mode)) {
            throw new InvalidDataException(
                $"Monster missile row {row.LineNumber} contains unknown attack mode '{text}'.");
        }
        return mode;
    }
}
