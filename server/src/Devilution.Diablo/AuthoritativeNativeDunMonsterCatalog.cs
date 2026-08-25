using Devilution.Data;

namespace Devilution.Diablo;

/** Mapping from a raw native DUN monster-layer value to a native monster identity. */
public sealed record AuthoritativeNativeDunMonsterDefinition(
    ushort NativeDunMonsterId,
    uint NativeMonsterId)
{
    public AuthoritativeNativeDunMonsterDefinition Validate()
    {
        if (NativeDunMonsterId == 0
            || NativeDunMonsterId > AuthoritativeLevel.NativeMaximumDunMonsterLayerId) {
            throw new InvalidDataException(
                "Native DUN monster mappings contain an invalid raw layer identity.");
        }
        return this;
    }
}

/** Validated external native DUN monster-layer mappings.
 *
 * The native layer stores an index into DevilutionX's MonstConvTbl rather than
 * an authoritative monster identity. This catalog externalizes that conversion
 * without creating gameplay entities; spawn materialization is a separate seam.
 */
public sealed class AuthoritativeNativeDunMonsterCatalog
{
    private readonly IReadOnlyDictionary<ushort, AuthoritativeNativeDunMonsterDefinition>
        definitions;

    public AuthoritativeNativeDunMonsterCatalog(
        IEnumerable<AuthoritativeNativeDunMonsterDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions
            .Select(definition => definition.Validate())
            .ToArray();
        if (values.GroupBy(definition => definition.NativeDunMonsterId)
            .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Native DUN monster mappings contain duplicate raw layer identities.");
        }
        this.definitions = values.ToDictionary(
            definition => definition.NativeDunMonsterId);
    }

    public IReadOnlyList<AuthoritativeNativeDunMonsterDefinition> Definitions
        => definitions.Values
            .OrderBy(definition => definition.NativeDunMonsterId)
            .ToArray();

    public bool TryGet(
        ushort nativeDunMonsterId,
        out AuthoritativeNativeDunMonsterDefinition definition)
        => definitions.TryGetValue(nativeDunMonsterId, out definition!);

    public bool TryGetNativeMonsterId(ushort nativeDunMonsterId, out uint nativeMonsterId)
    {
        if (!definitions.TryGetValue(nativeDunMonsterId, out var definition)) {
            nativeMonsterId = 0;
            return false;
        }
        nativeMonsterId = definition.NativeMonsterId;
        return true;
    }

    /** Fails activation if a mapping points at an absent native monster type. */
    public void ValidateMonsterTypes(AuthoritativeMonsterTypeCatalog monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(monsterTypes);
        foreach (var definition in Definitions) {
            if (!monsterTypes.Definitions.Any(
                    monsterType => monsterType.NativeMonsterId == definition.NativeMonsterId))
                throw new InvalidDataException(
                    $"Native DUN monster mapping {definition.NativeDunMonsterId} references "
                    + $"unknown native monster {definition.NativeMonsterId}.");
        }
    }

    public static AuthoritativeNativeDunMonsterCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeNativeDunMonsterCatalog(table.Rows.Select(row => {
            var rawId = row.RequiredUInt32("native_dun_monster_id");
            if (rawId > ushort.MaxValue)
                throw new InvalidDataException(
                    "Native DUN monster mappings contain an invalid raw layer identity.");
            return new AuthoritativeNativeDunMonsterDefinition(
                (ushort)rawId,
                row.RequiredUInt32("native_monster_id"));
        }));
    }
}
