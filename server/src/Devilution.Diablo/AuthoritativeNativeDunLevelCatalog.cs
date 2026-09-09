using Devilution.Data;

namespace Devilution.Diablo;

/** Explicit native DUN asset bundle for one ordinary runtime level. */
public sealed record AuthoritativeNativeDunLevelDefinition(
    uint LevelId,
    string AssetPath,
    string? TileAssetPath = null,
    string? SolAssetPath = null)
{
    /** Stable ordinary-level symbol retained for composed-pack diagnostics. */
    public string LevelSymbol { get; init; } = string.Empty;

    public AuthoritativeNativeDunLevelDefinition Validate()
    {
        if (!IsRelativeDunAssetPath(AssetPath))
            throw new InvalidDataException(
                $"Native DUN level {LevelId} contains an invalid asset path.");
        var hasTileAsset = !string.IsNullOrWhiteSpace(TileAssetPath);
        var hasSolAsset = !string.IsNullOrWhiteSpace(SolAssetPath);
        if (hasTileAsset != hasSolAsset
            || hasTileAsset && !IsRelativeNativeAssetPath(TileAssetPath!, ".til")
            || hasSolAsset && !IsRelativeNativeAssetPath(SolAssetPath!, ".sol")) {
            throw new InvalidDataException(
                $"Native DUN level {LevelId} must declare a safe .til/.sol asset pair.");
        }
        return this;
    }

    private static bool IsRelativeDunAssetPath(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && !Path.IsPathRooted(path)
            && path.EndsWith(".dun", StringComparison.OrdinalIgnoreCase)
            && path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
                .All(segment => segment is not "." and not "..")
            && !path.Contains(':', StringComparison.Ordinal);
    }

    private static bool IsRelativeNativeAssetPath(string path, string extension)
    {
        return !string.IsNullOrWhiteSpace(path)
            && !Path.IsPathRooted(path)
            && path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            && path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
                .All(segment => segment is not "." and not "..")
            && !path.Contains(':', StringComparison.Ordinal);
    }
}

/** Validated native DUN layers for ordinary runtime levels. */
public sealed class AuthoritativeNativeDunLevelCatalog
{
    private readonly IReadOnlyList<AuthoritativeNativeDunLevelDefinition> definitions;

    public AuthoritativeNativeDunLevelCatalog(
        IEnumerable<AuthoritativeNativeDunLevelDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions
            .Select(definition => definition.Validate())
            .OrderBy(definition => definition.LevelId)
            .ToArray();
        if (values.Select(definition => definition.LevelId).Distinct().Count() != values.Length) {
            throw new InvalidDataException(
                "Native DUN level definitions must contain unique level identities.");
        }
        this.definitions = values;
    }

