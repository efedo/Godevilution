using Devilution.Data;

namespace Devilution.Diablo;

/** A native DUN set-piece placed relative to a validated runtime object. */
public sealed record AuthoritativeNativeDunFragmentDefinition(
    uint FragmentId,
    uint LevelId,
    string AssetPath,
    uint AnchorEntityId,
    int AnchorOffsetX,
    int AnchorOffsetY)
{
    /** Stable level symbol retained for composed-pack diagnostics. */
    public string LevelSymbol { get; init; } = string.Empty;

    public AuthoritativeNativeDunFragmentDefinition Validate()
    {
        if (FragmentId == 0
            || AnchorEntityId == 0
            || !IsRelativeDunAssetPath(AssetPath)) {
            throw new InvalidDataException(
                $"Native DUN fragment {FragmentId} contains invalid identity or asset metadata.");
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
}

/** Validated native DUN fragments composed into ordinary runtime levels. */
public sealed class AuthoritativeNativeDunFragmentCatalog
{
    private readonly IReadOnlyList<AuthoritativeNativeDunFragmentDefinition> definitions;

    public AuthoritativeNativeDunFragmentCatalog(
        IEnumerable<AuthoritativeNativeDunFragmentDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions
            .Select(definition => definition.Validate())
            .OrderBy(definition => definition.FragmentId)
            .ToArray();
        if (values.Select(definition => definition.FragmentId).Distinct().Count() != values.Length)
            throw new InvalidDataException(
                "Native DUN fragments must contain unique fragment identities.");
        this.definitions = values;
    }

    public IReadOnlyList<AuthoritativeNativeDunFragmentDefinition> Definitions
        => definitions;

    /** Applies each fragment's tile, transparency, and object layers atomically. */
    public void Apply(
        ComposedContent content,
        AuthoritativeWorld world,
        IEnumerable<AuthoritativeWorldObject> objects)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(objects);
        var objectsById = objects.ToDictionary(@object => @object.EntityId);
        var occupiedMegaCells = new HashSet<(uint LevelId, int X, int Y)>();
        foreach (var definition in definitions) {
            if (!world.ContainsLevel(definition.LevelId))
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} references unknown level {definition.LevelId}.");
            if (!objectsById.TryGetValue(definition.AnchorEntityId, out var anchor))
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} references missing anchor entity {definition.AnchorEntityId}.");
            if (anchor.LevelId != definition.LevelId)
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} anchor entity {definition.AnchorEntityId} is on level {anchor.LevelId}, expected {definition.LevelId}.");

            var level = world.Levels.Single(candidate => candidate.LevelId == definition.LevelId);
            var map = AuthoritativeDunMapAsset.Load(
                definition.AssetPath,
                content.GetRequiredAsset(definition.AssetPath).Contents);
            var originX = checked(anchor.PositionX + definition.AnchorOffsetX);
            var originY = checked(anchor.PositionY + definition.AnchorOffsetY);
            if (originX % 2 != 0 || originY % 2 != 0)
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} must have an even d-piece origin.");
            if (originX < 0
                || originY < 0
                || checked(originX + map.TileWidth) > level.Width
                || checked(originY + map.TileHeight) > level.Height) {
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} exceeds level {definition.LevelId} bounds.");
            }

            var megaWidth = (level.Width + 1) / 2;
            var megaHeight = (level.Height + 1) / 2;
            var megaOriginX = originX / 2;
            var megaOriginY = originY / 2;
            var megaTiles = level.NativeMegaTileLayer.Count == 0
                ? new ushort[checked(megaWidth * megaHeight)]
                : level.NativeMegaTileLayer.ToArray();
            for (var y = 0; y < map.MegaHeight; y++) {
                for (var x = 0; x < map.MegaWidth; x++) {
                    if (!occupiedMegaCells.Add((level.LevelId, megaOriginX + x, megaOriginY + y)))
                        throw new InvalidDataException(
                            $"Native DUN fragment {definition.FragmentId} overlaps another fragment.");
                    megaTiles[(megaOriginY + y) * megaWidth + megaOriginX + x]
                        = map.TileLayer[y * map.MegaWidth + x];
                }
            }

            var transparency = level.NativeTransparency.ToArray();
            if (map.TransparencyLayer.Any(value => value != 0)) {
                if (transparency.Length == 0)
                    transparency = new ushort[checked(level.Width * level.Height)];
                for (var y = 0; y < map.TileHeight; y++) {
                    for (var x = 0; x < map.TileWidth; x++) {
                        var value = map.TransparencyLayer[y * map.TileWidth + x];
                        if (value != 0)
                            transparency[(originY + y) * level.Width + originX + x] = value;
                    }
                }
            }

            var objectLayer = level.NativeObjectLayer.ToArray();
            if (map.ObjectLayer.Any(value => value != 0)) {
                if (objectLayer.Length == 0)
                    objectLayer = new ushort[checked(level.Width * level.Height)];
                for (var y = 0; y < map.TileHeight; y++) {
                    for (var x = 0; x < map.TileWidth; x++) {
                        var value = map.ObjectLayer[y * map.TileWidth + x];
                        if (value != 0)
                            objectLayer[(originY + y) * level.Width + originX + x] = value;
                    }
                }
            }

            world.ReplaceLevel(new AuthoritativeLevel(
                level.LevelId,
                level.Width,
                level.Height,
                level.BlockedCells,
                level.MissileBlockedCells,
                level.IsDefaultStart,
                level.SuppressMultiplayerDeathDrops,
                transparency,
                objectLayer,
                megaTiles) {
                StableSymbol = level.StableSymbol,
            });
        }
    }

    public static AuthoritativeNativeDunFragmentCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeNativeDunFragmentCatalog(table.Rows.Select(row =>
            new AuthoritativeNativeDunFragmentDefinition(
                row.RequiredUInt32("fragment_id"),
                AuthoritativeLevelIdentity.Resolve(row, stableIds),
                row.Required("asset_path"),
                row.RequiredUInt32("anchor_entity_id"),
                row.RequiredInt32("anchor_offset_x"),
                row.RequiredInt32("anchor_offset_y")) {
                LevelSymbol = AuthoritativeLevelIdentity.ReadSymbol(row),
            }));
    }
}
