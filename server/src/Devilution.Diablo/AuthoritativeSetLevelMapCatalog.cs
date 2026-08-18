using Devilution.Data;

namespace Devilution.Diablo;

/** Native set-level map assets and entry point supplied by server content. */
public sealed record AuthoritativeSetLevelMapDefinition(
    int SetLevelId,
    string PreMapAssetPath,
    string ActiveMapAssetPath,
    string DungeonType,
    int SpawnX,
    int SpawnY)
{
    /** Stable set-level symbol retained for composed-pack diagnostics. */
    public string SetLevelSymbol { get; init; } = string.Empty;

    /** Optional native dungeon-piece mapping shared by both map variants. */
    public string TileAssetPath { get; init; } = string.Empty;

    /** Optional native d-piece property table shared by both map variants. */
    public string SolAssetPath { get; init; } = string.Empty;

    public AuthoritativeSetLevelMapDefinition Validate()
    {
        if (SetLevelId is < 0 or > 8
            || string.IsNullOrWhiteSpace(PreMapAssetPath)
            || string.IsNullOrWhiteSpace(ActiveMapAssetPath)
            || string.IsNullOrWhiteSpace(DungeonType)
            || SpawnX is < 0 or > 255
            || SpawnY is < 0 or > 255
            || !IsRelativeDunAssetPath(PreMapAssetPath)
            || !IsRelativeDunAssetPath(ActiveMapAssetPath)
            || !IsOptionalAssetPath(TileAssetPath, ".til")
            || !IsOptionalAssetPath(SolAssetPath, ".sol")
            || DungeonType is not ("Cathedral" or "Catacombs" or "Caves" or "Hell" or "Nest" or "Crypt")) {
            throw new InvalidDataException(
                $"Set-level map definition {SetLevelId} contains invalid asset, dungeon, or spawn data.");
        }
        return this;
    }

    private static bool IsRelativeDunAssetPath(string path)
        => IsRelativeAssetPath(path, ".dun");

    private static bool IsOptionalAssetPath(string path, string extension)
        => string.IsNullOrWhiteSpace(path) || IsRelativeAssetPath(path, extension);

    private static bool IsRelativeAssetPath(string path, string extension)
    {
        return !Path.IsPathRooted(path)
            && path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            && path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
                .All(segment => segment is not "." and not "..")
            && !path.Contains(':', StringComparison.Ordinal);
    }
}

/** Validated native set-level map asset selection and entry points. */
public sealed class AuthoritativeSetLevelMapCatalog
{
    private readonly IReadOnlyList<AuthoritativeSetLevelMapDefinition> definitions;

    public AuthoritativeSetLevelMapCatalog(
        IEnumerable<AuthoritativeSetLevelMapDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions
            .Select(definition => definition.Validate())
            .OrderBy(definition => definition.SetLevelId)
            .ToArray();
        if (values.Length == 0
            || values.Select(definition => definition.SetLevelId).Distinct().Count() != values.Length) {
            throw new InvalidDataException(
                "Set-level map definitions must contain at least one unique identity.");
        }
        this.definitions = values;
    }

    public IReadOnlyList<AuthoritativeSetLevelMapDefinition> Definitions
        => definitions;

    public bool TryGet(int setLevelId, out AuthoritativeSetLevelMapDefinition definition)
    {
        definition = definitions.SingleOrDefault(candidate => candidate.SetLevelId == setLevelId)!;
        return definition is not null;
    }

    public void ValidateSetLevels(AuthoritativeSetLevelNameCatalog setLevels)
    {
        ArgumentNullException.ThrowIfNull(setLevels);
        foreach (var definition in definitions) {
            if (!setLevels.TryGet(definition.SetLevelId, out _))
                throw new InvalidDataException(
                    $"Set-level map definition {definition.SetLevelId} references an unknown set level.");
        }
    }

    public static AuthoritativeSetLevelMapCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeSetLevelMapCatalog(table.Rows.Select(row =>
            new AuthoritativeSetLevelMapDefinition(
                AuthoritativeSetLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "set_level_id",
                    "set_level_symbol"),
                row.Required("pre_map_asset"),
                row.Required("active_map_asset"),
                row.Required("dungeon_type"),
                row.RequiredInt32("spawn_x"),
                row.RequiredInt32("spawn_y")) {
                SetLevelSymbol = row.TryGet("set_level_symbol", out var symbol)
                    ? symbol
                    : string.Empty,
                TileAssetPath = row.TryGet("til_asset", out var tilAsset)
                    ? tilAsset
                    : string.Empty,
                SolAssetPath = row.TryGet("sol_asset", out var solAsset)
                    ? solAsset
                    : string.Empty,
            }));
    }
}
