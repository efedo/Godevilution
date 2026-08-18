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

    /** Optional native transparency `.dun` layer loaded with the active map. */
    public string TransparencyAssetPath { get; init; } = string.Empty;

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
            || !IsOptionalAssetPath(TransparencyAssetPath, ".dun")
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
    /** Reserved level-ID range for materialized native set-level variants. */
    public const uint RuntimeLevelIdBase = 0x8000_0000;

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

    /** Returns the deterministic runtime level identity for one map variant. */
    public static uint GetRuntimeLevelId(int setLevelId, bool activeMap)
    {
        if (setLevelId is < 0 or > 8)
            throw new ArgumentOutOfRangeException(nameof(setLevelId));
        return checked(RuntimeLevelIdBase + (uint)(setLevelId * 2) + (activeMap ? 1U : 0U));
    }

    /** Returns whether a runtime level is one of DevilutionX's active arenas. */
    public static bool IsArenaRuntimeLevel(uint levelId)
    {
        for (var setLevelId = 6; setLevelId <= 8; setLevelId++) {
            if (levelId == GetRuntimeLevelId(setLevelId, activeMap: true))
                return true;
        }
        return false;
    }

    /**
     * Materializes every complete native bundle supplied by the active pack.
     * Legacy packs may declare paths before shipping the binary files; those
     * rows remain metadata-only until all required assets are present.
     */
    public IReadOnlyList<AuthoritativeLevel> LoadAvailableGeometry(ComposedContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var levels = new List<AuthoritativeLevel>();
        foreach (var definition in definitions) {
            if (string.IsNullOrWhiteSpace(definition.TileAssetPath)
                || string.IsNullOrWhiteSpace(definition.SolAssetPath)) {
                continue;
            }
            var paths = new[] {
                definition.PreMapAssetPath,
                definition.ActiveMapAssetPath,
                definition.TileAssetPath,
                definition.SolAssetPath,
            };
            if (!string.IsNullOrWhiteSpace(definition.TransparencyAssetPath))
                paths = [.. paths, definition.TransparencyAssetPath];
            var present = paths.Count(path => content.TryGetAsset(path, out _));
            if (present == 0)
                continue;
            if (present != paths.Length) {
                throw new InvalidDataException(
                    $"Set-level map {definition.SetLevelId} supplies an incomplete native asset bundle.");
            }
            var preMap = AuthoritativeNativeDunGeometryLoader.Load(
                content,
                definition,
                GetRuntimeLevelId(definition.SetLevelId, activeMap: false),
                activeMap: false);
            var activeMap = AuthoritativeNativeDunGeometryLoader.Load(
                content,
                definition,
                GetRuntimeLevelId(definition.SetLevelId, activeMap: true),
                activeMap: true);
            if (!activeMap.ContainsPosition(definition.SpawnX, definition.SpawnY)
                || !activeMap.IsWalkable(definition.SpawnX, definition.SpawnY)) {
                throw new InvalidDataException(
                    $"Set-level map {definition.SetLevelId} contains an invalid active-map spawn point.");
            }
            if (!string.IsNullOrWhiteSpace(definition.TransparencyAssetPath)) {
                var transparency = AuthoritativeDunMapAsset.Load(
                    definition.TransparencyAssetPath,
                    content.GetRequiredAsset(definition.TransparencyAssetPath).Contents);
                if (transparency.TileWidth != activeMap.Width
                    || transparency.TileHeight != activeMap.Height) {
                    throw new InvalidDataException(
                        $"Set-level map {definition.SetLevelId} transparency dimensions do not match the active map.");
                }
                activeMap = new AuthoritativeLevel(
                    activeMap.LevelId,
                    activeMap.Width,
                    activeMap.Height,
                    activeMap.BlockedCells,
                    activeMap.MissileBlockedCells,
                    activeMap.IsDefaultStart,
                    activeMap.SuppressMultiplayerDeathDrops,
                    transparency.TransparencyLayer) {
                    StableSymbol = activeMap.StableSymbol,
                };
            }
            levels.Add(preMap);
            levels.Add(activeMap);
        }
        return levels;
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
                TransparencyAssetPath = row.TryGet("transparency_asset", out var transparencyAsset)
                    ? transparencyAsset
                    : string.Empty,
            }));
    }
}
