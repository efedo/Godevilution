using Devilution.Diablo;
using Godevilution.Tools.Archive;

namespace Godevilution.Tools;

/** Cathedral geometry read from the player's archive (no native engine). */
public sealed record NativeSourceGeometry(
    AuthoritativeNativeDunGeometryCatalog Catalog,
    IReadOnlyList<byte> EffectivePieceProperties);

public static class NativeSourceGeometryExport
{
    public const string TilPath = "levels\\l1data\\l1.til";
    public const string SolPath = "levels\\l1data\\l1.sol";

    // Bit 0x40 has no TileProperties meaning; native keeps it, the catalog refuses it.
    private const byte UnnamedSolBit = 0x40;

    public static NativeSourceGeometry Read(MpqArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var pieces = AuthoritativeDunPieceCatalog.LoadTil("l1.til", archive.ReadFile(TilPath));
        var effective = ApplyCathedralSolFixes(archive.ReadFile(SolPath));
        var named = effective.Select(value => (byte)(value & ~UnnamedSolBit)).ToArray();
        var properties = AuthoritativeDunTilePropertyCatalog.LoadSol("l1.sol", named);
        return new(new AuthoritativeNativeDunGeometryCatalog(pieces, properties), Array.AsReadOnly(effective));
    }

    /**
     * LoadLevelSOLData (legacy-native/game/levels/dungeon_common.cpp), DTYPE_CATHEDRAL:
     * the file fills the start of a MAXTILES (1379) zeroed table, then native
     * patches incorrectly marked arches, a pillar and a wall.
     */
    public static byte[] ApplyCathedralSolFixes(ReadOnlySpan<byte> sol)
    {
        if (sol.Length is < 1 or > AuthoritativeDunTilePropertyCatalog.NativeTileCount)
            throw new InvalidDataException($"Cathedral .sol data must contain between 1 and {AuthoritativeDunTilePropertyCatalog.NativeTileCount} bytes.");
        var properties = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        sol.CopyTo(properties);
        const byte blockLight = (byte)AuthoritativeDunTileFlags.BlockLight;
        const byte blockMissile = (byte)AuthoritativeDunTileFlags.BlockMissile;
        // Fix incorrectly marked arched tiles.
        foreach (var piece in new[] { 9, 15, 16, 20, 21 })
            properties[piece] |= blockLight | blockMissile;
        properties[27] |= blockMissile;
        properties[28] |= blockMissile;
        foreach (var piece in new[] { 51, 56, 58, 61, 63, 65, 72, 208, 247, 253, 257, 323 })
            properties[piece] |= blockLight | blockMissile;
        properties[403] |= blockLight;
        // Fix incorrectly marked pillar tile.
        properties[24] |= blockLight;
        // Fix incorrectly marked wall tile.
        properties[450] |= blockLight | blockMissile;
        return properties;
    }
}
