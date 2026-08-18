using Devilution.Data;

namespace Devilution.Diablo;

[Flags]
public enum AuthoritativeDunTileFlags : byte
{
    None = 0,
    Solid = 1,
    BlockMissile = 2,
}

/** Native MIN expansion from one .dun mega-tile ID to four d-piece IDs. */
public sealed record AuthoritativeDunPieceDefinition(
    uint MegaTileId,
    uint NorthWestPieceId,
    uint NorthEastPieceId,
    uint SouthWestPieceId,
    uint SouthEastPieceId)
{
    public AuthoritativeDunPieceDefinition Validate()
    {
        if (MegaTileId > ushort.MaxValue
            || NorthWestPieceId > ushort.MaxValue
            || NorthEastPieceId > ushort.MaxValue
            || SouthWestPieceId > ushort.MaxValue
            || SouthEastPieceId > ushort.MaxValue) {
            throw new InvalidDataException(
                $"Native mega-tile {MegaTileId} references a d-piece outside the 16-bit range.");
        }
        return this;
    }
}

/** Validated native MIN expansion definitions. */
public sealed class AuthoritativeDunPieceCatalog
{
    private readonly IReadOnlyDictionary<uint, AuthoritativeDunPieceDefinition> definitions;

    public AuthoritativeDunPieceCatalog(
        IEnumerable<AuthoritativeDunPieceDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.Select(definition => definition.Validate()).ToArray();
        if (values.GroupBy(definition => definition.MegaTileId).Any(group => group.Count() != 1))
            throw new InvalidDataException("Native mega-tile definitions contain duplicate IDs.");
        this.definitions = values.ToDictionary(definition => definition.MegaTileId);
    }

    public IReadOnlyList<AuthoritativeDunPieceDefinition> Definitions
        => definitions.Values.OrderBy(definition => definition.MegaTileId).ToArray();

    public bool TryGet(uint megaTileId, out AuthoritativeDunPieceDefinition definition)
        => definitions.TryGetValue(megaTileId, out definition!);

    public static AuthoritativeDunPieceCatalog LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeDunPieceCatalog(table.Rows.Select(row =>
            new AuthoritativeDunPieceDefinition(
                row.RequiredUInt32("mega_tile_id"),
                row.RequiredUInt32("piece_north_west"),
                row.RequiredUInt32("piece_north_east"),
                row.RequiredUInt32("piece_south_west"),
                row.RequiredUInt32("piece_south_east"))));
    }
}

/** Validated .sol collision properties indexed by native d-piece ID. */
public sealed record AuthoritativeDunTileProperty(
    uint PieceId,
    AuthoritativeDunTileFlags Flags)
{
    public AuthoritativeDunTileProperty Validate()
    {
        if (PieceId > ushort.MaxValue || (Flags & ~(AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile)) != 0)
            throw new InvalidDataException($"Native d-piece {PieceId} has invalid collision properties.");
        return this;
    }
}

/** Validated native collision properties for d-piece IDs. */
public sealed class AuthoritativeDunTilePropertyCatalog
{
    private readonly IReadOnlyDictionary<uint, AuthoritativeDunTileFlags> properties;

    public AuthoritativeDunTilePropertyCatalog(
        IEnumerable<AuthoritativeDunTileProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        var values = properties.Select(property => property.Validate()).ToArray();
        if (values.GroupBy(property => property.PieceId).Any(group => group.Count() != 1))
            throw new InvalidDataException("Native d-piece properties contain duplicate IDs.");
        this.properties = values.ToDictionary(property => property.PieceId, property => property.Flags);
    }

    public IReadOnlyList<AuthoritativeDunTileProperty> Definitions
        => properties.OrderBy(property => property.Key)
            .Select(property => new AuthoritativeDunTileProperty(property.Key, property.Value))
            .ToArray();

    public bool TryGet(uint pieceId, out AuthoritativeDunTileFlags flags)
        => properties.TryGetValue(pieceId, out flags);

    public static AuthoritativeDunTilePropertyCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeDunTilePropertyCatalog(table.Rows.Select(row =>
            new AuthoritativeDunTileProperty(
                row.RequiredUInt32("piece_id"),
                (row.RequiredBoolean("solid") ? AuthoritativeDunTileFlags.Solid : AuthoritativeDunTileFlags.None)
                | (row.RequiredBoolean("block_missile")
                    ? AuthoritativeDunTileFlags.BlockMissile
                    : AuthoritativeDunTileFlags.None))));
    }
}

/** Expands native .dun/MIN/.sol data into authoritative movement geometry. */
public static class AuthoritativeDunGeometryMaterializer
{
    public static AuthoritativeLevel Materialize(
        uint levelId,
        AuthoritativeDunMapAsset asset,
        AuthoritativeDunPieceCatalog pieces,
        AuthoritativeDunTilePropertyCatalog tileProperties,
        string stableSymbol = "",
        bool suppressMultiplayerDeathDrops = false)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(tileProperties);
        var blocked = new List<int>();
        var missileBlocked = new List<int>();
        for (var megaY = 0; megaY < asset.MegaHeight; megaY++) {
            for (var megaX = 0; megaX < asset.MegaWidth; megaX++) {
                var megaTileId = asset.TileLayer[megaY * asset.MegaWidth + megaX];
                if (!pieces.TryGet(megaTileId, out var piece))
                    throw new InvalidDataException(
                        $"Native .dun asset '{asset.SourcePath}' references unknown mega-tile {megaTileId}.");
                AddPiece(piece.NorthWestPieceId, megaX * 2, megaY * 2);
                AddPiece(piece.NorthEastPieceId, megaX * 2 + 1, megaY * 2);
                AddPiece(piece.SouthWestPieceId, megaX * 2, megaY * 2 + 1);
                AddPiece(piece.SouthEastPieceId, megaX * 2 + 1, megaY * 2 + 1);
            }
        }
        return new AuthoritativeLevel(
            levelId,
            asset.TileWidth,
            asset.TileHeight,
            blocked,
            missileBlocked,
            suppressMultiplayerDeathDrops: suppressMultiplayerDeathDrops) {
            StableSymbol = stableSymbol,
        };

        void AddPiece(uint pieceId, int positionX, int positionY)
        {
            if (!tileProperties.TryGet(pieceId, out var flags))
                throw new InvalidDataException(
                    $"Native .dun asset '{asset.SourcePath}' references d-piece {pieceId} without .sol properties.");
            var cell = positionY * asset.TileWidth + positionX;
            if (flags.HasFlag(AuthoritativeDunTileFlags.Solid))
                blocked.Add(cell);
            if (flags.HasFlag(AuthoritativeDunTileFlags.BlockMissile))
                missileBlocked.Add(cell);
        }
    }
}
