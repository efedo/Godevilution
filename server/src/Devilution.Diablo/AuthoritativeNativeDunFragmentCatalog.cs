using Devilution.Data;
using Devilution.Domain.Protocol;

namespace Devilution.Diablo;

/** A native DUN set-piece placed relative to a validated runtime anchor. */
public sealed record AuthoritativeNativeDunFragmentDefinition(
    uint FragmentId,
    uint LevelId,
    string AssetPath,
    uint AnchorEntityId,
    int AnchorOffsetX,
    int AnchorOffsetY,
    string? TileAssetPath = null,
    string? SolAssetPath = null,
    string AnchorUniqueIdentity = "",
    int? AnchorPositionX = null,
    int? AnchorPositionY = null,
    int? AnchorSetLevelId = null)
{
    /** Stable level symbol retained for composed-pack diagnostics. */
    public string LevelSymbol { get; init; } = string.Empty;

    /** Optional native set-level identity whose generated origin anchors this fragment. */
    public string AnchorSetLevelSymbol { get; init; } = string.Empty;

    public AuthoritativeNativeDunFragmentDefinition Validate()
    {
        var uniqueIdentity = AnchorUniqueIdentity.Trim();
        var setLevelSymbol = AnchorSetLevelSymbol.Trim();
        var hasUniqueAnchor = uniqueIdentity.Length != 0;
        var hasCoordinateX = AnchorPositionX is not null;
        var hasCoordinateY = AnchorPositionY is not null;
        var hasCoordinateAnchor = hasCoordinateX && hasCoordinateY;
        var hasSetLevelAnchor = AnchorSetLevelId is not null;
        if (FragmentId == 0
            || hasCoordinateX != hasCoordinateY
            || (AnchorEntityId != 0 ? 1 : 0)
                    + (hasUniqueAnchor ? 1 : 0)
                    + (hasCoordinateAnchor ? 1 : 0)
                    + (hasSetLevelAnchor ? 1 : 0) != 1
            || AnchorSetLevelId is < 0 or > 8
            || AnchorPositionX is < 0 or > 512
            || AnchorPositionY is < 0 or > 512
            || !NativeTextLimits.IsIdentityWithinLimit(uniqueIdentity)
            || !NativeTextLimits.IsIdentityWithinLimit(setLevelSymbol)
            || !IsRelativeDunAssetPath(AssetPath)) {
            throw new InvalidDataException(
                $"Native DUN fragment {FragmentId} contains invalid identity or asset metadata.");
        }
        var hasTileAsset = !string.IsNullOrWhiteSpace(TileAssetPath);
        var hasSolAsset = !string.IsNullOrWhiteSpace(SolAssetPath);
        if (hasTileAsset != hasSolAsset
            || hasTileAsset && !IsRelativeNativeAssetPath(TileAssetPath!, ".til")
            || hasSolAsset && !IsRelativeNativeAssetPath(SolAssetPath!, ".sol")) {
            throw new InvalidDataException(
                $"Native DUN fragment {FragmentId} must declare a safe .til/.sol asset pair.");
        }
        return uniqueIdentity == AnchorUniqueIdentity
            && setLevelSymbol == AnchorSetLevelSymbol
            ? this
            : this with { AnchorUniqueIdentity = uniqueIdentity, AnchorSetLevelSymbol = setLevelSymbol };
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

    /** Fails activation when any deferred fragment targets an unknown level. */
    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var definition in definitions) {
            if (!world.ContainsLevel(definition.LevelId))
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} references unknown level {definition.LevelId}.");
        }
    }

    /** Validates optional set-level-origin anchors against the active set-level catalog. */
    public void ValidateSetLevels(AuthoritativeSetLevelNameCatalog setLevels)
    {
        ArgumentNullException.ThrowIfNull(setLevels);
        foreach (var definition in definitions.Where(definition => definition.AnchorSetLevelId is not null)) {
            if (!setLevels.TryGet(definition.AnchorSetLevelId!.Value, out _))
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} references unknown set level {definition.AnchorSetLevelId}.");
        }
    }

    /** Returns a validated view containing only definitions accepted by a lifecycle phase. */
    public AuthoritativeNativeDunFragmentCatalog Filter(
        Func<AuthoritativeNativeDunFragmentDefinition, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new AuthoritativeNativeDunFragmentCatalog(definitions.Where(predicate));
    }

    /** Validates that every deferred fragment asset is present before activation. */
    public void ValidateAssets(
        ComposedContent content,
        AuthoritativeNativeDunGeometryCatalog? externalGeometry = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        foreach (var definition in definitions) {
            RequireAsset(content, definition.FragmentId, definition.AssetPath);
            var map = AuthoritativeDunMapAsset.Load(
                definition.AssetPath,
                content.GetRequiredAsset(definition.AssetPath).Contents);
            if (!string.IsNullOrWhiteSpace(definition.TileAssetPath)) {
                RequireAsset(content, definition.FragmentId, definition.TileAssetPath!);
                RequireAsset(content, definition.FragmentId, definition.SolAssetPath!);
                var pieces = AuthoritativeDunPieceCatalog.LoadTil(
                    definition.TileAssetPath,
                    content.GetRequiredAsset(definition.TileAssetPath).Contents);
                var solAssetPath = definition.SolAssetPath!;
                var tileProperties = AuthoritativeDunTilePropertyCatalog.LoadSol(
                    solAssetPath,
                    content.GetRequiredAsset(solAssetPath).Contents);
                // Validate the complete deferred bundle before an encounter
                // anchor makes the fragment live. Materialization is used only
                // as a cross-asset validator here; Apply performs the actual
                // world projection once the anchor resolves.
                _ = AuthoritativeDunGeometryMaterializer.Materialize(
                    definition.LevelId,
                    map,
                    pieces,
                    tileProperties);
            } else if (externalGeometry is not null) {
                _ = AuthoritativeDunGeometryMaterializer.Materialize(
                    definition.LevelId,
                    map,
                    externalGeometry.Pieces,
                    externalGeometry.TileProperties);
            }
        }
    }

    /** Applies each fragment's native layers and optional collision metadata atomically. */
    public void Apply(
        ComposedContent content,
        AuthoritativeWorld world,
        IEnumerable<AuthoritativeWorldObject> objects,
        IEnumerable<AuthoritativeCombatTarget>? combatTargets = null,
        IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin>? setLevelOrigins = null,
        AuthoritativeNativeDunGeometryCatalog? externalGeometry = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(objects);
        var objectsById = objects.ToDictionary(@object => @object.EntityId);
        var targets = (combatTargets ?? []).ToArray();
        var occupiedMegaCells = new HashSet<(uint LevelId, int X, int Y)>();
        var stagedLevels = new Dictionary<uint, AuthoritativeLevel>();
        foreach (var definition in definitions) {
            if (!world.ContainsLevel(definition.LevelId))
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} references unknown level {definition.LevelId}.");
            var (anchorX, anchorY, anchorLevelId) = ResolveAnchor(
                definition,
                objectsById,
                targets,
                setLevelOrigins);
            if (anchorLevelId != definition.LevelId)
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} anchor is on level {anchorLevelId}, expected {definition.LevelId}.");

            if (!stagedLevels.TryGetValue(definition.LevelId, out var level))
                level = world.Levels.Single(candidate => candidate.LevelId == definition.LevelId);
            if (definition.AnchorPositionX is not null
                && (anchorX < 0 || anchorX >= level.Width || anchorY < 0 || anchorY >= level.Height)) {
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} coordinate anchor exceeds level {definition.LevelId} bounds.");
            }
            var map = AuthoritativeDunMapAsset.Load(
                definition.AssetPath,
                content.GetRequiredAsset(definition.AssetPath).Contents);
            var originX = checked(anchorX + definition.AnchorOffsetX);
            var originY = checked(anchorY + definition.AnchorOffsetY);
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

            var monsterLayer = level.NativeMonsterLayer.ToArray();
            if (map.MonsterLayer.Any(value => value != 0)) {
                if (monsterLayer.Length == 0)
                    monsterLayer = new ushort[checked(level.Width * level.Height)];
                for (var y = 0; y < map.TileHeight; y++) {
                    for (var x = 0; x < map.TileWidth; x++) {
                        var value = map.MonsterLayer[y * map.TileWidth + x];
                        if (value != 0)
                            monsterLayer[(originY + y) * level.Width + originX + x] = value;
                    }
                }
            }

            var nativePieces = level.NativePieceLayer.ToArray();

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

            var blockedCells = level.BlockedCells.ToHashSet();
            var missileBlockedCells = level.MissileBlockedCells.ToHashSet();
            var nativePieceCollisionCells = level.NativePieceCollisionCells.ToHashSet();
            var nativePieceMissileBlockedCells = level.NativePieceMissileBlockedCells.ToHashSet();
            var nativePieceProperties = level.NativePieceProperties.ToDictionary(
                pair => pair.Key,
                pair => pair.Value);
            var hasBinaryGeometry = !string.IsNullOrWhiteSpace(definition.TileAssetPath)
                && !string.IsNullOrWhiteSpace(definition.SolAssetPath);
            if (hasBinaryGeometry || externalGeometry is not null) {
                var pieces = hasBinaryGeometry
                    ? AuthoritativeDunPieceCatalog.LoadTil(
                        definition.TileAssetPath!,
                        content.GetRequiredAsset(definition.TileAssetPath!).Contents)
                    : externalGeometry!.Pieces;
                var tileProperties = hasBinaryGeometry
                    ? AuthoritativeDunTilePropertyCatalog.LoadSol(
                        definition.SolAssetPath!,
                        content.GetRequiredAsset(definition.SolAssetPath!).Contents)
                    : externalGeometry!.TileProperties;
                var fragmentGeometry = AuthoritativeDunGeometryMaterializer.Materialize(
                    definition.LevelId,
                    map,
                    pieces,
                    tileProperties);
                if (nativePieces.Length == 0)
                    nativePieces = new ushort[checked(level.Width * level.Height)];
                for (var y = 0; y < map.TileHeight; y++) {
                    for (var x = 0; x < map.TileWidth; x++) {
                        nativePieces[(originY + y) * level.Width + originX + x]
                            = fragmentGeometry.NativePieceLayer[y * map.TileWidth + x];
                    }
                }
                foreach (var property in tileProperties.Definitions) {
                    var pieceId = checked((ushort)property.PieceId);
                    if (nativePieceProperties.TryGetValue(pieceId, out var existingFlags)
                        && existingFlags != property.Flags) {
                        throw new InvalidDataException(
                            $"Native DUN fragment {definition.FragmentId} redefines d-piece {pieceId} with different collision flags.");
                    }
                    nativePieceProperties[pieceId] = property.Flags;
                }
                foreach (var cell in fragmentGeometry.BlockedCells) {
                    var localX = cell % map.TileWidth;
                    var localY = cell / map.TileWidth;
                    var worldCell = (originY + localY) * level.Width + originX + localX;
                    blockedCells.Add(worldCell);
                    nativePieceCollisionCells.Add(worldCell);
                }
                foreach (var cell in fragmentGeometry.MissileBlockedCells) {
                    var localX = cell % map.TileWidth;
                    var localY = cell / map.TileWidth;
                    var worldCell = (originY + localY) * level.Width + originX + localX;
                    missileBlockedCells.Add(worldCell);
                    nativePieceMissileBlockedCells.Add(worldCell);
                }
            }

            stagedLevels[definition.LevelId] = new AuthoritativeLevel(
                level.LevelId,
                level.Width,
                level.Height,
                blockedCells,
                missileBlockedCells,
                level.IsDefaultStart,
                level.SuppressMultiplayerDeathDrops,
                transparency,
                objectLayer,
                megaTiles,
                nativePieces,
                nativePieceProperties,
                nativePieceCollisionCells,
                nativePieceMissileBlockedCells,
                monsterLayer,
                level.StartPositionX,
                level.StartPositionY) {
                StableSymbol = level.StableSymbol,
            };
        }
        foreach (var level in stagedLevels.Values.OrderBy(level => level.LevelId))
            world.ReplaceLevel(level);
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
                row.OptionalUInt32("anchor_entity_id"),
                row.RequiredInt32("anchor_offset_x"),
                row.RequiredInt32("anchor_offset_y"),
                OptionalAssetPath(row, "tile_asset_path"),
                OptionalAssetPath(row, "sol_asset_path"),
                row.TryGet("anchor_unique_identity", out var anchorUniqueIdentity)
                    ? anchorUniqueIdentity
                    : string.Empty,
                OptionalCoordinate(row, "anchor_position_x"),
                OptionalCoordinate(row, "anchor_position_y"),
                OptionalSetLevelId(row, stableIds)) {
                LevelSymbol = AuthoritativeLevelIdentity.ReadSymbol(row),
                AnchorSetLevelSymbol = row.TryGet("anchor_set_level_symbol", out var symbol)
                    ? symbol
                    : string.Empty,
            }));
    }

    private static (int X, int Y, uint LevelId) ResolveAnchor(
        AuthoritativeNativeDunFragmentDefinition definition,
        IReadOnlyDictionary<uint, AuthoritativeWorldObject> objects,
        IReadOnlyList<AuthoritativeCombatTarget> combatTargets,
        IReadOnlyDictionary<int, AuthoritativeSetLevelObjectOrigin>? setLevelOrigins)
    {
        if (definition.AnchorSetLevelId is { } anchorSetLevelId) {
            if (setLevelOrigins is null
                || !setLevelOrigins.TryGetValue(anchorSetLevelId, out var origin)) {
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} references unpublished set-level origin {anchorSetLevelId}.");
            }
            return (origin.PositionX, origin.PositionY, origin.LevelId);
        }
        if (definition.AnchorPositionX is { } positionX
            && definition.AnchorPositionY is { } positionY) {
            return (positionX, positionY, definition.LevelId);
        }

        if (definition.AnchorEntityId != 0) {
            if (!objects.TryGetValue(definition.AnchorEntityId, out var objectAnchor))
                throw new InvalidDataException(
                    $"Native DUN fragment {definition.FragmentId} references missing anchor entity {definition.AnchorEntityId}.");
            return (objectAnchor.PositionX, objectAnchor.PositionY, objectAnchor.LevelId);
        }

        var matches = combatTargets
            .Where(target => target.IsUnique
                && string.Equals(
                    target.UniqueIdentity,
                    definition.AnchorUniqueIdentity,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException(
                $"Native DUN fragment {definition.FragmentId} unique anchor '{definition.AnchorUniqueIdentity}' "
                + $"must match exactly one encounter; found {matches.Length}.");
        return (matches[0].PositionX, matches[0].PositionY, matches[0].LevelId);
    }

    private static string? OptionalAssetPath(TsvRow row, string column)
    {
        return row.TryGet(column, out var value)
            && !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
            ? value
            : null;
    }

    private static int? OptionalCoordinate(TsvRow row, string column)
    {
        return row.TryGet(column, out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? row.OptionalInt32(column)
            : null;
    }

    private static int? OptionalSetLevelId(
        TsvRow row,
        StableContentIdCatalog? stableIds)
    {
        var numericPresent = row.TryGet("anchor_set_level_id", out var numericText)
            && !string.IsNullOrWhiteSpace(numericText);
        var symbolPresent = row.TryGet("anchor_set_level_symbol", out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            && !string.Equals(symbol, "none", StringComparison.OrdinalIgnoreCase);
        if (!numericPresent && !symbolPresent)
            return null;
        if (stableIds is null && !numericPresent)
            throw new InvalidDataException(
                $"Native DUN fragment row {row.LineNumber} requires stable IDs to resolve set-level anchor '{symbol}'.");
        return AuthoritativeSetLevelIdentity.ResolveOptional(
            row,
            stableIds,
            "anchor_set_level_id",
            "anchor_set_level_symbol");
    }

    private static void RequireAsset(
        ComposedContent content,
        uint fragmentId,
        string assetPath)
    {
        if (!content.TryGetAsset(assetPath, out _))
            throw new InvalidDataException(
                $"Native DUN fragment {fragmentId} references missing asset '{assetPath}'.");
    }
}
