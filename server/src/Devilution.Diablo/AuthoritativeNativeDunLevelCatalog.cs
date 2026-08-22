using Devilution.Data;

namespace Devilution.Diablo;

/** Explicit native DUN object-layer asset for one ordinary runtime level. */
public sealed record AuthoritativeNativeDunLevelDefinition(
    uint LevelId,
    string AssetPath)
{
    /** Stable ordinary-level symbol retained for composed-pack diagnostics. */
    public string LevelSymbol { get; init; } = string.Empty;

    public AuthoritativeNativeDunLevelDefinition Validate()
    {
        if (!IsRelativeDunAssetPath(AssetPath))
            throw new InvalidDataException(
                $"Native DUN level {LevelId} contains an invalid asset path.");
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
}

/** Validated native DUN object layers for ordinary runtime levels. */
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

    /** Applies each native DUN object's layer without inferring collision behavior. */
    public void Apply(ComposedContent content, AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(world);
        ValidateWorld(world);
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
            world.ReplaceLevel(new AuthoritativeLevel(
                level.LevelId,
                level.Width,
                level.Height,
                level.BlockedCells,
                level.MissileBlockedCells,
                level.IsDefaultStart,
                level.SuppressMultiplayerDeathDrops,
                level.NativeTransparency,
                map.ObjectLayer,
                map.TileLayer,
                map.ReservedLayer) {
                StableSymbol = string.IsNullOrWhiteSpace(definition.LevelSymbol)
                    ? level.StableSymbol
                    : definition.LevelSymbol,
            });
        }
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
                row.Required("asset_path")) {
                LevelSymbol = AuthoritativeLevelIdentity.ReadSymbol(row),
            }));
    }
}
