namespace Devilution.Diablo;

/** Immutable coarse/fine geometry pair for one ordinary Cathedral level. */
public sealed class AuthoritativeCathedralLevel1Geometry
{
    internal AuthoritativeCathedralLevel1Geometry(
        AuthoritativeCathedralLevel1Result generationResult,
        AuthoritativeLevel level)
    {
        GenerationResult = generationResult ?? throw new ArgumentNullException(nameof(generationResult));
        Level = level ?? throw new ArgumentNullException(nameof(level));
    }

    /** Deep-copied native 40x40 generation output, including seed and stair metadata. */
    public AuthoritativeCathedralLevel1Result GenerationResult { get; }

    /** Native 112x112 fine geometry; its mega layer remains in GenerationResult. */
    public AuthoritativeLevel Level { get; }
}

/**
 * Expands an ordinary Cathedral level-1 coarse result into the native
 * 112x112 coordinate space used by DevilutionX.
 *
 * The generator stores the same one-based mega-tile IDs that PlaceDunTiles
 * writes.  DRLG_LPass3 indexes the native .til table with ID - 1, expands
 * north-west/north-east/south-west/south-east pieces, and starts the generated
 * 40x40 area at world coordinate (16, 16).
 */
public static class AuthoritativeCathedralLevel1GeometryMaterializer
{
    public const int NativeWidth = 112;
    public const int NativeHeight = 112;
    public const int InteriorOffset = 16;
    public const ushort DefaultExteriorMegaTileId = 22;

    private const int NativeMegaWidth = NativeWidth / 2;
    private const int NativeMegaHeight = NativeHeight / 2;

    // LoadLevelSOLData applies these Cathedral-specific corrections after
    // reading l1.sol.  Keep the supplied catalog immutable and overlay the
    // corrections only on the materialized level's effective properties.
    private static readonly IReadOnlyDictionary<uint, AuthoritativeDunTileFlags> NativePropertyCorrections =
        new Dictionary<uint, AuthoritativeDunTileFlags> {
            [9] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [15] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [16] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [20] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [21] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [27] = AuthoritativeDunTileFlags.BlockMissile,
            [28] = AuthoritativeDunTileFlags.BlockMissile,
            [51] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [56] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [58] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [61] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [63] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [65] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [72] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [208] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [247] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [253] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [257] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [323] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
            [403] = AuthoritativeDunTileFlags.BlockLight,
            [24] = AuthoritativeDunTileFlags.BlockLight,
            [450] = AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile,
        };

    /** Materializes the generated result without mutating either input catalog or result. */
    public static AuthoritativeCathedralLevel1Geometry Materialize(
        uint levelId,
        AuthoritativeCathedralLevel1Result result,
        AuthoritativeDunPieceCatalog pieces,
        AuthoritativeDunTilePropertyCatalog tileProperties,
        string stableSymbol = "cathedral",
        bool isDefaultStart = true,
        bool suppressMultiplayerDeathDrops = false)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(tileProperties);
        ArgumentNullException.ThrowIfNull(stableSymbol);

        ValidateResult(result);
        var copiedResult = CopyResult(result);

        var effectiveProperties = tileProperties.Definitions
            .ToDictionary(property => property.PieceId, property => property.Flags);
        foreach (var (pieceId, correction) in NativePropertyCorrections) {
            if (effectiveProperties.TryGetValue(pieceId, out var flags))
                effectiveProperties[pieceId] = flags | correction;
        }
        var defaultPiece = GetPieceDefinition(
            pieces,
            DefaultExteriorMegaTileId,
            "default exterior mega-tile");
        ValidatePieceProperties(defaultPiece, effectiveProperties, DefaultExteriorMegaTileId);

        var nativePieceLayer = new ushort[checked(NativeWidth * NativeHeight)];

