using Devilution.Data;

namespace Devilution.Diablo;

[Flags]
public enum AuthoritativeDunTileFlags : byte
{
    None = 0,
    Solid = 1,
    BlockLight = 2,
    BlockMissile = 4,
    Transparent = 8,
    TransparentLeft = 16,
    TransparentRight = 32,
    Trap = 128,
}

/** Native .til expansion from one .dun mega-tile ID to four d-piece IDs. */
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
            || NorthWestPieceId >= AuthoritativeDunTilePropertyCatalog.NativeTileCount
            || NorthEastPieceId >= AuthoritativeDunTilePropertyCatalog.NativeTileCount
            || SouthWestPieceId >= AuthoritativeDunTilePropertyCatalog.NativeTileCount
            || SouthEastPieceId >= AuthoritativeDunTilePropertyCatalog.NativeTileCount) {
            throw new InvalidDataException(
                $"Native mega-tile {MegaTileId} references a d-piece outside the native range (0-{AuthoritativeDunTilePropertyCatalog.NativeTileCount - 1}).");
        }
        return this;
    }
}

/** Validated native .til expansion definitions. */
public sealed class AuthoritativeDunPieceCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "mega_tile_id",
        "piece_north_west",
        "piece_north_east",
        "piece_south_west",
        "piece_south_east",
    ];

    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production native-DUN piece table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.RequiredUInt32("mega_tile_id");
            _ = row.RequiredUInt32("piece_north_west");
            _ = row.RequiredUInt32("piece_north_east");
            _ = row.RequiredUInt32("piece_south_west");
            _ = row.RequiredUInt32("piece_south_east");
        }
    }

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

    /** Loads the native little-endian .til records used by DevilutionX. */
    public static AuthoritativeDunPieceCatalog LoadTil(
        string sourcePath,
        ReadOnlyMemory<byte> contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var bytes = contents.Span;
        const int bytesPerMegaTile = sizeof(ushort) * 4;
        if (bytes.Length == 0 || bytes.Length % bytesPerMegaTile != 0)
            throw new InvalidDataException(
                $"Native .til asset '{sourcePath}' has an invalid length.");

        var definitions = new AuthoritativeDunPieceDefinition[bytes.Length / bytesPerMegaTile];
        for (var megaTileId = 0; megaTileId < definitions.Length; megaTileId++) {
            var offset = checked(megaTileId * bytesPerMegaTile);
            definitions[megaTileId] = new AuthoritativeDunPieceDefinition(
                (uint)megaTileId,
                ReadWord(bytes, offset),
                ReadWord(bytes, offset + sizeof(ushort)),
                ReadWord(bytes, offset + sizeof(ushort) * 2),
                ReadWord(bytes, offset + sizeof(ushort) * 3));
        }
        return new AuthoritativeDunPieceCatalog(definitions);
    }

    private static ushort ReadWord(ReadOnlySpan<byte> bytes, int offset)
        => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.Slice(offset, sizeof(ushort)));
}

/** Validated .sol properties indexed by native d-piece ID. */
public sealed record AuthoritativeDunTileProperty(
    uint PieceId,
    AuthoritativeDunTileFlags Flags)
{
    public AuthoritativeDunTileProperty Validate()
    {
        const AuthoritativeDunTileFlags supportedFlags =
            AuthoritativeDunTileFlags.Solid
            | AuthoritativeDunTileFlags.BlockLight
            | AuthoritativeDunTileFlags.BlockMissile
            | AuthoritativeDunTileFlags.Transparent
            | AuthoritativeDunTileFlags.TransparentLeft
            | AuthoritativeDunTileFlags.TransparentRight
            | AuthoritativeDunTileFlags.Trap;
        if (PieceId >= AuthoritativeDunTilePropertyCatalog.NativeTileCount
            || (Flags & ~supportedFlags) != 0)
            throw new InvalidDataException($"Native d-piece {PieceId} has invalid tile properties.");
        return this;
    }
}

/** Validated native collision properties for d-piece IDs. */
public sealed class AuthoritativeDunTilePropertyCatalog
{
    public const int NativeTileCount = 1379;

