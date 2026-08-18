using Devilution.Data;

namespace Devilution.Diablo;

/** One native fixed-object placement on a set-level map. */
public sealed record AuthoritativeSetLevelObjectPlacement(
    uint PlacementId,
    int SetLevelId,
    uint ObjectId,
    int PositionX,
    int PositionY,
    int TemplateX,
    int TemplateY,
    int TemplateWidth,
    int TemplateHeight)
{
    public string SetLevelSymbol { get; init; } = string.Empty;
}

/** Validated external placements for native set-level object footprints. */
public sealed class AuthoritativeSetLevelObjectCatalog
{
    private readonly IReadOnlyList<AuthoritativeSetLevelObjectPlacement> definitions;

    public AuthoritativeSetLevelObjectCatalog(
        IEnumerable<AuthoritativeSetLevelObjectPlacement> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(placement =>
                placement.PlacementId == 0
                || placement.SetLevelId is < 0 or > 8
                || placement.ObjectId == 0
                || placement.PositionX is < 0 or > 255
                || placement.PositionY is < 0 or > 255
                || placement.TemplateX is < 0 or > 255
                || placement.TemplateY is < 0 or > 255
                || placement.TemplateWidth is <= 0 or > 64
                || placement.TemplateHeight is <= 0 or > 64)
            || values.Select(placement => placement.PlacementId).Distinct().Count() != values.Length
            || values.GroupBy(placement => (
                    placement.SetLevelId,
                    placement.ObjectId,
                    placement.PositionX,
                    placement.PositionY))
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Set-level object placements contain invalid or duplicate values.");
        }
        this.definitions = values
            .OrderBy(placement => placement.SetLevelId)
            .ThenBy(placement => placement.PlacementId)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeSetLevelObjectPlacement> Definitions
        => definitions;

    public IReadOnlyList<AuthoritativeSetLevelObjectPlacement> GetForSetLevel(int setLevelId)
        => definitions.Where(placement => placement.SetLevelId == setLevelId).ToArray();

    public void ValidateSetLevels(AuthoritativeSetLevelNameCatalog setLevels)
    {
        ArgumentNullException.ThrowIfNull(setLevels);
        foreach (var placement in definitions) {
            if (!setLevels.TryGet(placement.SetLevelId, out _))
                throw new InvalidDataException(
                    $"Set-level object placement {placement.PlacementId} references unknown set level {placement.SetLevelId}.");
        }
    }

    public static AuthoritativeSetLevelObjectCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeSetLevelObjectCatalog(table.Rows.Select(row =>
            new AuthoritativeSetLevelObjectPlacement(
                row.RequiredUInt32("placement_id"),
                AuthoritativeSetLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "set_level_id",
                    "set_level_symbol"),
                row.RequiredUInt32("object_id"),
                row.RequiredInt32("position_x"),
                row.RequiredInt32("position_y"),
                row.RequiredInt32("template_x"),
                row.RequiredInt32("template_y"),
                row.RequiredInt32("template_width"),
                row.RequiredInt32("template_height")) {
                SetLevelSymbol = row.TryGet("set_level_symbol", out var symbol)
                    ? symbol
                    : string.Empty,
            }));
    }
}