        // DRLG_LPass3 first expands Dirt - 1 over the complete native world,
        // including its 16-tile border.  The generated interior then replaces
        // those d-pieces at the exact world offset.
        for (var megaY = 0; megaY < NativeMegaHeight; megaY++) {
            for (var megaX = 0; megaX < NativeMegaWidth; megaX++) {
                WritePieceDefinition(
                    defaultPiece,
                    megaX * 2,
                    megaY * 2,
                    nativePieceLayer);
            }
        }

        for (var y = 0; y < AuthoritativeCathedralLevel1Result.MegaHeight; y++) {
            for (var x = 0; x < AuthoritativeCathedralLevel1Result.MegaWidth; x++) {
                var megaTileId = copiedResult.GetMegaTile(x, y);
                var definition = GetPieceDefinition(pieces, megaTileId, $"result mega-tile ({x}, {y})");
                ValidatePieceProperties(definition, effectiveProperties, megaTileId);
                var megaX = InteriorOffset / 2 + x;
                var megaY = InteriorOffset / 2 + y;
                WritePieceDefinition(
                    definition,
                    megaX * 2,
                    megaY * 2,
                    nativePieceLayer);
            }
        }

        var blockedCells = new List<int>();
        var missileBlockedCells = new List<int>();
        var visionBlockedCells = new List<int>();
        // AuthoritativeLevel conservatively unions movement-solid cells into
        // its vision blockers. The explicit list preserves native BlockLight
        // (including d-piece 0), but exact native vision parity is not claimed
        // until the shared level projection supports independent vision cells.
        for (var cell = 0; cell < nativePieceLayer.Length; cell++) {
            var flags = effectiveProperties[nativePieceLayer[cell]];
            if (flags.HasFlag(AuthoritativeDunTileFlags.Solid))
                blockedCells.Add(cell);
            if (flags.HasFlag(AuthoritativeDunTileFlags.BlockMissile))
                missileBlockedCells.Add(cell);
            if (flags.HasFlag(AuthoritativeDunTileFlags.BlockLight))
                visionBlockedCells.Add(cell);
        }

        var transparency = new ushort[checked(NativeWidth * NativeHeight)];
        for (var y = 0; y < AuthoritativeCathedralLevel1Result.TransparencyHeight; y++) {
            for (var x = 0; x < AuthoritativeCathedralLevel1Result.TransparencyWidth; x++) {
                transparency[(y + InteriorOffset) * NativeWidth + InteriorOffset + x]
                    = copiedResult.NativeTransparency[
                        y * AuthoritativeCathedralLevel1Result.TransparencyWidth + x];
            }
        }