    private static readonly IReadOnlyList<string> ProductionColumns = [
        "piece_id",
        "solid",
        "block_light",
        "block_missile",
        "transparent",
        "transparent_left",
        "transparent_right",
        "trap",
    ];

    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production native-DUN tile-properties table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.RequiredUInt32("piece_id");
            _ = row.RequiredBoolean("solid");
            _ = row.RequiredBoolean("block_missile");
        }
    }

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
                ReadFlag(row, "solid", AuthoritativeDunTileFlags.Solid)
                | ReadFlag(row, "block_light", AuthoritativeDunTileFlags.BlockLight)
                | ReadFlag(row, "block_missile", AuthoritativeDunTileFlags.BlockMissile)
                | ReadFlag(row, "transparent", AuthoritativeDunTileFlags.Transparent)
                | ReadFlag(row, "transparent_left", AuthoritativeDunTileFlags.TransparentLeft)
                | ReadFlag(row, "transparent_right", AuthoritativeDunTileFlags.TransparentRight)
                | ReadFlag(row, "trap", AuthoritativeDunTileFlags.Trap))));

        static AuthoritativeDunTileFlags ReadFlag(
            TsvRow row,
            string column,
            AuthoritativeDunTileFlags flag)
        {
            // The original TSV contract required only solid and block_missile.
            // Keep those columns required while allowing older rows to omit the
            // newly externalized .sol-compatible flags.
            var enabled = column is "solid" or "block_missile"
                ? row.RequiredBoolean(column)
                : row.OptionalBoolean(column);
            return enabled ? flag : AuthoritativeDunTileFlags.None;
        }
    }

    /** Loads the native one-byte .sol property table (MAXTILES = 1379). */
    public static AuthoritativeDunTilePropertyCatalog LoadSol(
        string sourcePath,
        ReadOnlyMemory<byte> contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var bytes = contents.Span;
        if (bytes.Length != NativeTileCount)
            throw new InvalidDataException(
                $"Native .sol asset '{sourcePath}' has {bytes.Length} bytes; expected {NativeTileCount}.");

        const byte supportedMask = (byte)(
            AuthoritativeDunTileFlags.Solid
            | AuthoritativeDunTileFlags.BlockLight
            | AuthoritativeDunTileFlags.BlockMissile
            | AuthoritativeDunTileFlags.Transparent
            | AuthoritativeDunTileFlags.TransparentLeft
            | AuthoritativeDunTileFlags.TransparentRight
            | AuthoritativeDunTileFlags.Trap);
        var definitions = new AuthoritativeDunTileProperty[NativeTileCount];
        for (var pieceId = 0; pieceId < bytes.Length; pieceId++) {
            var rawFlags = bytes[pieceId];
            if ((rawFlags & ~supportedMask) != 0)
                throw new InvalidDataException(
                    $"Native .sol asset '{sourcePath}' contains unsupported flags 0x{rawFlags:X2} at d-piece {pieceId}.");
            definitions[pieceId] = new AuthoritativeDunTileProperty(
                (uint)pieceId,
                (AuthoritativeDunTileFlags)rawFlags);
        }
        return new AuthoritativeDunTilePropertyCatalog(definitions);
    }
}

/** Shared external native geometry definitions supplied by a content pack. */
public sealed class AuthoritativeNativeDunGeometryCatalog
{
    public const string PieceDefinitionsTablePath = "native_dun_piece_definitions.tsv";
    public const string TilePropertiesTablePath = "native_dun_tile_properties.tsv";

    public AuthoritativeNativeDunGeometryCatalog(
        AuthoritativeDunPieceCatalog pieces,
        AuthoritativeDunTilePropertyCatalog tileProperties)
    {
        Pieces = pieces ?? throw new ArgumentNullException(nameof(pieces));
        TileProperties = tileProperties ?? throw new ArgumentNullException(nameof(tileProperties));
    }

    public AuthoritativeDunPieceCatalog Pieces { get; }

    public AuthoritativeDunTilePropertyCatalog TileProperties { get; }

    /** Loads the optional shared geometry pair from the composed content set. */
    public static AuthoritativeNativeDunGeometryCatalog? LoadOptional(
        ComposedContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var hasPieces = content.TryGetTable(PieceDefinitionsTablePath, out var piecesTable);
        var hasProperties = content.TryGetTable(TilePropertiesTablePath, out var propertiesTable);
        if (hasPieces != hasProperties)
            throw new InvalidDataException(
                "Native DUN external geometry requires both "
                + $"{PieceDefinitionsTablePath} and {TilePropertiesTablePath}.");
        return hasPieces
            ? LoadTsv(
                piecesTable.SourcePath,
                piecesTable.ToCanonicalTsv(),
                propertiesTable.SourcePath,
                propertiesTable.ToCanonicalTsv())
            : null;
    }

    public static AuthoritativeNativeDunGeometryCatalog LoadTsv(
        string pieceSourcePath,
        string pieceContents,
        string tilePropertySourcePath,
        string tilePropertyContents)
    {
        return new AuthoritativeNativeDunGeometryCatalog(
            AuthoritativeDunPieceCatalog.LoadTsv(pieceSourcePath, pieceContents),
            AuthoritativeDunTilePropertyCatalog.LoadTsv(tilePropertySourcePath, tilePropertyContents));
    }
}

/** Expands native .dun/.til/.sol data into authoritative movement geometry. */
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
        var nativePieceLayer = new ushort[checked(asset.TileWidth * asset.TileHeight)];
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
            suppressMultiplayerDeathDrops: suppressMultiplayerDeathDrops,
            nativeTransparency: asset.TransparencyLayer,
            nativeMonsterLayer: asset.MonsterLayer,
            nativeObjectLayer: asset.ObjectLayer,
            nativeMegaTileLayer: asset.TileLayer,
            nativePieceLayer: nativePieceLayer,
            nativePieceProperties: tileProperties.Definitions.ToDictionary(
                property => checked((ushort)property.PieceId),
                property => property.Flags),
            nativePieceCollisionCells: blocked,
            nativePieceMissileBlockedCells: missileBlocked) {
            StableSymbol = stableSymbol,
        };

        void AddPiece(uint pieceId, int positionX, int positionY)
        {
            if (!tileProperties.TryGet(pieceId, out var flags))
                throw new InvalidDataException(
                    $"Native .dun asset '{asset.SourcePath}' references d-piece {pieceId} without .sol properties.");
            var cell = positionY * asset.TileWidth + positionX;
            nativePieceLayer[cell] = checked((ushort)pieceId);
            if (flags.HasFlag(AuthoritativeDunTileFlags.Solid))
                blocked.Add(cell);
            if (flags.HasFlag(AuthoritativeDunTileFlags.BlockMissile))
                missileBlocked.Add(cell);
        }
    }
}
