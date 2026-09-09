using Devilution.Data;
using Devilution.Shared;

namespace Devilution.Diablo;

/** How a generated unique encounter's additional monsters relate to its leader. */
public enum AuthoritativeUniqueMonsterPackKind
{
    Independent,
    Leashed,
}

/** Validated pack metadata for one generated unique-monster placement. */
public sealed record AuthoritativeUniqueMonsterPackDefinition(
    uint PackId,
    uint PlacementId,
    uint MinionMonsterId,
    int PackSize,
    AuthoritativeUniqueMonsterPackKind Kind = AuthoritativeUniqueMonsterPackKind.Leashed);

/** Optional content table that externalizes native unique-pack placement. */
public sealed class AuthoritativeUniqueMonsterPackCatalog
{
    private readonly IReadOnlyList<AuthoritativeUniqueMonsterPackDefinition> definitions;

    public AuthoritativeUniqueMonsterPackCatalog(
        IEnumerable<AuthoritativeUniqueMonsterPackDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(definition => definition.PackId == 0
                || definition.PlacementId == 0
                || definition.MinionMonsterId == 0
                || definition.PackSize is < 1 or > NativeCompatibilityLimits.MaximumNativeMonsterPackSize
                || !Enum.IsDefined(definition.Kind))
            || values.GroupBy(definition => definition.PackId)
                .Any(group => group.Count() != 1)
            || values.GroupBy(definition => definition.PlacementId)
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Unique monster pack definitions contain invalid or duplicate identities.");
        }
        this.definitions = values
            .OrderBy(definition => definition.PackId)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeUniqueMonsterPackDefinition> Definitions
        => definitions;

    public AuthoritativeUniqueMonsterPackDefinition? FindForPlacement(uint placementId)
        => definitions.SingleOrDefault(definition => definition.PlacementId == placementId);

    /** Validates pack references against the active placement and monster catalogs. */
    public void Validate(
        AuthoritativeUniqueMonsterPlacementCatalog placements,
        AuthoritativeMonsterTypeCatalog monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(monsterTypes);
        var placementIds = placements.Definitions
            .Select(definition => definition.PlacementId)
            .ToHashSet();
        foreach (var definition in Definitions) {
            if (!placementIds.Contains(definition.PlacementId))
                throw new InvalidDataException(
                    $"Unique monster pack {definition.PackId} references unknown placement {definition.PlacementId}.");
            if (!monsterTypes.TryGet(definition.MinionMonsterId, out _))
                throw new InvalidDataException(
                    $"Unique monster pack {definition.PackId} references unknown minion monster {definition.MinionMonsterId}.");
        }
    }

    public static AuthoritativeUniqueMonsterPackCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeUniqueMonsterPackCatalog(table.Rows.Select(row =>
            new AuthoritativeUniqueMonsterPackDefinition(
                row.RequiredUInt32("pack_id"),
                row.RequiredUInt32("placement_id"),
                AuthoritativeMonsterIdentity.Resolve(row, stableIds, "minion_monster_id", "minion_monster_symbol"),
                row.RequiredInt32("pack_size"),
                ParseKind(row.Required("pack_kind")))));
    }

    private static AuthoritativeUniqueMonsterPackKind ParseKind(string value)
        => value.Trim().ToLowerInvariant() switch {
            "independent" => AuthoritativeUniqueMonsterPackKind.Independent,
            "leashed" => AuthoritativeUniqueMonsterPackKind.Leashed,
            _ => throw new InvalidDataException(
                $"Unique monster pack kind '{value}' is not supported."),
        };
}
