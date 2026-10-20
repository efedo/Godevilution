namespace Devilution.Diablo;

/** Native trigger messages retain their original interface-mode values. */
public enum AuthoritativeCatacombsTriggerKind
{
    NextLevel = 0,
    PreviousLevel = 1,
    TownWarpUp = 6,
}

public sealed record AuthoritativeCatacombsTrigger(
    int X, int Y, AuthoritativeCatacombsTriggerKind Kind, int Level);

/** Owned generation and materialized semantic geometry; no assets or MPQs are opened. */
public sealed class AuthoritativeCatacombsGeometry
{
    internal AuthoritativeCatacombsGeometry(AuthoritativeCatacombsGenerationResult generation,
        AuthoritativeLevel level, byte[] effectiveProperties, IEnumerable<AuthoritativeCatacombsTrigger> triggers)
    {
        GenerationResult = generation;
        Level = level;
        EffectivePieceProperties = Array.AsReadOnly(effectiveProperties);
        Triggers = Array.AsReadOnly(triggers.ToArray());
    }

    public AuthoritativeCatacombsGenerationResult GenerationResult { get; }
    public AuthoritativeLevel Level { get; }
    /** Full native SOL bytes, including the opaque 0x40 bit and zero-filled tail. */
    public IReadOnlyList<byte> EffectivePieceProperties { get; }
    /** Actual InitL2Triggers scan order; the L8 down stair remains present. */
    public IReadOnlyList<AuthoritativeCatacombsTrigger> Triggers { get; }
}

/**
 * L2 Pass3 expansion and special-marker scan, separate from Cathedral corrections.
 * The runtime owner must bind stable level/depth identity and validate required
 * stair/warp anchors before admitting routes; this materializer permits test catalogs.
 */
public static class AuthoritativeCatacombsGeometryMaterializer
{
    public const int NativeWidth = 112;
    public const int NativeHeight = 112;
    public const int InteriorOffset = 16;
    public const int NativeMegaTileCount = 160;
    public const int DefaultExteriorMegaTileId = 12;
    private const byte NamedSolMask = 0xBF;

    public static AuthoritativeCatacombsGeometry Materialize(uint levelId,
        AuthoritativeCatacombsGenerationResult result, AuthoritativeDunPieceCatalog pieces,
        IReadOnlyList<byte> effectivePieceProperties, string stableSymbol,
        bool isDefaultStart = false, bool suppressMultiplayerDeathDrops = false)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(effectivePieceProperties);
        ArgumentException.ThrowIfNullOrWhiteSpace(stableSymbol);
        if (levelId == 0)
            throw new ArgumentOutOfRangeException(nameof(levelId));
        result.Validate();
        var definitions = pieces.Definitions;
        if (definitions.Count != NativeMegaTileCount
            || definitions.Where((definition, index) => definition.MegaTileId != index).Any())
            throw new InvalidDataException("Catacombs requires the complete ordered 160-record L2 TIL catalog.");
        if (effectivePieceProperties.Count != AuthoritativeDunTilePropertyCatalog.NativeTileCount)
            throw new InvalidDataException("Catacombs requires all 1379 effective native SOL bytes.");
        var owned = result.CopyOwned();
        var sol = effectivePieceProperties.ToArray();
        var fine = new ushort[NativeWidth * NativeHeight];
        for (var y = 0; y < NativeHeight; y += 2) {
            for (var x = 0; x < NativeWidth; x += 2)
                Expand(x, y, DefaultExteriorMegaTileId);
        }
        for (var y = 0; y < AuthoritativeCatacombsGenerationResult.MegaHeight; y++) {
            for (var x = 0; x < AuthoritativeCatacombsGenerationResult.MegaWidth; x++)
                Expand(InteriorOffset + 2 * x, InteriorOffset + 2 * y, owned.GetMegaTile(x, y));
        }
        var special = new ushort[fine.Length];
        for (var cell = 0; cell < fine.Length; cell++) {
            special[cell] = fine[cell] switch {
                540 or 177 or 550 => 5,
                541 or 552 => 6,
                _ => 0,
            };
        }
        // Preserve the production two-pass write order and fail malformed
        // metadata before an edge marker could write outside the fine grid.
        for (var y = 0; y < NativeHeight; y++) {
            for (var x = 0; x < NativeWidth; x++) {
                var piece = fine[y * NativeWidth + x];
                if (piece == 131) {
                    if (y + 2 >= NativeHeight)
                        throw new InvalidDataException("Catacombs vertical special marker crosses the fine-grid edge.");
                    special[(y + 1) * NativeWidth + x] = 2;
                    special[(y + 2) * NativeWidth + x] = 1;
                } else if (piece is 134 or 138) {
                    if (x + 2 >= NativeWidth)
                        throw new InvalidDataException("Catacombs horizontal special marker crosses the fine-grid edge.");
                    special[y * NativeWidth + x + 1] = 3;
                    special[y * NativeWidth + x + 2] = 4;
                }
            }
        }
        var blocked = new List<int>();
        var missiles = new List<int>();
        var light = new List<int>();
        var triggers = new List<AuthoritativeCatacombsTrigger>();
        for (var cell = 0; cell < fine.Length; cell++) {
            var properties = (AuthoritativeDunTileFlags)(sol[fine[cell]] & NamedSolMask);
            if ((properties & AuthoritativeDunTileFlags.Solid) != 0) blocked.Add(cell);
            if ((properties & AuthoritativeDunTileFlags.BlockMissile) != 0) missiles.Add(cell);
            if ((properties & AuthoritativeDunTileFlags.BlockLight) != 0) light.Add(cell);
            var kind = fine[cell] switch {
                266 => AuthoritativeCatacombsTriggerKind.PreviousLevel,
                558 => AuthoritativeCatacombsTriggerKind.TownWarpUp,
                270 => AuthoritativeCatacombsTriggerKind.NextLevel,
                _ => (AuthoritativeCatacombsTriggerKind?)null,
            };
            if (kind.HasValue)
                triggers.Add(new(cell % NativeWidth, cell / NativeWidth, kind.Value, 0));
        }
        var namedProperties = Enumerable.Range(0, sol.Length).ToDictionary(
            piece => (ushort)piece, piece => (AuthoritativeDunTileFlags)(sol[piece] & NamedSolMask));
        var level = new AuthoritativeLevel(levelId, NativeWidth, NativeHeight, blocked, missiles,
            isDefaultStart, suppressMultiplayerDeathDrops,
            nativeTransparency: owned.NativeTransparency.Select(value => (ushort)value),
            nativePieceLayer: fine, nativePieceProperties: namedProperties,
            nativePieceCollisionCells: blocked, nativePieceMissileBlockedCells: missiles,
            startPositionX: owned.EntryPosition.X, startPositionY: owned.EntryPosition.Y,
            nativeVisionBlockedCells: light, nativeSpecialLayer: special) {
            StableSymbol = stableSymbol,
        };
        if (isDefaultStart)
            level.ValidateStartPosition();
        return new(owned, level, sol, triggers);

        void Expand(int x, int y, int oneBasedId)
        {
            var definition = definitions[oneBasedId - 1];
            fine[y * NativeWidth + x] = checked((ushort)definition.NorthWestPieceId);
            fine[y * NativeWidth + x + 1] = checked((ushort)definition.NorthEastPieceId);
            fine[(y + 1) * NativeWidth + x] = checked((ushort)definition.SouthWestPieceId);
            fine[(y + 1) * NativeWidth + x + 1] = checked((ushort)definition.SouthEastPieceId);
        }
    }
}