    public IReadOnlyList<AuthoritativeNativeDunLevelDefinition> Definitions
        => definitions;

    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var definition in definitions) {
            if (!world.ContainsLevel(definition.LevelId))
                throw new InvalidDataException(
                    $"Native DUN level definition {definition.LevelId} references an unknown level.");
        }
    }

    /** Validates every deferred DUN and optional collision asset before activation. */
    public void ValidateAssets(ComposedContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        foreach (var definition in definitions) {
            RequireAsset(definition.LevelId, definition.AssetPath, content);
            var map = AuthoritativeDunMapAsset.Load(
                definition.AssetPath,
                content.GetRequiredAsset(definition.AssetPath).Contents);
            if (!string.IsNullOrWhiteSpace(definition.TileAssetPath)) {
                RequireAsset(definition.LevelId, definition.TileAssetPath!, content);
                RequireAsset(definition.LevelId, definition.SolAssetPath!, content);
                var pieces = AuthoritativeDunPieceCatalog.LoadTil(
                    definition.TileAssetPath,
                    content.GetRequiredAsset(definition.TileAssetPath).Contents);
                var solAssetPath = definition.SolAssetPath!;
                var tileProperties = AuthoritativeDunTilePropertyCatalog.LoadSol(
                    solAssetPath,
                    content.GetRequiredAsset(solAssetPath).Contents);
                // Validate the complete level bundle before activation. Materialization
                // is used only as a cross-asset validator here; Apply performs the
                // actual world projection once the level is composed.
                _ = AuthoritativeDunGeometryMaterializer.Materialize(
                    definition.LevelId,
                    map,
                    pieces,
                    tileProperties);
            }
        }
    }

    /** Applies each native DUN object's layer and optional external collision geometry. */
    public void Apply(ComposedContent content, AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(world);
        ValidateWorld(world);
        var replacements = new List<AuthoritativeLevel>(definitions.Count);
        foreach (var definition in definitions) {
            var level = world.Levels.Single(candidate => candidate.LevelId == definition.LevelId);
            var map = AuthoritativeDunMapAsset.Load(
                definition.AssetPath,
                content.GetRequiredAsset(definition.AssetPath).Contents);
            if (map.TileWidth != level.Width || map.TileHeight != level.Height) {
                throw new InvalidDataException(
                    $"Native DUN level {definition.LevelId} dimensions {map.TileWidth}x{map.TileHeight} "
                    + $"do not match level {level.Width}x{level.Height}.");
            }
            var nativeGeometry = LoadOptionalGeometry(content, definition, map);
            replacements.Add(new AuthoritativeLevel(
                level.LevelId,
                level.Width,
                level.Height,
                nativeGeometry?.BlockedCells ?? level.BlockedCells,
                nativeGeometry?.MissileBlockedCells ?? level.MissileBlockedCells,
                level.IsDefaultStart,
                level.SuppressMultiplayerDeathDrops,
                level.NativeTransparency,
                map.ObjectLayer,
                map.TileLayer,
                nativeGeometry?.NativePieceLayer ?? level.NativePieceLayer,
                nativeGeometry?.NativePieceProperties ?? level.NativePieceProperties,
                nativeGeometry?.NativePieceCollisionCells ?? level.NativePieceCollisionCells,
                nativeGeometry?.NativePieceMissileBlockedCells ?? level.NativePieceMissileBlockedCells,
                map.MonsterLayer,
                level.StartPositionX,
                level.StartPositionY) {
                StableSymbol = string.IsNullOrWhiteSpace(definition.LevelSymbol)
                    ? level.StableSymbol
                    : definition.LevelSymbol,
            });
        }
        foreach (var replacement in replacements)
            world.ReplaceLevel(replacement);
    }

    private static AuthoritativeLevel? LoadOptionalGeometry(
        ComposedContent content,
        AuthoritativeNativeDunLevelDefinition definition,
        AuthoritativeDunMapAsset map)
    {
        if (string.IsNullOrWhiteSpace(definition.TileAssetPath)
            || string.IsNullOrWhiteSpace(definition.SolAssetPath))
            return null;
        var pieces = AuthoritativeDunPieceCatalog.LoadTil(
            definition.TileAssetPath,
            content.GetRequiredAsset(definition.TileAssetPath).Contents);
        var tileProperties = AuthoritativeDunTilePropertyCatalog.LoadSol(
            definition.SolAssetPath,
            content.GetRequiredAsset(definition.SolAssetPath).Contents);
        return AuthoritativeDunGeometryMaterializer.Materialize(
            definition.LevelId,
            map,
            pieces,
            tileProperties);
    }

    public static AuthoritativeNativeDunLevelCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeNativeDunLevelCatalog(table.Rows.Select(row =>
            new AuthoritativeNativeDunLevelDefinition(
                AuthoritativeLevelIdentity.Resolve(row, stableIds),
                row.Required("asset_path"),
                OptionalAssetPath(row, "tile_asset_path"),
                OptionalAssetPath(row, "sol_asset_path")) {
                LevelSymbol = AuthoritativeLevelIdentity.ReadSymbol(row),
            }));
    }

    private static string? OptionalAssetPath(TsvRow row, string column)
    {
        return row.TryGet(column, out var value)
            && !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
            ? value
            : null;
    }

    private static void RequireAsset(
        uint levelId,
        string assetPath,
        ComposedContent content)
    {
        if (!content.TryGetAsset(assetPath, out _))
            throw new InvalidDataException(
                $"Native DUN level {levelId} references missing asset '{assetPath}'.");
    }
}