        var level = new AuthoritativeLevel(
            levelId,
            NativeWidth,
            NativeHeight,
            blockedCells,
            missileBlockedCells,
            isDefaultStart,
            suppressMultiplayerDeathDrops,
            transparency,
            nativePieceLayer: nativePieceLayer,
            nativePieceProperties: effectiveProperties.ToDictionary(
                property => checked((ushort)property.Key),
                property => property.Value),
            nativePieceCollisionCells: blockedCells,
            nativePieceMissileBlockedCells: missileBlockedCells,
            nativeVisionBlockedCells: visionBlockedCells,
            startPositionX: copiedResult.EntryPosition.X,
            startPositionY: copiedResult.EntryPosition.Y) {
            StableSymbol = stableSymbol,
        };
        if (isDefaultStart)
            level.ValidateStartPosition();
        return new AuthoritativeCathedralLevel1Geometry(copiedResult, level);
    }

    private static AuthoritativeCathedralLevel1Result CopyResult(
        AuthoritativeCathedralLevel1Result result)
        => new(
            result.InputSeed,
            result.AttemptSeed,
            result.FinalRandomState,
            Array.AsReadOnly(result.MegaTiles.ToArray()),
            Array.AsReadOnly(result.NativeTransparency.ToArray()),
            result.StairsUp,
            result.StairsDown,
            result.EntryPosition) {
            NextTransparencyValue = result.NextTransparencyValue,
        };

    private static void ValidateResult(AuthoritativeCathedralLevel1Result result)
    {
        if (result.MegaTiles is null
            || result.MegaTiles.Count != AuthoritativeCathedralLevel1Result.MegaWidth
                * AuthoritativeCathedralLevel1Result.MegaHeight) {
            throw new InvalidDataException(
                "Cathedral level-1 result has an invalid 40x40 mega-tile layer.");
        }
        if (result.NativeTransparency is null
            || result.NativeTransparency.Count
                != AuthoritativeCathedralLevel1Result.TransparencyWidth
                    * AuthoritativeCathedralLevel1Result.TransparencyHeight) {
            throw new InvalidDataException(
                "Cathedral level-1 result has an invalid 80x80 transparency layer.");
        }
        if (result.NativeTransparency.Any(value => value > byte.MaxValue))
            throw new InvalidDataException(
                "Cathedral level-1 transparency values must be unsigned native bytes.");
        ValidateCoarsePoint(result.StairsUp, nameof(result.StairsUp));
        ValidateCoarsePoint(result.StairsDown, nameof(result.StairsDown));
        if (result.EntryPosition.X < 0 || result.EntryPosition.X >= NativeWidth
            || result.EntryPosition.Y < 0 || result.EntryPosition.Y >= NativeHeight) {
            throw new InvalidDataException(
                $"Cathedral level-1 entry position ({result.EntryPosition.X}, {result.EntryPosition.Y}) "
                + "is outside the native 112x112 world.");
        }
        if (result.MegaTiles.Any(value => value is 0 or > byte.MaxValue))
            throw new InvalidDataException(
                "Cathedral level-1 mega-tile IDs must be non-zero native byte IDs.");

        static void ValidateCoarsePoint(AuthoritativeCathedralPoint point, string name)
        {
            if (point.X < 0 || point.X >= AuthoritativeCathedralLevel1Result.MegaWidth
                || point.Y < 0 || point.Y >= AuthoritativeCathedralLevel1Result.MegaHeight) {
                throw new InvalidDataException(
                    $"Cathedral level-1 {name} ({point.X}, {point.Y}) is outside the 40x40 coarse world.");
            }
        }
    }

    private static AuthoritativeDunPieceDefinition GetPieceDefinition(
        AuthoritativeDunPieceCatalog pieces,
        ushort oneBasedMegaTileId,
        string context)
    {
        if (oneBasedMegaTileId == 0)
            throw new InvalidDataException($"Cathedral {context} uses the invalid one-based mega-tile ID 0.");
        var catalogMegaTileId = (uint)oneBasedMegaTileId - 1;
        if (!pieces.TryGet(catalogMegaTileId, out var definition))
            throw new InvalidDataException(
                $"Cathedral {context} references missing .til mega-tile {catalogMegaTileId}.");
        return definition;
    }

    private static void ValidatePieceProperties(
        AuthoritativeDunPieceDefinition definition,
        IReadOnlyDictionary<uint, AuthoritativeDunTileFlags> properties,
        ushort oneBasedMegaTileId)
    {
        foreach (var pieceId in new[] {
                     definition.NorthWestPieceId,
                     definition.NorthEastPieceId,
                     definition.SouthWestPieceId,
                     definition.SouthEastPieceId,
                 }) {
            if (!properties.ContainsKey(pieceId))
                throw new InvalidDataException(
                    $"Cathedral one-based mega-tile {oneBasedMegaTileId} references d-piece "
                    + $"{pieceId} without .sol properties.");
        }
    }

    private static void WritePieceDefinition(
        AuthoritativeDunPieceDefinition definition,
        int x,
        int y,
        ushort[] nativePieceLayer)
    {
        nativePieceLayer[y * NativeWidth + x] = checked((ushort)definition.NorthWestPieceId);
        nativePieceLayer[y * NativeWidth + x + 1] = checked((ushort)definition.NorthEastPieceId);
        nativePieceLayer[(y + 1) * NativeWidth + x] = checked((ushort)definition.SouthWestPieceId);
        nativePieceLayer[(y + 1) * NativeWidth + x + 1] = checked((ushort)definition.SouthEastPieceId);
    }
}
